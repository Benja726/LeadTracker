# LeadTracker

Multi-tenant lead management dashboard for real estate agencies. Captures leads from WhatsApp, Instagram, and Facebook via an AI agent, and exposes them through a web dashboard.

## Stack

- **.NET 8 Web API** — authentication, tenant isolation, dashboard reads
- **Blazor WebAssembly** — dashboard frontend
- **Supabase** — Postgres database + Auth
- **n8n** — automation workflows (WhatsApp AI agent, CRM sync)

## Architecture

Data is written exclusively by n8n workflows:
- **Sales Agent**: WhatsApp → AI Agent (Anthropic) → saves `conversations` + `leads`
- **Property Sync**: CRM (CasasWeb) → upserts `properties`

The .NET API is **read-only** from the dashboard's perspective. It validates the JWT issued by Supabase Auth and enforces tenant scoping in code.

## Schema (7 tables, all scoped by `business_id`)

| Table | Description |
|---|---|
| `businesses` | Each agency (tenant root) |
| `users` | Dashboard users |
| `business_memberships` | User ↔ business relationships with role (`owner/admin/member`) |
| `business_whatsapp_numbers` | WhatsApp numbers per business |
| `properties` | Property inventory from CRM |
| `conversations` | Chat transcripts (`history` JSONB) |
| `leads` | Structured lead profiles (name, intent, classification, handoff status) |

## Authentication

The frontend authenticates with Supabase Auth and receives a JWT (`access_token`). That token is sent on every API request via `Authorization: Bearer <token>`. The API validates the token and resolves the user's accessible businesses from `business_memberships`.

## Multi-tenancy

A user can belong to multiple businesses. Every query is scoped to a `business_id` that has been validated against the caller's memberships. No exceptions.

## Local setup

```bash
dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co" --project src/LeadTracker.Api
dotnet user-secrets set "ConnectionStrings:Supabase" "<connection-string>" --project src/LeadTracker.Api
```

## Run

```bash
# API
dotnet run --project src/LeadTracker.Api --launch-profile https

# Frontend
dotnet run --project src/LeadTracker.Web --launch-profile https
```

- API + Swagger: `https://localhost:7028/swagger`
- Dashboard: `https://localhost:7088`

## Re-scaffold entities

```bash
dotnet ef dbcontext scaffold "<connection-string>" Npgsql.EntityFrameworkCore.PostgreSQL \
  -o Data/Scaffolded --context LeadTrackerDbContext --schema public --no-onconfiguring \
  --project src/LeadTracker.Infrastructure --startup-project src/LeadTracker.Api --force
```
