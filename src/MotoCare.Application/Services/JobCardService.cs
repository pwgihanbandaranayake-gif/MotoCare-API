using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Domain.Constants;

namespace MotoCare.Application.Services;

/// <summary>
/// Job cards: header, services and parts.
/// Services and parts can only change while the job is "Open" or "In progress".
/// </summary>
public sealed class JobCardService(IJobCardRepository repo, IVehicleRepository vehicles, IInvoiceRepository invoices, MasterService masters)
{
    private static readonly string[] AllowedStatuses =
        { JobCardStatus.Open, JobCardStatus.InProgress, JobCardStatus.Completed, JobCardStatus.Cancelled };

    // ---------------------------------------------------------------- header

    public async Task<int> SaveAsync(JobCardSaveDto dto)
    {
        Check.Required(dto.CustomerId, "Customer");
        Check.Required(dto.VehicleId, "Vehicle");
        Check.That(dto.Odometer >= 0, "Odometer cannot be negative.");
        // Added: next service must be after the current odometer (blank = not set)
        Check.That(dto.NextServiceKm is null || dto.NextServiceKm > dto.Odometer, "Next service km must be more than the current odometer.");

        var vehicle = Check.Found((await vehicles.GetAsync(dto.VehicleId)).Row("vehicle"), "Vehicle");
        Check.That(vehicle.Get<int>("CustomerId") == dto.CustomerId, "The selected vehicle does not belong to this customer.");

        if (dto.JobCardId > 0)
            await GetEditableJobAsync(dto.JobCardId);

        return await repo.SaveAsync(dto);
    }

    // ---------------------------------------------------------------- services

    public async Task AddServiceAsync(int jobCardId, JobServiceAddDto dto)
    {
        await GetEditableJobAsync(jobCardId);

        var service = Check.Found(await masters.GetAsync("services", dto.ServiceId), "Service");
        Check.That(service.Get<bool>("IsActive"), "This service is inactive.");

        // charge defaults to the catalogue labour charge
        var charge = dto.Charge ?? service.Get<decimal>("LabourCharge");
        Check.NotNegative(charge, "Charge");

        await repo.AddServiceAsync(jobCardId, dto.ServiceId, charge);
    }

    public async Task RemoveServiceAsync(int jobCardId, int jobServiceId)
    {
        var job = await GetEditableJobAsync(jobCardId);
        Check.That(job.Rows("services").Any(s => s.Get<int>("JobServiceId") == jobServiceId), "Service line not found on this job.");
        await repo.RemoveServiceAsync(jobServiceId);
    }

    // ---------------------------------------------------------------- parts

    public async Task IssuePartAsync(int jobCardId, JobPartIssueDto dto)
    {
        await GetEditableJobAsync(jobCardId);
        Check.Positive(dto.Qty, "Quantity");

        var part = Check.Found(await masters.GetAsync("parts", dto.PartId), "Part");
        var stock = part.Get<decimal>("StockQty");
        Check.That(stock >= dto.Qty, $"Not enough stock for {part.Get<string>("PartNo")}. Available {stock:0.##}.");

        // price defaults to the part's selling price
        var price = dto.UnitPrice ?? part.Get<decimal>("SellPrice");
        Check.NotNegative(price, "Unit price");

        await repo.IssuePartAsync(jobCardId, dto.PartId, dto.Qty, price);
    }

    public async Task RemovePartAsync(int jobCardId, int jobPartId)
    {
        var job = await GetEditableJobAsync(jobCardId);
        Check.That(job.Rows("parts").Any(p => p.Get<int>("JobPartId") == jobPartId), "Part line not found on this job.");
        await repo.RemovePartAsync(jobPartId);
    }

    // ---------------------------------------------------------------- status

    public async Task SetStatusAsync(int jobCardId, string? status)
    {
        Check.That(AllowedStatuses.Contains(status), "Invalid job card status.");

        var bag = await repo.GetAsync(jobCardId);
        var job = Check.Found(bag.Row("job"), "Job card");
        var current = job.Get<string>("Status");

        Check.That(current is not (JobCardStatus.Invoiced or JobCardStatus.Cancelled), "Invoiced or cancelled job cards cannot change status.");

        // a job needs at least one line before it can be completed
        if (status == JobCardStatus.Completed)
            Check.That(bag.Rows("services").Count + bag.Rows("parts").Count > 0, "Add at least one service or part before completing the job.");

        // re-opening a completed job is blocked while a draft invoice exists for it
        if (current == JobCardStatus.Completed && status != JobCardStatus.Completed)
        {
            var billing = (await invoices.LoadFromJobAsync(jobCardId)).Row("job");
            Check.That(billing?.Get<int?>("DraftInvoiceId") is null, "A draft invoice exists for this job. Delete or finish it first.");
        }

        await repo.SetStatusAsync(jobCardId, status!);
    }

    /// <summary>Loads the job card and checks that its lines may still change.</summary>
    private async Task<ResultBag> GetEditableJobAsync(int jobCardId)
    {
        var bag = await repo.GetAsync(jobCardId);
        var job = Check.Found(bag.Row("job"), "Job card");
        var status = job.Get<string>("Status");
        Check.That(status is JobCardStatus.Open or JobCardStatus.InProgress,
                   "This job card is closed. Services and parts can only change while it is open or in progress.");
        return bag;
    }
}
