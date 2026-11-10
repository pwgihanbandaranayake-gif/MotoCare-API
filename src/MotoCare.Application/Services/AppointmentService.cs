using System.Globalization;
using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;

namespace MotoCare.Application.Services;

/// <summary>Booking appointments and checking vehicles in (opens a job card).</summary>
public sealed class AppointmentService(IAppointmentRepository repo, IVehicleRepository vehicles)
{
    private static readonly string[] ManualStatuses = { "Pending", "Confirmed", "Cancelled", "No show" };

    public async Task<int> SaveAsync(AppointmentSaveDto dto)
    {
        Check.Required(dto.CustomerId, "Customer");
        Check.Required(dto.VehicleId, "Vehicle");
        Check.That(dto.ApptDate.HasValue, "Appointment date is required.");
        Check.That(TimeSpan.TryParse(dto.ApptTime, CultureInfo.InvariantCulture, out _), "Enter a valid time, e.g. 09:30.");

        // new bookings cannot be in the past
        if (dto.AppointmentId == 0)
            Check.That(dto.ApptDate!.Value.Date >= DateTime.Today, "Appointments cannot be booked in the past.");

        // the vehicle must belong to the selected customer
        await EnsureVehicleBelongsAsync(dto.VehicleId, dto.CustomerId);

        // a checked-in appointment is locked
        if (dto.AppointmentId > 0)
        {
            var current = Check.Found(await repo.GetAsync(dto.AppointmentId), "Appointment");
            Check.That(current.Get<string>("Status") != "Checked in", "This appointment is already checked in.");
        }

        var status = string.IsNullOrWhiteSpace(dto.Status) ? "Pending" : dto.Status;
        return await repo.SaveAsync(dto with { Status = status });
    }

    public async Task SetStatusAsync(int id, string? status)
    {
        Check.That(ManualStatuses.Contains(status), "Use check-in to mark an appointment as checked in.");
        var current = Check.Found(await repo.GetAsync(id), "Appointment");
        Check.That(current.Get<string>("Status") != "Checked in", "This appointment is already checked in.");
        await repo.SetStatusAsync(id, status!);
    }

    public async Task<int> CheckInAsync(int id, CheckInDto dto)
    {
        Check.That(dto.Odometer >= 0, "Odometer cannot be negative.");

        var appt = Check.Found(await repo.GetAsync(id), "Appointment");
        var status = appt.Get<string>("Status");
        Check.That(status is not ("Checked in" or "Cancelled"), "This appointment is already checked in or cancelled.");

        // complaints default to "service required - notes"
        var complaints = dto.Complaints;
        if (string.IsNullOrWhiteSpace(complaints))
        {
            var parts = new[] { appt.Get<string?>("ServiceRequired"), appt.Get<string?>("Notes") }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            complaints = string.Join(" - ", parts);
        }

        return await repo.CheckInAsync(id, dto.EmployeeId, dto.Odometer, complaints);
    }

    private async Task EnsureVehicleBelongsAsync(int vehicleId, int customerId)
    {
        var vehicle = Check.Found((await vehicles.GetAsync(vehicleId)).Row("vehicle"), "Vehicle");
        Check.That(vehicle.Get<int>("CustomerId") == customerId, "The selected vehicle does not belong to this customer.");
    }
}
