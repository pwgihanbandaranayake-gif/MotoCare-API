using Microsoft.Extensions.DependencyInjection;
using MotoCare.Application.Services;

namespace MotoCare.Application;

/// <summary>Registers every application service (called from Program.cs).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IPermissionService, PermissionService>();

        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<MasterService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<VehicleService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<JobCardService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<StockService>();
        services.AddScoped<PurchasingService>();
        services.AddScoped<SupplierBillService>();
        services.AddScoped<ReportService>();   // Added: reports
        services.AddScoped<PrintService>();    // Added: reprint
        services.AddScoped<QuotationService>();    // Added
        services.AddScoped<StockCountService>();   // Added
        services.AddScoped<PayrollService>();      // Added
        services.AddScoped<MessageService>();      // Added
        return services;
    }
}
