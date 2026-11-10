namespace MotoCare.Domain.Constants;

public static class JobCardStatus
{
    public const string Open = "Open";
    public const string InProgress = "In progress";
    public const string Completed = "Completed";
    public const string Invoiced = "Invoiced";
    public const string Cancelled = "Cancelled";
}

public static class InvoiceStatus
{
    public const string Draft = "Draft";
    public const string Paid = "Paid";
    public const string Partial = "Partial";
    public const string Credit = "Credit";
    public const string Void = "Void";
}

public static class CustomerIdentifyBy
{
    public const string Plate = "PLATE";
    public const string Name = "NAME";
}

public static class PurchaseOrderAction
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Close = "CLOSE";
}
