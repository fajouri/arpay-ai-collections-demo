using System.Collections.Concurrent;
var app = WebApplication.CreateBuilder(args).Build();
var invoices = new ConcurrentDictionary<string, Invoice>();
foreach (var x in new[] {
 new Invoice("INV-1001","tenant-north","Northwind Studio","2026-08-10",1250m,0m,"EUR"),
 new Invoice("INV-1002","tenant-north","Contoso Design","2026-09-02",840m,300m,"EUR"),
 new Invoice("INV-1003","tenant-north","Fabrikam Services","2026-10-01",2100m,0m,"EUR"),
 new Invoice("INV-2001","tenant-south","Tailspin Retail","2026-08-14",560m,0m,"EUR"),
 new Invoice("INV-2002","tenant-south","Adventure Works","2026-10-05",990m,990m,"EUR")
}) invoices[x.Id] = x;
var reminders = new ConcurrentDictionary<string, Reminder>();
app.MapGet("/health", () => Results.Ok(new { status="ok", service="fake-erp" }));
app.MapGet("/api/invoices", (string? tenantId) => Results.Ok(invoices.Values.Where(x => tenantId == null || x.TenantId == tenantId)));
app.MapGet("/api/invoices/{id}", (string id) => invoices.TryGetValue(id, out var x) ? Results.Ok(x) : Results.NotFound());
app.MapGet("/api/invoices/{id}/communications", (string id) => invoices.ContainsKey(id)
 ? Results.Ok(new[] { new { date="2026-10-05", channel="Email", direction="Outbound", summary="Reminder sent; customer requested a copy." }, new { date="2026-10-08", channel="Email", direction="Inbound", summary="Customer asks us to verify whether payment arrived." } })
 : Results.NotFound());
app.MapPost("/api/reminders", (ReminderRequest r) => {
 if (!invoices.TryGetValue(r.InvoiceId, out var invoice)) return Results.NotFound();
 var item = reminders.GetOrAdd(r.IdempotencyKey, _ => new Reminder(r.IdempotencyKey, invoice.Id, invoice.TenantId, r.To, r.Subject, r.Body, "Simulated", DateTimeOffset.UtcNow));
 return Results.Ok(item);
});
app.MapGet("/api/reminders", () => Results.Ok(reminders.Values));
app.Run();
record Invoice(string Id,string TenantId,string Customer,string DueDate,decimal Amount,decimal AmountPaid,string Currency) {
 public decimal Balance => Amount - AmountPaid;
 public int DaysOverdue => Math.Max(0,(int)(DateTime.UtcNow.Date-DateTime.Parse(DueDate).Date).TotalDays);
}
record ReminderRequest(string InvoiceId,string IdempotencyKey,string To,string Subject,string Body);
record Reminder(string Id,string InvoiceId,string TenantId,string To,string Subject,string Body,string Status,DateTimeOffset CreatedAt);
