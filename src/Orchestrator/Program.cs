using System.Collections.Concurrent;
using System.Net.Http.Json;
using Npgsql;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("erp", c => c.BaseAddress = new Uri(builder.Configuration["FAKE_ERP_URL"] ?? "http://localhost:8082"));
var app = builder.Build();
var proposals = new ConcurrentDictionary<string, Proposal>();
var eventsLog = new ConcurrentQueue<Audit>();
var cs = builder.Configuration.GetConnectionString("Audit");
async Task Log(Audit e) {
 eventsLog.Enqueue(e);
 if (string.IsNullOrWhiteSpace(cs)) return;
 await using var conn = new NpgsqlConnection(cs);
 await conn.OpenAsync();
 await using var cmd = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS audit_events(id text PRIMARY KEY, tenant_id text NOT NULL, actor_role text NOT NULL, action text NOT NULL, entity_id text NOT NULL, outcome text NOT NULL, occurred_at timestamptz NOT NULL); INSERT INTO audit_events VALUES(@id,@tenant,@role,@action,@entity,@outcome,@at) ON CONFLICT(id) DO NOTHING",conn);
 cmd.Parameters.AddWithValue("id",e.Id); cmd.Parameters.AddWithValue("tenant",e.TenantId); cmd.Parameters.AddWithValue("role",e.ActorRole); cmd.Parameters.AddWithValue("action",e.Action); cmd.Parameters.AddWithValue("entity",e.EntityId); cmd.Parameters.AddWithValue("outcome",e.Outcome); cmd.Parameters.AddWithValue("at",e.At);
 await cmd.ExecuteNonQueryAsync();
}
app.MapGet("/health", () => Results.Ok(new {status="ok",service="orchestrator",llmMode=builder.Configuration["LLM_MODE"]??"deterministic"}));
app.MapGet("/api/invoices", async (string? tenantId, IHttpClientFactory f) => {
 var xs = await f.CreateClient("erp").GetFromJsonAsync<List<Invoice>>("/api/invoices?tenantId="+Uri.EscapeDataString(tenantId ?? "tenant-north")) ?? [];
 return Results.Ok(xs.Select(x => new {x.Id,x.TenantId,x.Customer,x.DueDate,x.Amount,x.AmountPaid,x.Currency,x.Balance,x.DaysOverdue,isOverdue=x.Balance>0&&x.DaysOverdue>0}));
});
app.MapPost("/api/proposals/{invoiceId}", async (string invoiceId, ProposalRequest r, IHttpClientFactory f) => {
 var role=r.ActorRole??"Employee";
 if(role is not ("Employee" or "Approver" or "Admin")) return Results.Forbid();
 var erp=f.CreateClient("erp");
 var x=await erp.GetFromJsonAsync<Invoice>("/api/invoices/"+Uri.EscapeDataString(invoiceId));
 if(x is null||x.TenantId!=r.TenantId) return Results.NotFound();
 if(x.Balance<=0||x.DaysOverdue<=0) return Results.BadRequest(new {error="Invoice is not overdue with an outstanding balance."});
 var p=new Proposal(Guid.NewGuid().ToString("N"),x.Id,x.TenantId,x.Customer,"Payment reminder: "+x.Id,$"Hello {x.Customer}, our records show an outstanding balance of {x.Balance:0.00} {x.Currency} for invoice {x.Id}. If you have already paid, please send us the payment reference so we can reconcile it. Kind regards, Accounts Receivable","PendingApproval",DateTimeOffset.UtcNow,role);
 proposals[p.Id]=p; await Log(new Audit(Guid.NewGuid().ToString("N"),p.TenantId,role,"ProposalCreated",p.InvoiceId,"PendingApproval",DateTimeOffset.UtcNow)); return Results.Ok(p);
});
app.MapGet("/api/proposals", (string? tenantId) => Results.Ok(proposals.Values.Where(p=>tenantId==null||p.TenantId==tenantId)));
app.MapPost("/api/proposals/{id}/decision", async (string id, DecisionRequest r) => {
 if(r.ActorRole is not ("Approver" or "Admin")) return Results.Problem("Only Approver or Admin can decide.",statusCode:403);
 if(!proposals.TryGetValue(id,out var p)||p.TenantId!=r.TenantId) return Results.NotFound();
 if(p.Status!="PendingApproval") return Results.Conflict(new {error="Proposal already decided."});
 var next=p with {Status=r.Approve?"Approved":"Rejected"}; proposals[id]=next;
 await Log(new Audit(Guid.NewGuid().ToString("N"),p.TenantId,r.ActorRole,r.Approve?"ProposalApproved":"ProposalRejected",p.InvoiceId,next.Status,DateTimeOffset.UtcNow)); return Results.Ok(next);
});
app.MapPost("/api/proposals/{id}/execute", async (string id, ExecuteRequest r, IHttpClientFactory f) => {
 if(r.ActorRole is not ("Approver" or "Admin")) return Results.Problem("Only Approver or Admin can execute.",statusCode:403);
 if(!proposals.TryGetValue(id,out var p)||p.TenantId!=r.TenantId) return Results.NotFound();
 if(p.Status!="Approved") return Results.Conflict(new {error="Approval required before execution."});
 var response=await f.CreateClient("erp").PostAsJsonAsync("/api/reminders",new {invoiceId=p.InvoiceId,idempotencyKey=p.Id,to="billing@example.test",subject=p.Subject,body=p.Body});
 if(!response.IsSuccessStatusCode) return Results.Problem("Fake ERP execution failed.",statusCode:502);
 var next=p with {Status="Executed"}; proposals[id]=next;
 await Log(new Audit(Guid.NewGuid().ToString("N"),p.TenantId,r.ActorRole,"ReminderExecuted",p.InvoiceId,"SIMULATED_ONLY",DateTimeOffset.UtcNow));
 return Results.Ok(new {proposal=next,execution="SIMULATED_ONLY",idempotencyKey=p.Id});
});
app.MapGet("/api/audit", (string? tenantId) => Results.Ok(eventsLog.Where(x=>tenantId==null||x.TenantId==tenantId).OrderByDescending(x=>x.At)));
app.MapPost("/api/chat", (ChatRequest r) => Results.Ok(new {response="I can help identify overdue invoices and draft follow-ups. An Approver must approve before the fake ERP records a simulated send.",mode=builder.Configuration["LLM_MODE"]??"deterministic",modelCanAuthorizeActions=false}));
app.Run();
record Invoice(string Id,string TenantId,string Customer,string DueDate,decimal Amount,decimal AmountPaid,string Currency) { public decimal Balance=>Amount-AmountPaid; public int DaysOverdue=>Math.Max(0,(int)(DateTime.UtcNow.Date-DateTime.Parse(DueDate).Date).TotalDays); }
record ProposalRequest(string TenantId,string? ActorRole);
record DecisionRequest(string TenantId,string ActorRole,bool Approve);
record ExecuteRequest(string TenantId,string ActorRole);
record ChatRequest(string Message);
record Proposal(string Id,string InvoiceId,string TenantId,string Customer,string Subject,string Body,string Status,DateTimeOffset CreatedAt,string CreatedByRole);
record Audit(string Id,string TenantId,string ActorRole,string Action,string EntityId,string Outcome,DateTimeOffset At);
