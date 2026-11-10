using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoCare.Application.Abstractions;
using MotoCare.Infrastructure.Persistence;
using MotoCare.Infrastructure.Persistence.Repositories;
using MotoCare.Infrastructure.Security;

namespace MotoCare.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.AddSingleton<DbExecutor>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMasterRepository, MasterRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IJobCardRepository, JobCardRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IPurchasingRepository, PurchasingRepository>();
        services.AddScoped<ISupplierBillRepository, SupplierBillRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();   // Added: reports
        services.AddScoped<IPrintRepository, PrintRepository>();     // Added: reprint

        // ---- Added: quotations, part search, stock counting, payroll, messages
        services.AddScoped<IPartRepository, PartRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IStockCountRepository, StockCountRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.Configure<Messaging.MessagingOptions>(config.GetSection("Messaging"));
        services.AddHttpClient("whatsapp");
        services.AddScoped<IEmailSender, Messaging.SmtpEmailSender>();
        services.AddScoped<IWhatsAppSender, Messaging.WhatsAppCloudSender>();
        services.AddScoped<IDocumentPdfBuilder, Documents.PdfDocumentBuilder>();
        services.AddHostedService<Messaging.MessageDispatcher>();     // sends pending messages every 30 seconds
        return services;
    }
}
