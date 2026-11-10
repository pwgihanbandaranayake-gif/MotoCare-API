namespace MotoCare.Application.Reports;

// =====================================================================
// Report catalogue - one entry per report in the Reports section.
// To add a report: write a sp_Rpt_* procedure (06_reports_and_reprint.sql)
// and add one line here. The React Reports screen builds its filters and
// table columns from this list, so no frontend change is needed.
// =====================================================================

/// <summary>Column display format used by the React table / print.</summary>
public enum ColumnFormat { Text, Date, Money, Qty, Int, Pct, Status }

/// <summary>One column of a report. Name must match the column name returned by the procedure.</summary>
public sealed record ReportColumn(string Name, string Label, ColumnFormat Format = ColumnFormat.Text, bool Total = false);

/// <summary>Optional filters a report accepts (dates are always shown).</summary>
[Flags]
public enum ReportFilter { None = 0, Customer = 1, Supplier = 2, Top = 4, AsAtDate = 8, NoDates = 16 }

public sealed record ReportDefinition(
    string Key, string Title, string Group, string Description, string Procedure,
    ReportFilter Filters, IReadOnlyList<ReportColumn> Columns);

public static class ReportCatalog
{
    // short helpers to keep the list readable
    private static ReportColumn T(string n, string l) => new(n, l);
    private static ReportColumn D(string n, string l) => new(n, l, ColumnFormat.Date);
    private static ReportColumn M(string n, string l) => new(n, l, ColumnFormat.Money, true);
    private static ReportColumn Q(string n, string l) => new(n, l, ColumnFormat.Qty, true);
    private static ReportColumn I(string n, string l) => new(n, l, ColumnFormat.Int, true);
    private static ReportColumn P(string n, string l) => new(n, l, ColumnFormat.Pct);
    private static ReportColumn S(string n, string l) => new(n, l, ColumnFormat.Status);

