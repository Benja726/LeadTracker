# LeadTracker

Multi-tenant lead management dashboard for real estate agencies. Captures leads from WhatsApp via an AI agent, and exposes them through a web dashboard.

## Stack

- **.NET 8 Web API** — authentication, tenant isolation, dashboard reads. Data access via `Supabase.Postgrest` (PostgREST over HTTPS, service-role key) — no EF Core / direct DB connection
- **Blazor WebAssembly** — dashboard frontend. Auth via `Supabase.Gotrue` (auto-refreshing session, persisted to localStorage)
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

The frontend authenticates with Supabase Auth via the GoTrue SDK, which keeps the session alive (auto-refresh + localStorage persistence). The current access token is sent on every API request via `Authorization: Bearer <token>`. The API validates the token and resolves the user's accessible businesses from `business_memberships`.

## Multi-tenancy

A user can belong to multiple businesses. Every query is scoped to a `business_id` that has been validated against the caller's memberships. No exceptions.

## Local setup

```bash
dotnet user-secrets set "Supabase:Url" "https://your-project.supabase.co" --project src/LeadTracker.Api
dotnet user-secrets set "Supabase:ServiceRoleKey" "<service_role_key>" --project src/LeadTracker.Api
```

Get the service-role key from the Supabase dashboard → Project Settings → API → `service_role`. Leave `Supabase:JwtSecret` unset — JWTs are validated via JWKS (ES256). The Web app's Supabase URL + anon key live in `src/LeadTracker.Web/wwwroot/appsettings.json`.

## Run

```bash
# API (must use the https profile → binds :7028)
dotnet run --project src/LeadTracker.Api --launch-profile https

# Frontend
dotnet run --project src/LeadTracker.Web
```

- API + Swagger: `https://localhost:7028/swagger`
- Dashboard: the URL printed by `dotnet run` (e.g. `https://localhost:7088`)

## Data models

PostgREST `BaseModel` classes live in `src/LeadTracker.Infrastructure/Models` — hand-written, one per table the API reads, with snake_case `[Column]` names matching the DB. Add only the columns the API actually uses; never migrate the Supabase-owned schema from .NET.
