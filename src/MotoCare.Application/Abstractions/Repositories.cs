using MotoCare.Application.Dtos;
using MotoCare.Application.Masters;

namespace MotoCare.Application.Abstractions;

// =====================================================================
// Repository contracts. Each method calls ONE stored procedure.
// Implementations live in MotoCare.Infrastructure/Persistence/Repositories.
// =====================================================================

public interface IAuthRepository
{
    Task<LoginUserRecord?> GetForLoginAsync(string username);
    Task TouchLoginAsync(int userId);
}

public interface IUserRepository
{
    Task<List<RowData>> ListAsync();
    Task<int> SaveAsync(int userId, string username, string? passwordHash, int? employeeId, int roleId, bool isActive);
    Task<List<RowData>> GetPermissionsAsync(int roleId);
    Task SavePermissionsAsync(int roleId, IEnumerable<PermissionDto> permissions);
    Task<bool> CheckPermissionAsync(int roleId, string module, string action);
}

public interface IMasterRepository
{
    Task<List<RowData>> ListAsync(MasterDefinition def, string? search, bool activeOnly, int? parentId, string? filter);
    Task<RowData?> GetAsync(MasterDefinition def, int id);
    Task<int> SaveAsync(MasterDefinition def, int id, IReadOnlyDictionary<string, object?> values, bool isActive);
    Task<string> DeleteAsync(MasterDefinition def, int id);
}

public interface ISettingsRepository
{
    Task<List<RowData>> ListAsync();
    Task SaveAsync(string key, string value);
    Task<List<RowData>> DocNumbersAsync();
    Task SaveDocNumberAsync(DocNumberDto dto);
}

public interface IDashboardRepository
{
    Task<ResultBag> GetAsync();
}

public interface ICustomerRepository
{
    Task<List<RowData>> ListAsync(string? search, bool outstandingOnly);
    Task<ResultBag> GetAsync(int id);                       // customer?, vehicles, openInvoices
    Task<int> SaveAsync(CustomerSaveDto dto, List<VehicleLineDto> vehicles);
    Task<List<RowData>> LookupAsync(string term);
    Task<RowData?> CheckDuplicateAsync(string? regNo, string? mobile, int customerId);   // PlateOwner, MobileOwner
}

public interface IVehicleRepository
{
    Task<List<RowData>> ListAsync(string? search, int? customerId);
    Task<ResultBag> GetAsync(int id);                       // vehicle?, reminders, history
    Task<int> SaveAsync(VehicleSaveDto dto);
    Task<List<RowData>> RemindersAsync(string? status, int daysAhead);
    Task<int> SaveReminderAsync(ReminderSaveDto dto);
    Task SetReminderStatusAsync(int reminderId, string status);
}

public interface IAppointmentRepository
{
    Task<List<RowData>> ListAsync(DateTime from, DateTime to, string? status);
    Task<RowData?> GetAsync(int id);
    Task<int> SaveAsync(AppointmentSaveDto dto);
    Task SetStatusAsync(int id, string status);
    Task<int> CheckInAsync(int id, int? employeeId, int odometer, string? complaints);
}

public interface IJobCardRepository
{
    Task<List<RowData>> ListAsync(string? status, string? search);
    Task<ResultBag> GetAsync(int id);                       // job?, services, parts
    Task<int> SaveAsync(JobCardSaveDto dto);
    Task AddServiceAsync(int jobCardId, int serviceId, decimal charge);
    Task RemoveServiceAsync(int jobServiceId);
    Task IssuePartAsync(int jobCardId, int partId, decimal qty, decimal unitPrice);
    Task RemovePartAsync(int jobPartId);
    Task SetStatusAsync(int jobCardId, string status);
    Task<List<RowData>> ReadyToBillAsync();
}

public interface IInvoiceRepository
{
    Task<ResultBag> LoadFromJobAsync(int jobCardId);        // job?, items
    Task<ResultBag> GetAsync(int id);                       // invoice?, items, payments
    Task<List<RowData>> ListAsync(string? status, DateTime? from, DateTime? to, string? search, int? customerId);
    Task<int> SaveAsync(InvoiceSaveModel model);
    Task DeleteDraftAsync(int invoiceId);
    Task<RowData?> SettleAsync(SettleModel model);
    Task VoidAsync(int invoiceId, string reason);
}

public interface IPaymentRepository
{
    Task<List<RowData>> ListAsync(DateTime? from, DateTime? to, string? search);
    Task<ResultBag> GetAsync(int id);                       // payment?, lines
}

public interface ILedgerRepository
{
    Task<ResultBag> CustomerAsync(int customerId, DateTime from, DateTime to);   // summary?, rows
    Task AdjustCustomerAsync(int customerId, decimal debit, decimal credit, string notes);
    Task<ResultBag> SupplierAsync(int supplierId, DateTime from, DateTime to);   // summary?, rows
}

public interface IStockRepository
{
    Task<RowData?> SummaryAsync();
    Task<ResultBag> LedgerAsync(int partId, DateTime? from, DateTime? to);       // part?, rows
    Task AdjustAsync(int partId, decimal qty, string notes);
}

public interface IPurchasingRepository
{
    // purchase orders
    Task<List<RowData>> PoListAsync(string? status, int? supplierId);
    Task<ResultBag> PoGetAsync(int id);                     // po?, items
    Task<int> PoSaveAsync(PurchaseOrderSaveDto dto, List<PoItemDto> items, int userId);
    Task PoSetStatusAsync(int poId, string status, int userId);
    Task<List<RowData>> LowStockAsync();

    // goods received
    Task<List<RowData>> GrnListAsync(string? status, int? supplierId);
    Task<ResultBag> GrnGetAsync(int id);                    // grn?, items, returns
    Task<List<RowData>> GrnPendingFromPoAsync(int poId);
    Task<int> GrnSaveAsync(GrnSaveModel model);
    Task GrnPostAsync(int grnId);
    Task GrnDeleteAsync(int grnId);

    // returns (debit notes)
    Task<int> ReturnSaveAsync(int grnId, DateTime returnDate, string reason, List<ReturnItemModel> items);
    Task<List<RowData>> ReturnListAsync();
}

public interface ISupplierBillRepository
{
    Task<List<RowData>> ListAsync(int? supplierId, string? status);
    Task<RowData?> GetAsync(int billId);
    Task<List<RowData>> UnbilledGrnAsync(int? supplierId);
    Task<int> SaveAsync(int supplierId, int? grnId, string billNo, DateTime billDate, DateTime dueDate, decimal amount);
    Task<int> PayAsync(SupplierPaymentDto dto, int userId);
    Task<List<RowData>> PaymentsAsync(int? supplierId);
}

// ----- Added: reports + reprint -----

public interface IReportRepository
{
    /// <summary>Runs one sp_Rpt_* procedure with the given parameters.</summary>
    Task<List<RowData>> RunAsync(string procedure, IReadOnlyDictionary<string, object?> parameters);
}

public interface IPrintRepository
{
    Task<RowData?> LogAsync(string docType, int docId, int userId);              // CopyNo, PrintedAt, PrintedBy
    Task<List<RowData>> HistoryAsync(string docType, int docId);
    Task<ResultBag> DebitNoteAsync(int returnId);                               // note?, items
    Task<RowData?> SupplierPaymentAsync(int supPaymentId);
}
