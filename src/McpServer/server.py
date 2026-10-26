import os
import httpx
from mcp.server.fastmcp import FastMCP
ERP = os.getenv("FAKE_ERP_URL", "http://localhost:8082")
mcp = FastMCP("collections-tools", host="0.0.0.0", port=int(os.getenv("MCP_PORT", "8080")))
@mcp.tool()
async def list_invoices(tenant_id: str = "tenant-north") -> list[dict]:
    """List synthetic invoices from the fake ERP."""
    async with httpx.AsyncClient(timeout=10) as client:
        r = await client.get(f"{ERP}/api/invoices", params={"tenantId": tenant_id})
        r.raise_for_status()
        return r.json()
@mcp.tool()
async def get_invoice_communications(invoice_id: str) -> list[dict]:
    """Read synthetic communication history for an invoice."""
    async with httpx.AsyncClient(timeout=10) as client:
        r = await client.get(f"{ERP}/api/invoices/{invoice_id}/communications")
        r.raise_for_status()
        return r.json()
@mcp.tool()
async def simulate_reminder(invoice_id: str, idempotency_key: str, to: str, subject: str, body: str) -> dict:
    """Record a simulated reminder in the fake ERP; never sends real email."""
    async with httpx.AsyncClient(timeout=10) as client:
        r = await client.post(f"{ERP}/api/reminders", json={"invoiceId":invoice_id,"idempotencyKey":idempotency_key,"to":to,"subject":subject,"body":body})
        r.raise_for_status()
        return r.json()
@mcp.custom_route("/health", methods=["GET"])
async def health(request):
    from starlette.responses import JSONResponse
    return JSONResponse({"status":"ok","service":"mcp-server"})
if __name__ == "__main__":
    mcp.run(transport="streamable-http")
