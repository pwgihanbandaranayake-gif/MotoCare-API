using MotoCare.Application.Abstractions;
using MotoCare.Application.Dtos;

namespace MotoCare.Infrastructure.Persistence.Repositories;

// Customers, vehicles, appointments and job cards.

public sealed class CustomerRepository(DbExecutor db) : ICustomerRepository
{
    public Task<List<RowData>> ListAsync(string? search, bool outstandingOnly) =>
        db.ListAsync("dbo.sp_Customer_List", new { Search = search, OutstandingOnly = outstandingOnly });

    public Task<ResultBag> GetAsync(int id) =>
        db.MultiAsync("dbo.sp_Customer_Get", new { Id = id }, "customer?", "vehicles", "openInvoices");

    public Task<int> SaveAsync(CustomerSaveDto d, List<VehicleLineDto> vehicles) =>
        db.SaveAsync("dbo.sp_Customer_Save", new
        {
            d.CustomerId, d.IdentifyBy, d.CustomerName, d.Mobile, NIC = d.Nic, d.Email, d.Address, d.City,
            d.CustomerTypeId, d.CreditLimit, d.PaymentTermsDays, d.IsActive,
            SendBillVia = d.SendBillVia ?? "None",   // Added
            Vehicles = TableTypes.Vehicles(vehicles)
        });

    public Task<List<RowData>> LookupAsync(string term) =>
        db.ListAsync("dbo.sp_Customer_Lookup", new { Term = term });

    public Task<RowData?> CheckDuplicateAsync(string? regNo, string? mobile, int customerId) =>
        db.SingleAsync("dbo.sp_Customer_CheckDuplicate", new { RegNo = regNo, Mobile = mobile, CustomerId = customerId });
}

public sealed class VehicleRepository(DbExecutor db) : IVehicleRepository
{
    public Task<List<RowData>> ListAsync(string? search, int? customerId) =>
        db.ListAsync("dbo.sp_Vehicle_List", new { Search = search, CustomerId = customerId });

    public Task<ResultBag> GetAsync(int id) =>
        db.MultiAsync("dbo.sp_Vehicle_Get", new { Id = id }, "vehicle?", "reminders", "history");

    public Task<int> SaveAsync(VehicleSaveDto d) =>
        db.SaveAsync("dbo.sp_Vehicle_Save", new
        {
            d.VehicleId, d.CustomerId, d.RegNo, d.ModelId, d.Year, d.ChassisNo, d.EngineNo, d.Colour,
            d.Odometer, d.ReminderIntervalKm, d.IsActive
        });

    public Task<List<RowData>> RemindersAsync(string? status, int daysAhead) =>
        db.ListAsync("dbo.sp_Reminder_List", new { Status = status, DaysAhead = daysAhead });

    public Task<int> SaveReminderAsync(ReminderSaveDto d) =>
        db.SaveAsync("dbo.sp_Reminder_Save", new { d.ReminderId, d.VehicleId, DueDate = d.DueDate!.Value.Date, d.DueKm, d.Notes });

    public Task SetReminderStatusAsync(int reminderId, string status) =>
        db.ExecAsync("dbo.sp_Reminder_SetStatus", new { ReminderId = reminderId, Status = status });
}

public sealed class AppointmentRepository(DbExecutor db) : IAppointmentRepository
{
    public Task<List<RowData>> ListAsync(DateTime from, DateTime to, string? status) =>
        db.ListAsync("dbo.sp_Appointment_List", new { From = from.Date, To = to.Date, Status = status });

    public Task<RowData?> GetAsync(int id) => db.SingleAsync("dbo.sp_Appointment_Get", new { Id = id });

    public Task<int> SaveAsync(AppointmentSaveDto d) =>
        db.SaveAsync("dbo.sp_Appointment_Save", new
        {
            d.AppointmentId, d.CustomerId, d.VehicleId, ApptDate = d.ApptDate!.Value.Date,
            ApptTime = TimeSpan.Parse(d.ApptTime!), d.ServiceRequired, d.Notes, d.Status
        });

    public Task SetStatusAsync(int id, string status) =>
        db.ExecAsync("dbo.sp_Appointment_SetStatus", new { AppointmentId = id, Status = status });

    public Task<int> CheckInAsync(int id, int? employeeId, int odometer, string? complaints) =>
        db.SaveAsync("dbo.sp_Appointment_CheckIn", new { AppointmentId = id, EmployeeId = employeeId, Odometer = odometer, Complaints = complaints });
}

public sealed class JobCardRepository(DbExecutor db) : IJobCardRepository
{
    public Task<List<RowData>> ListAsync(string? status, string? search) =>
        db.ListAsync("dbo.sp_JobCard_List", new { Status = status, Search = search });

    public Task<ResultBag> GetAsync(int id) =>
        db.MultiAsync("dbo.sp_JobCard_Get", new { Id = id }, "job?", "services", "parts");

    public Task<int> SaveAsync(JobCardSaveDto d) =>
        db.SaveAsync("dbo.sp_JobCard_Save", new { d.JobCardId, d.CustomerId, d.VehicleId, d.EmployeeId, d.Odometer, d.Complaints, d.AppointmentId,
                                                  d.NextServiceKm });   // Added

    public Task AddServiceAsync(int jobCardId, int serviceId, decimal charge) =>
        db.ExecAsync("dbo.sp_JobCard_AddService", new { JobCardId = jobCardId, ServiceId = serviceId, Charge = charge });

    public Task RemoveServiceAsync(int jobServiceId) =>
        db.ExecAsync("dbo.sp_JobCard_RemoveService", new { JobServiceId = jobServiceId });

    public Task IssuePartAsync(int jobCardId, int partId, decimal qty, decimal unitPrice) =>
        db.ExecAsync("dbo.sp_JobCard_IssuePart", new { JobCardId = jobCardId, PartId = partId, Qty = qty, UnitPrice = unitPrice });

    public Task RemovePartAsync(int jobPartId) =>
        db.ExecAsync("dbo.sp_JobCard_RemovePart", new { JobPartId = jobPartId });

    public Task SetStatusAsync(int jobCardId, string status) =>
        db.ExecAsync("dbo.sp_JobCard_SetStatus", new { JobCardId = jobCardId, Status = status });

    public Task<List<RowData>> ReadyToBillAsync() => db.ListAsync("dbo.sp_JobCard_ReadyToBill");
}
