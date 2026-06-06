# LeadTracker — CLAUDE.md

.NET 8 API: dashboard backend for a multi-tenant WhatsApp/IG/FB lead system for real-estate agencies. This service does **auth + tenant isolation + dashboard reads only**. Data lives in Supabase (Postgres) and is written by an n8n agent.

## Stack
.NET 8 Web API · EF Core + Npgsql · Supabase (Postgres + Auth).
Solution `LeadTracker.sln` → `src/{Api, Core, Infrastructure}`.

## Locked decisions — do not re-litigate
- **Auth:** Supabase Auth. The API only *validates* the JWT (JwtBearer). No password/user handling.
- **DB access:** Option A — connect with a privileged role and enforce tenant scoping in C# code. RLS exists in Supabase but is NOT relied on from .NET.
- **Schema is owned by Supabase/n8n → database-first.** Generate entities via `dotnet ef dbcontext scaffold`. Never hand-write or migrate the existing tables.
- **Tenancy key:** `business_id`. One user can belong to many businesses.

## Tenancy chain (enforce on every data query)
JWT `sub` == `public.users.id` → `business_memberships (user_id, business_id, role: owner|admin|member)` → accessible `business_id`s.
Every query touching tenant data MUST filter by a `business_id` that was validated against the caller's memberships. No exceptions.

## Schema (7 tables, all scoped by business_id)
`businesses` · `users` · `business_memberships` · `business_whatsapp_numbers` · `properties` (price numeric + currency; uniq business_id,ref) · `conversations` (history jsonb; uniq business_id,phone) · `leads` (uniq business_id,phone).

## Rules
- Secrets (Supabase URL, JWT secret, DB connection string) → user-secrets / env vars. NEVER commit. `appsettings.json` holds placeholders only.
- Minimal dependencies — don't add packages without a clear need.
- Scaffolded code lives in `src/LeadTracker.Infrastructure/Data`. Don't hand-edit generated files; re-scaffold instead.
- Conventional commit messages.

## Commands
- Build: `dotnet build`
- Run: `dotnet run --project src/LeadTracker.Api`
- Set secret: `dotnet user-secrets set "<key>" "<value>" --project src/LeadTracker.Api`
- Scaffold:
  `dotnet ef dbcontext scaffold "$CONN" Npgsql.EntityFrameworkCore.PostgreSQL -o Data/Scaffolded --context LeadTrackerDbContext --schema public --no-onconfiguring --project src/LeadTracker.Infrastructure --startup-project src/LeadTracker.Api`

## Working style
Be concise. Skip preamble, recaps, and restating this file. Make the change, show only the relevant diff/output. Ask only when blocked by real ambiguity. Keep the build green.
