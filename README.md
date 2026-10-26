# Arpay AI Collections Demo

Synthetic-data pilot for governed accounts-receivable automation.

## Run locally
Requires Docker Engine/Desktop and Compose v2.
```bash
cp .env.example .env
docker compose up --build
```
- Dashboard: http://localhost:8080
- Orchestrator: http://localhost:8081/health
- Fake ERP: http://localhost:8082/health
- Python MCP: http://localhost:8083/health
- Keycloak: http://localhost:8089

The demo supports overdue invoice review, reminder proposal, approval/rejection, simulated execution and audit review. No real emails or payments are sent.

## Services
ASP.NET Core Orchestrator; ASP.NET Core Fake ERP API; Python MCP server; PostgreSQL; Keycloak; static dashboard.

## Security status
This is a scaffold, not production-ready. Keycloak is included but OIDC/JWT validation is not wired into the API. The dashboard role selector is a demo convenience, not authentication. Do not expose this stack publicly or use real customer data. Tenant isolation and audit failure handling require further hardening.

Use Compose for the whole stack. A single docker run is not a safe supervisor for this multi-container application.