    public static readonly IReadOnlyList<ReportDefinition> All = new List<ReportDefinition>
    {
        // ---------------- Sales
        new("sales-daily", "Daily sales summary", "Sales", "Invoices, discounts, tax, collections and credit per day.",
            "dbo.sp_Rpt_SalesDaily", ReportFilter.None,
            new[] { D("Date", "Date"), I("Invoices", "Invoices"), M("GrossSales", "Gross sales"), M("Discount", "Discount"),
                    M("Tax", "Tax"), M("NetSales", "Net sales"), M("Collected", "Collected"), M("OnCredit", "On credit") }),

        new("invoice-register", "Invoice report", "Sales", "Every invoice in the period with totals and status.",
            "dbo.sp_Rpt_InvoiceRegister", ReportFilter.Customer,
            new[] { T("InvoiceNo", "Invoice"), D("InvoiceDate", "Date"), T("JobNo", "Job card"), T("Customer", "Customer"),
                    T("RegNo", "Vehicle"), M("SubTotal", "Sub total"), M("Discount", "Discount"), M("Tax", "Tax"),
                    M("NetTotal", "Net total"), M("Paid", "Paid"), M("Balance", "Balance"), S("Status", "Status") }),

        new("payment-methods", "Collections by payment method", "Sales", "Money received per payment method (cash, card, transfer...).",
            "dbo.sp_Rpt_PaymentMethods", ReportFilter.None,
            new[] { T("PaymentMethod", "Payment method"), I("Transactions", "Transactions"), M("Amount", "Amount") }),

        new("sales-by-customer", "Sales by customer", "Sales", "Net sales, payments and balance for each customer.",
            "dbo.sp_Rpt_SalesByCustomer", ReportFilter.None,
            new[] { T("Customer", "Customer"), T("Mobile", "Mobile"), I("Invoices", "Invoices"), M("NetSales", "Net sales"),
                    M("Paid", "Paid"), M("Balance", "Balance") }),

        new("service-sales", "Service (labour) sales", "Sales", "How often each service was billed and its revenue.",
            "dbo.sp_Rpt_ServiceSales", ReportFilter.None,
            new[] { T("Service", "Service"), Q("TimesDone", "Times done"), M("Revenue", "Revenue") }),

        // ---------------- Profit
        new("profit-by-invoice", "Profit report (by invoice)", "Profit", "Sales excluding tax minus parts cost (current cost price).",
            "dbo.sp_Rpt_ProfitByInvoice", ReportFilter.None,
            new[] { T("InvoiceNo", "Invoice"), D("InvoiceDate", "Date"), T("Customer", "Customer"), M("SalesExTax", "Sales (ex tax)"),
                    M("PartsCost", "Parts cost"), M("GrossProfit", "Gross profit"), P("MarginPct", "Margin %") }),

        new("part-profit", "Profit by part", "Profit", "Quantity sold, revenue, cost and profit for each part.",
            "dbo.sp_Rpt_PartProfit", ReportFilter.None,
            new[] { T("PartNo", "Part no"), T("Description", "Description"), Q("QtySold", "Qty sold"), M("Revenue", "Revenue"),
                    M("Cost", "Cost"), M("Profit", "Profit") }),

        // ---------------- Stock
        new("fast-moving", "Fast moving items", "Stock", "Parts used most through job cards and sales.",
            "dbo.sp_Rpt_FastMoving", ReportFilter.Top,
            new[] { T("PartNo", "Part no"), T("Description", "Description"), Q("QtyMoved", "Qty moved"), I("Movements", "Movements"),
                    Q("InStock", "In stock"), new ReportColumn("ReorderLevel", "Reorder level", ColumnFormat.Qty) }),

        new("slow-moving", "Slow / non-moving items", "Stock", "Parts in stock that did not move in the period.",
            "dbo.sp_Rpt_SlowMoving", ReportFilter.None,
            new[] { T("PartNo", "Part no"), T("Description", "Description"), Q("InStock", "In stock"),
                    new ReportColumn("CostPrice", "Cost", ColumnFormat.Money), M("StockValue", "Stock value"), D("LastMovement", "Last movement") }),

        new("stock-valuation", "Stock valuation", "Stock", "Current stock at cost and selling price, by category.",
            "dbo.sp_Rpt_StockValuation", ReportFilter.NoDates,
            new[] { T("Category", "Category"), T("PartNo", "Part no"), T("Description", "Description"), Q("InStock", "In stock"),
                    new ReportColumn("CostPrice", "Cost", ColumnFormat.Money), new ReportColumn("SellPrice", "Sell", ColumnFormat.Money),
                    M("CostValue", "Cost value"), M("SellValue", "Sell value") }),

        new("reorder", "Reorder list", "Stock", "Parts at or below reorder level with a suggested order quantity.",
            "dbo.sp_Rpt_Reorder", ReportFilter.NoDates,
            new[] { T("PartNo", "Part no"), T("Description", "Description"), Q("InStock", "In stock"),
                    new ReportColumn("ReorderLevel", "Reorder level", ColumnFormat.Qty), Q("SuggestedQty", "Suggested qty"),
                    new ReportColumn("CostPrice", "Cost", ColumnFormat.Money), M("EstimatedCost", "Estimated cost") }),

        // ---------------- Customers & workshop
        new("customer-outstanding", "Customer outstanding (aging)", "Customers", "Unpaid invoice balances by age as at the 'To' date.",
            "dbo.sp_Rpt_CustomerOutstanding", ReportFilter.AsAtDate,
            new[] { T("Customer", "Customer"), T("Mobile", "Mobile"), new ReportColumn("CreditLimit", "Credit limit", ColumnFormat.Money),
                    M("Days0To30", "0-30 days"), M("Days31To60", "31-60 days"), M("Days61To90", "61-90 days"), M("Over90", "Over 90"),
                    M("TotalDue", "Total due") }),

        new("service-due", "Service due list", "Customers", "Vehicles due for a service in the period (for SMS / calls).",
            "dbo.sp_Rpt_ServiceDue", ReportFilter.None,
            new[] { D("DueDate", "Due date"), T("RegNo", "Vehicle"), T("Customer", "Customer"), T("Mobile", "Mobile"),
                    new ReportColumn("DueKm", "Due km", ColumnFormat.Int), new ReportColumn("LastOdometer", "Last odometer", ColumnFormat.Int),
                    S("Status", "Status") }),

        new("mechanic-performance", "Mechanic performance", "Workshop", "Jobs completed and labour / parts value per mechanic.",
            "dbo.sp_Rpt_MechanicPerformance", ReportFilter.None,
            new[] { T("Mechanic", "Mechanic"), I("JobsCompleted", "Jobs completed"), M("LabourValue", "Labour value"), M("PartsValue", "Parts value") }),

        new("jobcard-register", "Job card register", "Workshop", "All job cards opened in the period with status and invoice.",
            "dbo.sp_Rpt_JobCardRegister", ReportFilter.None,
            new[] { T("JobNo", "Job no"), D("DateIn", "Date in"), T("RegNo", "Vehicle"), T("Customer", "Customer"),
                    T("Mechanic", "Mechanic"), S("Status", "Status"), D("Completed", "Completed"), T("InvoiceNo", "Invoice") }),

        // ---------------- Purchasing
        new("grn-register", "GRN report", "Purchasing", "Goods received in the period with accepted value.",
            "dbo.sp_Rpt_GrnRegister", ReportFilter.Supplier,
            new[] { T("GRNNo", "GRN"), D("GRNDate", "Date"), T("PONo", "PO"), T("Supplier", "Supplier"), S("Status", "Status"),
                    Q("QtyReceived", "Qty received"), Q("QtyRejected", "Qty rejected"), M("AcceptedValue", "Accepted value") }),

        new("supplier-summary", "Supplier report", "Purchasing", "Purchases, returns, bills, payments and current balance per supplier.",
            "dbo.sp_Rpt_SupplierSummary", ReportFilter.None,
            new[] { T("Supplier", "Supplier"), M("Purchases", "Purchases"), M("Returns", "Returns"), M("Billed", "Billed"),
                    M("Paid", "Paid"), M("BalanceNow", "Balance now") }),

        new("supplier-outstanding", "Supplier outstanding (aging)", "Purchasing", "Unpaid supplier bills by days past due, as at the 'To' date.",
            "dbo.sp_Rpt_SupplierOutstanding", ReportFilter.AsAtDate,
            new[] { T("Supplier", "Supplier"), M("NotDue", "Not due"), M("Overdue1To30", "1-30 overdue"), M("Overdue31To60", "31-60 overdue"),
                    M("Over60", "Over 60"), M("TotalPayable", "Total payable") }),

        new("po-status", "Purchase order status", "Purchasing", "Ordered vs received quantities per purchase order.",
            "dbo.sp_Rpt_PoStatus", ReportFilter.Supplier,
            new[] { T("PONo", "PO"), D("PODate", "Date"), T("Supplier", "Supplier"), S("Status", "Status"), M("Total", "PO total"),
                    Q("QtyOrdered", "Ordered"), Q("QtyReceived", "Received"), Q("QtyPending", "Pending") }),

        // ---------------- Added: quotations, stock count
        new("quotation-register", "Quotation report", "Sales", "Quotations given, their value and whether they became invoices.",
            "dbo.sp_Rpt_QuotationRegister", ReportFilter.None,
            new[] { T("QuotationNo", "Quotation"), D("QuoteDate", "Date"), D("ValidUntil", "Valid until"), T("Customer", "Customer"),
                    T("Vehicle", "Vehicle"), M("NetTotal", "Total"), S("Status", "Status"), T("InvoiceNo", "Invoice") }),

        new("stock-count-variance", "Stock count variance", "Stock", "Differences found in stock counts (counted vs system).",
            "dbo.sp_Rpt_StockCountVariance", ReportFilter.None,
            new[] { T("CountNo", "Count"), D("CountDate", "Date"), S("Status", "Status"), T("PartNo", "Part no"), T("Description", "Description"),
                    new ReportColumn("SystemQty", "System qty", ColumnFormat.Qty), new ReportColumn("CountedQty", "Counted", ColumnFormat.Qty),
                    Q("Variance", "Variance"), M("VarianceValue", "Variance value") }),

        // ---------------- Added: payroll (also needs Payroll > View permission)
        new("payroll-summary", "Payroll summary", "Payroll", "Salary lines of payroll runs ending in the period.",
            "dbo.sp_Rpt_PayrollSummary", ReportFilter.None,
            new[] { T("RunNo", "Run"), D("ToDate", "Period end"), S("Status", "Status"), T("EmployeeNo", "Emp no"), T("Employee", "Employee"),
                    M("Basic", "Basic"), M("Commission", "Commission"), M("Allowances", "Allowances"), M("Gross", "Gross"),
                    M("EpfEmployee", "EPF 8%"), M("Deductions", "Deductions"), M("NetPay", "Net pay"), M("EpfEmployer", "EPF 12%"), M("Etf", "ETF 3%") }),

        new("commission", "Commission report", "Payroll", "Commission earned per employee and service from completed jobs (current %).",
            "dbo.sp_Rpt_Commission", ReportFilter.None,
            new[] { T("Employee", "Employee"), T("Service", "Service"), I("Jobs", "Jobs"), M("ServiceAmount", "Service value"),
                    P("CommissionPct", "Commission %"), M("Commission", "Commission") }),

        new("epf-etf", "EPF / ETF report", "Payroll", "EPF (employee + employer) and ETF per employee from approved runs.",
            "dbo.sp_Rpt_EpfEtf", ReportFilter.None,
            new[] { T("EpfNo", "EPF no"), T("NIC", "NIC"), T("Employee", "Employee"), M("EpfBase", "EPF base"), M("EpfEmployee", "Employee 8%"),
                    M("EpfEmployer", "Employer 12%"), M("EpfTotal", "EPF total"), M("Etf", "ETF 3%") }),

        new("employee-advances", "Advances & expenses", "Payroll", "Money taken by employees and whether it was deducted from salary.",
            "dbo.sp_Rpt_EmployeeAdvances", ReportFilter.None,
            new[] { D("AdvanceDate", "Date"), T("EmployeeNo", "Emp no"), T("Employee", "Employee"), T("Type", "Type"), T("Description", "Description"),
                    M("Amount", "Amount"), S("Status", "Status"), T("RunNo", "Payroll run") }),

        new("employee-list", "Employee list", "Payroll", "All employees with designation, join date and basic salary.",
            "dbo.sp_Rpt_EmployeeList", ReportFilter.NoDates,
            new[] { T("EmployeeNo", "Emp no"), T("Employee", "Employee"), T("NIC", "NIC"), T("Designation", "Designation"),
                    D("JoinDate", "Joined"), T("Mobile", "Mobile"), M("BasicSalary", "Basic"), T("OnPayroll", "On payroll"), S("Status", "Status") }),

        // ---------------- Audit
        new("discount-void-audit", "Discounts & voids audit", "Audit", "Invoices with a discount or that were voided, and who created them.",
            "dbo.sp_Rpt_DiscountVoidAudit", ReportFilter.None,
            new[] { T("InvoiceNo", "Invoice"), D("InvoiceDate", "Date"), T("Customer", "Customer"), T("CreatedBy", "Created by"),
                    M("SubTotal", "Sub total"), M("Discount", "Discount"), P("DiscountPct", "Discount %"), S("Status", "Status"),
                    T("VoidReason", "Void reason") }),
    };

    public static ReportDefinition? Find(string key) =>
        All.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));
}
