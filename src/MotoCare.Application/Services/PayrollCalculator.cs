using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>EPF / ETF settings used for one payroll run.</summary>
public sealed record EpfSettings(bool Enabled, decimal EmployeePct, decimal EmployerPct, decimal EtfPct, bool IncludeCommission);

/// <summary>
/// Added: salary maths in one place.
///   basic        = monthly basic (or basic x days / 30 when "pay full basic" is off)
///   commission   = sum(service value x %)  - % for that service, else the employee's default %
///   allowances   = fixed monthly allowances + extra allowances added to this run
///   gross        = basic + commission + allowances
///   EPF base     = basic + allowances marked "include in EPF" (+ commission when that setting is on)
///   EPF employee = base x 8%   (deducted from pay)       -- Sri Lanka rates, editable in Payroll settings
///   EPF employer = base x 12%  (company cost)
///   ETF          = base x 3%   (company cost)
///   net pay      = gross - EPF employee - deductions (extra deductions + advances / expenses taken)
/// </summary>
public static class PayrollCalculator
{
    public static (List<PayrollLineModel> Lines, List<PayrollDetailModel> Details) Calculate(
        ResultBag inputs, List<RowData> extras, EpfSettings epf, bool payFullBasic, DateTime from, DateTime to)
    {
        var lines = new List<PayrollLineModel>();
        var details = new List<PayrollDetailModel>();
        int days = (to.Date - from.Date).Days + 1;

        var allowances = inputs.Rows("allowances");
        var commissions = inputs.Rows("commissions");

        foreach (var emp in inputs.Rows("employees"))
        {
            int id = emp.Get<int>("EmployeeId");

            // ---- basic
            decimal monthlyBasic = emp.Get<decimal>("BasicSalary");
            decimal basic = payFullBasic ? monthlyBasic : R(monthlyBasic * days / 30m);

            // ---- commission per service
            decimal defaultPct = emp.Get<decimal>("DefaultCommissionPct");
            decimal commission = 0;
            foreach (var c in commissions.Where(c => c.Get<int>("EmployeeId") == id))
            {
                decimal amount = c.Get<decimal>("ServiceAmount");
                decimal pct = c.Get<decimal?>("ServicePct") ?? defaultPct;
                decimal value = R(amount * pct / 100);
                if (value == 0) continue;
                commission += value;
                details.Add(new PayrollDetailModel(id, "COMMISSION", c.Get<string>("ServiceName"), amount, pct, value));
            }

            // ---- fixed monthly allowances
            decimal fixedAllowances = 0, epfAllowances = 0;
            foreach (var a in allowances.Where(a => a.Get<int>("EmployeeId") == id))
            {
                decimal amount = a.Get<decimal>("Amount");
                fixedAllowances += amount;
                if (a.Get<bool>("EpfApplicable")) epfAllowances += amount;
                details.Add(new PayrollDetailModel(id, "ALLOWANCE", a.Get<string>("AllowanceName"), 0, 0, amount));
            }

            // ---- extras added to this run (allowances or deductions)
            decimal extraAllowances = 0, deductions = 0;
            foreach (var x in extras.Where(x => x.Get<int>("EmployeeId") == id))
            {
                decimal amount = x.Get<decimal>("Amount");
                if (x.Get<bool>("IsDeduction")) { deductions += amount; continue; }
                extraAllowances += amount;
                if (x.Get<bool>("EpfApplicable")) epfAllowances += amount;
            }

            // ---- Added: advances / expenses taken by the employee are deducted
            deductions += inputs.Rows("advances").Where(a => a.Get<int>("EmployeeId") == id).Sum(a => a.Get<decimal>("Amount"));

            // ---- totals, EPF / ETF
            decimal gross = basic + commission + fixedAllowances + extraAllowances;
            decimal epfBase = basic + epfAllowances + (epf.IncludeCommission ? commission : 0);
            decimal epfEmployee = epf.Enabled ? R(epfBase * epf.EmployeePct / 100) : 0;
            decimal epfEmployer = epf.Enabled ? R(epfBase * epf.EmployerPct / 100) : 0;
            decimal etf = epf.Enabled ? R(epfBase * epf.EtfPct / 100) : 0;
            decimal net = gross - epfEmployee - deductions;

            lines.Add(new PayrollLineModel(id, basic, commission, fixedAllowances, extraAllowances, gross,
                                           epf.Enabled ? epfBase : 0, epfEmployee, epfEmployer, etf, deductions, net));
        }
        return (lines, details);
    }

    private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Reads the Settings table as key -> value.</summary>
public static class SettingsReader
{
    public static async Task<Dictionary<string, string>> ReadAsync(ISettingsRepository repo) =>
        (await repo.ListAsync()).ToDictionary(r => r.Get<string>("SettingKey"), r => r.Get<string>("SettingValue") ?? "",
                                               StringComparer.OrdinalIgnoreCase);

    public static string Text(this Dictionary<string, string> s, string key, string fallback = "") =>
        s.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;

    public static bool Flag(this Dictionary<string, string> s, string key) => s.Text(key) == "1";

    public static decimal Number(this Dictionary<string, string> s, string key, decimal fallback) =>
        decimal.TryParse(s.Text(key), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;
}
