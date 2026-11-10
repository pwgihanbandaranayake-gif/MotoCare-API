using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>
/// Added: salary processing.
///   Setup : employee basic salary, default commission %, commission % per service, fixed allowances
///   Run   : choose a date range -> salaries are calculated (Draft) -> add extra allowances / deductions
///           (recalculated automatically) -> Approve -> Mark paid. Payslips can be printed / sent.
/// </summary>
public sealed class PayrollService(IPayrollRepository repo, ISettingsRepository settings, MasterService masters,
                                   MessageService messages, ICurrentUser me)
{
    // =================================================================== setup
    public async Task SaveProfileAsync(int employeeId, PayrollProfileDto dto)
    {
        Check.Found(await masters.GetAsync("employees", employeeId), "Employee");
        Check.NotNegative(dto.BasicSalary, "Basic salary");
        Check.That(dto.DefaultCommissionPct is >= 0 and <= 100, "Default commission % must be between 0 and 100.");

        // only services with a % entered are saved
        var commissions = (dto.Commissions ?? new()).Where(c => c.CommissionPct is not null).ToList();
        foreach (var c in commissions)
            Check.That(c.CommissionPct is >= 0 and <= 100, "Commission % must be between 0 and 100.");
        Check.That(commissions.Select(c => c.ServiceId).Distinct().Count() == commissions.Count, "A service is listed twice.");

        // only allowances with an amount are saved
        var allowances = (dto.Allowances ?? new()).Where(a => a.Amount is > 0).ToList();
        Check.That(allowances.Select(a => a.AllowanceTypeId).Distinct().Count() == allowances.Count, "An allowance is listed twice.");

        await repo.SaveProfileAsync(employeeId, dto, commissions, allowances);
    }

    // =================================================================== runs
    public async Task<int> CreateRunAsync(PayrollRunDto dto)
    {
        Check.That(dto.FromDate.Date <= dto.ToDate.Date, "'From' date must be on or before the 'To' date.");
        Check.That((dto.ToDate.Date - dto.FromDate.Date).TotalDays <= 62, "A payroll run can cover at most 2 months.");
        return await CalculateAndSaveAsync(0, dto.FromDate.Date, dto.ToDate.Date, dto.PayFullBasic, dto.Notes, new List<RowData>());
    }

    /// <summary>Calculates again with the current setup, settings and extras (Draft runs only).</summary>
    public async Task RecalculateAsync(int runId)
    {
        var bag = await repo.RunAsync(runId);
        var run = DraftRun(bag);
        await CalculateAndSaveAsync(runId, run.Get<DateTime>("FromDate"), run.Get<DateTime>("ToDate"),
                                    run.Get<bool>("PayFullBasic"), run.Get<string?>("Notes"), bag.Rows("extras"));
    }

    public async Task AddExtraAsync(int runId, PayrollExtraDto dto)
    {
        var bag = await repo.RunAsync(runId);
        DraftRun(bag);
        Check.Required(dto.Description, "Description");
        Check.Positive(dto.Amount, "Amount");
        if (dto.EmployeeId is > 0)
            Check.That(bag.Rows("lines").Any(l => l.Get<int>("EmployeeId") == dto.EmployeeId), "The employee is not in this payroll run.");

        await repo.AddExtraAsync(runId, dto with { Description = dto.Description!.Trim(), EmployeeId = dto.EmployeeId is > 0 ? dto.EmployeeId : null });
        await RecalculateAsync(runId);
    }

    public async Task DeleteExtraAsync(int runId, int extraId)
    {
        var bag = await repo.RunAsync(runId);
        DraftRun(bag);
        Check.That(bag.Rows("extras").Any(x => x.Get<int>("ExtraId") == extraId), "Item not found in this run.");
        await repo.DeleteExtraAsync(extraId);
        await RecalculateAsync(runId);
    }

    /// <summary>Draft -> Approved -> Paid. Approved can go back to Draft (reopen).</summary>
    public async Task SetStatusAsync(int runId, string? status)
    {
        var bag = await repo.RunAsync(runId);
        var run = Check.Found(bag.Row("run"), "Payroll run");
        string current = run.Get<string>("Status");
        string next = status?.Trim() ?? "";

        bool allowed = (current, next) switch
        {
            ("Draft", "Approved") => true,
            ("Approved", "Draft") => true,
            ("Approved", "Paid") => true,
            _ => false
        };
        Check.That(allowed, $"A {current.ToLower()} payroll run cannot be changed to {next}.");
        Check.That(next != "Approved" || bag.Rows("lines").Count > 0, "There are no salary lines to approve.");

        await repo.SetStatusAsync(runId, next, me.UserId);

        // auto-send payslips when the run is approved (Messages > settings)
        if (next == "Approved")
            foreach (var line in bag.Rows("lines"))
                await messages.AutoSendAsync("PAYSLIP", line.Get<int>("LineId"));
    }

    public async Task DeleteRunAsync(int runId)
    {
        DraftRun(await repo.RunAsync(runId));
        await repo.DeleteRunAsync(runId);
    }

    // =================================================================== Added: advances / expenses
    public static readonly string[] AdvanceTypes = { "Advance", "Expense" };

    public async Task<int> SaveAdvanceAsync(AdvanceSaveDto dto)
    {
        Check.Found(await masters.GetAsync("employees", dto.EmployeeId), "Employee");
        string type = dto.AdvanceType?.Trim() ?? "";
        Check.That(AdvanceTypes.Contains(type), "Type must be Advance or Expense.");
        Check.Required(dto.Description, "Description");
        Check.Positive(dto.Amount, "Amount");
        Check.That(dto.AdvanceDate.Date <= DateTime.Today, "Date cannot be in the future.");
        if (dto.AdvanceId > 0)
        {
            var current = Check.Found(await repo.AdvanceGetAsync(dto.AdvanceId), "Advance");
            Check.That(current.Get<int?>("RunId") is null, "This amount is already in a payroll run. Delete the draft run or reopen it first.");
        }
        return await repo.SaveAdvanceAsync(dto with { AdvanceType = type, Description = dto.Description!.Trim() }, me.UserId);
    }

    public async Task DeleteAdvanceAsync(int id)
    {
        var current = Check.Found(await repo.AdvanceGetAsync(id), "Advance");
        Check.That(current.Get<int?>("RunId") is null, "This amount is already in a payroll run and cannot be deleted.");
        await repo.DeleteAdvanceAsync(id);
    }

    // =================================================================== helpers
    private async Task<int> CalculateAndSaveAsync(int runId, DateTime from, DateTime to, bool payFullBasic, string? notes, List<RowData> extras)
    {
        var s = await SettingsReader.ReadAsync(settings);
        var epf = new EpfSettings(s.Flag("PayrollEpfEnabled"), s.Number("EpfEmployeePct", 8), s.Number("EpfEmployerPct", 12),
                                  s.Number("EtfPct", 3), s.Flag("EpfIncludeCommission"));

        var inputs = await repo.InputsAsync(from, to, runId > 0 ? runId : null);
        Check.That(inputs.Rows("employees").Count > 0, "No employees are on payroll. Set basic salaries in Payroll > Salary setup first.");

        var (lines, details) = PayrollCalculator.Calculate(inputs, extras, epf, payFullBasic, from, to);
        var model = new PayrollRunModel(runId, from, to, payFullBasic, epf.Enabled, epf.EmployeePct, epf.EmployerPct, epf.EtfPct,
                                        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(), me.UserId, lines, details);
        return await repo.SaveRunAsync(model);
    }

    private static RowData DraftRun(ResultBag bag)
    {
        var run = Check.Found(bag.Row("run"), "Payroll run");
        Check.That(run.Get<string>("Status") == "Draft", "Only draft payroll runs can be changed. Reopen it first.");
        return run;
    }
}
