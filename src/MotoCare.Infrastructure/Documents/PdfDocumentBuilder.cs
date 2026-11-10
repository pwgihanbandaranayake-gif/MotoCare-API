using MotoCare.Application.Abstractions;
using MotoCare.Application.Common;
using MotoCare.Application.Dtos;
using MotoCare.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotoCare.Infrastructure.Documents;

/// <summary>
/// Added: PDF versions of the invoice, payslip and quotation (for e-mail / WhatsApp and the "Download PDF" button).
/// Uses QuestPDF (free Community licence for businesses under USD 1M yearly revenue).
/// The logo is read from Assets/logo.png next to the API.
/// </summary>
public sealed class PdfDocumentBuilder(IInvoiceRepository invoices, IPayrollRepository payroll, IQuotationRepository quotations,
                                       ISettingsRepository settings) : IDocumentPdfBuilder
{
    static PdfDocumentBuilder() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly string LogoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo.png");
    private const string Accent = "#EA580C";

    public async Task<PdfFile> BuildAsync(string docType, int docId)
    {
        var s = await SettingsReader.ReadAsync(settings);
        var company = new Company(s.Text("CompanyName", "Moto GT"), s.Text("CompanyAddress"), s.Text("CompanyPhone"), s.Text("CompanyEmail"),
                                  File.Exists(LogoPath) ? await File.ReadAllBytesAsync(LogoPath) : null);

        switch (docType)
        {
            case "INVOICE":
            {
                var bag = await invoices.GetAsync(docId);
                var i = Check.Found(bag.Row("invoice"), "Invoice");
                string no = i.Get<string>("InvoiceNo");
                return new PdfFile($"Invoice-{no}.pdf", Invoice(company, i, bag.Rows("items")), $"Invoice {no}");
            }
            case "QUOTATION":
            {
                var bag = await quotations.GetAsync(docId);
                var q = Check.Found(bag.Row("quotation"), "Quotation");
                string no = q.Get<string>("QuotationNo");
                return new PdfFile($"Quotation-{no}.pdf", Quotation(company, q, bag.Rows("items"), s.Text("QuotationTerms")), $"Quotation {no}");
            }
            case "PAYSLIP":
            {
                var bag = await payroll.PayslipAsync(docId);
                var p = Check.Found(bag.Row("slip"), "Payslip");
                string no = $"{p.Get<string>("RunNo")}-{p.Get<string>("EmployeeNo")}";
                return new PdfFile($"Payslip-{no}.pdf", Payslip(company, p, bag.Rows("details"), bag.Rows("extras"), bag.Rows("advances")), $"Payslip {no}");
            }
            default:
                throw new KeyNotFoundException($"Cannot build a PDF for '{docType}'.");
        }
    }

    // ================================================================= documents
    private static byte[] Invoice(Company c, RowData i, List<RowData> items) => Page(c, "INVOICE", col =>
    {
        Info(col, ("Invoice no", i.Get<string>("InvoiceNo")), ("Date", D(i, "InvoiceDate")), ("Customer", i.Get<string>("CustomerName")),
                  ("Vehicle", i.Get<string?>("RegNo") ?? "Direct sale"), ("Job card", i.Get<string?>("JobNo") ?? "-"), ("Status", i.Get<string>("PayStatus")),
                  // Added: odometer and next service
                  ("Odometer", Km(i, "Odometer")), ("Next service", Km(i, "NextServiceKm")));
        Lines(col, items);
        Totals(col, ("Sub total", M(i, "SubTotal")), ("Discount", M(i.Get<decimal>("LineDiscount") + i.Get<decimal>("BillDiscountAmt"))),
                    ("Tax", M(i, "TaxAmt")), ("Net total", M(i, "NetTotal")), ("Paid", M(i, "PaidAmount")), ("Balance", M(i, "Balance")));
        col.Item().PaddingTop(20).AlignCenter().Text("Thank you for your business!").Italic();
    });

    private static byte[] Quotation(Company c, RowData q, List<RowData> items, string terms) => Page(c, "QUOTATION", col =>
    {
        Info(col, ("Quotation no", q.Get<string>("QuotationNo")), ("Date", D(q, "QuoteDate")), ("Customer", q.Get<string>("CustomerName")),
                  ("Valid until", D(q, "ValidUntil")), ("Mobile", q.Get<string?>("Mobile") ?? "-"), ("Vehicle", q.Get<string?>("VehicleText") ?? "-"));
        Lines(col, items);
        Totals(col, ("Sub total", M(q, "SubTotal")), ("Discount", M(q.Get<decimal>("LineDiscount") + q.Get<decimal>("BillDiscountAmt"))),
                    ("Tax", M(q, "TaxAmt")), ("Total", M(q, "NetTotal")));
        if (!string.IsNullOrWhiteSpace(terms))
            col.Item().PaddingTop(14).Column(t => { t.Item().Text("Terms & conditions").Bold(); t.Item().Text(terms).FontSize(9); });
    });

    private static byte[] Payslip(Company c, RowData p, List<RowData> details, List<RowData> extras, List<RowData> advances) => Page(c, "PAYSLIP", col =>
    {
        Info(col, ("Employee", p.Get<string>("FullName")), ("Employee no", p.Get<string>("EmployeeNo")),
                  ("Designation", p.Get<string?>("DesignationName") ?? "-"), ("Period", $"{D(p, "FromDate")} - {D(p, "ToDate")}"),
                  ("EPF no", p.Get<string?>("EpfNo") ?? "-"), ("Bank", $"{p.Get<string?>("BankName")} {p.Get<string?>("BankAccountNo")}".Trim()));

        col.Item().PaddingTop(8).Text("Earnings").Bold().FontColor(Accent);
        var earnings = new List<(string, string)> { ("Basic salary", M(p, "Basic")) };
        earnings.AddRange(details.Where(d => d.Get<string>("DetailType") == "COMMISSION")
            .Select(d => ($"Commission - {d.Get<string>("Description")} ({M(d, "BaseAmount")} x {d.Get<decimal>("Pct"):0.##}%)", M(d, "Amount"))));
        earnings.AddRange(details.Where(d => d.Get<string>("DetailType") == "ALLOWANCE").Select(d => (d.Get<string>("Description"), M(d, "Amount"))));
        earnings.AddRange(extras.Where(x => !x.Get<bool>("IsDeduction")).Select(x => (x.Get<string>("Description"), M(x, "Amount"))));
        Rows(col, earnings);

        col.Item().PaddingTop(8).Text("Deductions").Bold().FontColor(Accent);
        var deductions = new List<(string, string)>();
        if (p.Get<bool>("EpfEnabled")) deductions.Add(($"EPF employee ({p.Get<decimal>("EpfEmployeePct"):0.##}%)", M(p, "EpfEmployee")));
        deductions.AddRange(extras.Where(x => x.Get<bool>("IsDeduction")).Select(x => (x.Get<string>("Description"), M(x, "Amount"))));
        // Added: advances / expenses taken
        deductions.AddRange(advances.Select(a => ($"{a.Get<string>("AdvanceType")} {D(a, "AdvanceDate")} - {a.Get<string>("Description")}", M(a, "Amount"))));
        if (deductions.Count == 0) deductions.Add(("None", M(0)));
        Rows(col, deductions);

        Totals(col, ("Gross pay", M(p, "Gross")), ("Total deductions", M(p.Get<decimal>("EpfEmployee") + p.Get<decimal>("Deductions"))), ("NET PAY", M(p, "NetPay")));
        if (p.Get<bool>("EpfEnabled"))
            col.Item().PaddingTop(6).Text($"Employer contributions: EPF {M(p, "EpfEmployer")} · ETF {M(p, "Etf")}").FontSize(9).FontColor(Colors.Grey.Darken1);
    });

    // ================================================================= building blocks
    private sealed record Company(string Name, string Address, string Phone, string Email, byte[]? Logo);

    /// <summary>A4 page with the company header (logo, name, contact) and the document title.</summary>
    private static byte[] Page(Company c, string title, Action<ColumnDescriptor> body) =>
        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().BorderBottom(2).BorderColor(Accent).PaddingBottom(8).Row(row =>
            {
                if (c.Logo is not null) row.ConstantItem(80).Height(70).Image(c.Logo);
                row.RelativeItem().PaddingLeft(10).Column(col =>
                {
                    col.Item().Text(c.Name).FontSize(20).Bold();
                    if (c.Address != "") col.Item().Text(c.Address);
                    if (c.Phone != "") col.Item().Text($"Tel: {c.Phone}");
                    if (c.Email != "") col.Item().Text(c.Email);
                });
                row.ConstantItem(160).AlignRight().AlignMiddle().Text(title).FontSize(22).Bold().FontColor(Accent);
            });

            page.Content().PaddingVertical(12).Column(col => { col.Spacing(6); body(col); });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span($"{c.Name} · Generated {DateTime.Now:dd MMM yyyy HH:mm} · Page ").FontSize(8);
                t.CurrentPageNumber().FontSize(8);
            });
        })).GeneratePdf();

    /// <summary>Two-column key / value block.</summary>
    private static void Info(ColumnDescriptor col, params (string Label, string Value)[] items) =>
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(2); });
            foreach (var (label, value) in items)
            {
                t.Cell().PaddingVertical(2).Text(label).FontColor(Colors.Grey.Darken1);
                t.Cell().PaddingVertical(2).Text(value).Bold();
            }
        });

    /// <summary>Item table (invoice / quotation).</summary>
    private static void Lines(ColumnDescriptor col, List<RowData> items) =>
        col.Item().PaddingTop(8).Table(t =>
        {
            t.ColumnsDefinition(c => { c.ConstantColumn(24); c.RelativeColumn(5); c.RelativeColumn(); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
            t.Header(h =>
            {
                foreach (var title in new[] { "#", "Description", "Qty", "Unit price", "Disc.", "Amount" })
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(title).Bold();
            });
            int n = 1;
            foreach (var r in items)
            {
                t.Cell().Element(Cell).Text($"{n++}");
                t.Cell().Element(Cell).Text(r.Get<string>("Description"));
                t.Cell().Element(Cell).AlignRight().Text($"{r.Get<decimal>("Qty"):0.##}");
                t.Cell().Element(Cell).AlignRight().Text(M(r, "UnitPrice"));
                t.Cell().Element(Cell).AlignRight().Text(r.Get<decimal>("Discount") == 0 ? "" : M(r, "Discount"));
                t.Cell().Element(Cell).AlignRight().Text(M(r, "LineTotal"));
            }
        });

    /// <summary>Description / amount rows (payslip).</summary>
    private static void Rows(ColumnDescriptor col, List<(string Label, string Amount)> rows) =>
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(); });
            foreach (var (label, amount) in rows)
            {
                t.Cell().Element(Cell).Text(label);
                t.Cell().Element(Cell).AlignRight().Text(amount);
            }
        });

    /// <summary>Right-aligned totals; the last line is bold.</summary>
    private static void Totals(ColumnDescriptor col, params (string Label, string Value)[] rows) =>
        col.Item().AlignRight().Width(240).PaddingTop(6).Column(c =>
        {
            for (int k = 0; k < rows.Length; k++)
            {
                var (label, value) = rows[k];
                bool last = k == rows.Length - 1;
                c.Item().Row(r =>
                {
                    var l = r.RelativeItem().Text(label);
                    var v = r.RelativeItem().AlignRight().Text(value);
                    if (last) { l.Bold().FontSize(12); v.Bold().FontSize(12); }
                });
            }
        });

    private static IContainer Cell(IContainer c) => c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4);
    private static string M(decimal v) => $"Rs {v:N2}";
    private static string M(RowData r, string col) => M(r.Get<decimal>(col));
    private static string Km(RowData r, string col) => r.Get<int?>(col) is int km ? $"{km:N0} km" : "-";   // Added
    private static string D(RowData r, string col) => r.Get<DateTime?>(col)?.ToString("dd MMM yyyy") ?? "-";
}
