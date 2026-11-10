namespace MotoCare.Domain.Constants;

/// <summary>Module keys - must match dbo.Modules.ModuleKey.</summary>
public static class AppModules
{
    public const string Dashboard = "Dashboard";
    public const string Appointments = "Appointments";
    public const string JobCards = "JobCards";
    public const string Invoices = "Invoices";
    public const string Payments = "Payments";
    public const string Customers = "Customers";
    public const string Vehicles = "Vehicles";
    public const string Parts = "Parts";
    public const string Suppliers = "Suppliers";
    public const string PurchaseOrders = "PurchaseOrders";
    public const string Grn = "GRN";
    public const string SupplierBills = "SupplierBills";
    public const string Ledgers = "Ledgers";
    public const string Masters = "Masters";
    public const string Users = "Users";
    public const string Settings = "Settings";
    public const string Reports = "Reports";        // Added: reports section
    public const string Quotations = "Quotations";  // Added
    public const string Payroll = "Payroll";        // Added: employees, salary setup, payroll runs
    public const string Messages = "Messages";      // Added: e-mail / WhatsApp outbox
}

public static class PermissionActions
{
    public const string View = "View";
    public const string Add = "Add";
    public const string Edit = "Edit";
    public const string Delete = "Delete";
    public const string Approve = "Approve";
}
