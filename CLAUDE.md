# LeadTracker — CLAUDE.md

.NET 8 API: dashboard backend for a multi-tenant WhatsApp/IG/FB lead system for real-estate agencies. This service does **auth + tenant isolation + dashboard reads only**. Data lives in Supabase (Postgres) and is written by an n8n agent.

## Stack
.NET 8 Web API · supabase-csharp over HTTPS (`Supabase.Postgrest` in the API, `Supabase.Gotrue` in the Web app) · Supabase (Postgres + Auth). No EF Core / Npgsql / direct DB connection.
Solution `LeadTracker.sln` → `src/{Api, Core, Infrastructure}`.

## Locked decisions — do not re-litigate
- **Auth:** Supabase Auth. The API only *validates* the JWT (JwtBearer). The Web app uses the GoTrue SDK (auto-refresh + localStorage persistence); no hand-rolled token handling.
- **Data access:** PostgREST over HTTPS via `Supabase.Postgrest`. The API authenticates with the **service-role key** (bypasses RLS) and enforces tenant scoping in C# code — same posture as the old "Option A". RLS exists in Supabase but is NOT relied on from .NET. No connection string / pooler / Npgsql.
- **Schema is owned by Supabase/n8n.** Hand-write `BaseModel` classes in `src/LeadTracker.Infrastructure/Models` for only the tables the controllers query (`[Table]`/`[Column]`/`[Reference]`). Never migrate the existing tables from .NET.
- **Tenancy key:** `business_id`. One user can belong to many businesses.

## Tenancy chain (enforce on every data query)
JWT `sub` == `public.users.id` → `business_memberships (user_id, business_id, role: owner|admin|member)` → accessible `business_id`s.
Every query touching tenant data MUST filter by a `business_id` that was validated against the caller's memberships. No exceptions.

## Schema (7 tables, all scoped by business_id)
`businesses` · `users` · `business_memberships` · `business_whatsapp_numbers` · `properties` (price numeric + currency; uniq business_id,ref) · `conversations` (history jsonb; uniq business_id,phone) · `leads` (uniq business_id,phone).

## Rules
- Secrets (Supabase URL, service-role key) → user-secrets / env vars. NEVER commit. `appsettings.json` holds placeholders only.
- Minimal dependencies — don't add packages without a clear need.
- PostgREST models live in `src/LeadTracker.Infrastructure/Models`. Add only the columns the API actually reads; keep snake_case `[Column]` names matching the DB.
- Conventional commit messages.

## Commands
- Build: `dotnet build`
- Run: `dotnet run --project src/LeadTracker.Api`
- Set secret: `dotnet user-secrets set "<key>" "<value>" --project src/LeadTracker.Api`

## Local dev setup (read this before first run — avoids the common failures)
Two processes must run: the API (`src/LeadTracker.Api`) and the Blazor Web app (`src/LeadTracker.Web`). Login hits Supabase directly, but the dashboard needs the API.

1. **Run the API on the `https` profile**, not the default:
   `dotnet run --project src/LeadTracker.Api --launch-profile https`
   It must listen on `https://localhost:7028` — that's what the Web app's `Api:BaseUrl` and the API's CORS `AllowedOrigins` expect. The default (http) profile only binds `5184`, so the frontend gets `ERR_CONNECTION_REFUSED`. Trust the dev cert once: `dotnet dev-certs https --trust`.

2. **Data access is HTTPS/PostgREST — no DB connection.** The API talks to `{Supabase:Url}/rest/v1` using the **service-role key**. Set it as a secret (never in `appsettings.json`):
   ```
   dotnet user-secrets set "Supabase:ServiceRoleKey" "<service_role_key>" --project src/LeadTracker.Api
   ```
   Get the key from dashboard → Project Settings → API → `service_role`. The old `ConnectionStrings:Supabase` / pooler / IPv6 / DB-password setup is gone — remove that secret if it lingers (`dotnet user-secrets remove "ConnectionStrings:Supabase" --project src/LeadTracker.Api`).

3. **JWT validation uses ES256 via JWKS, not the legacy HS256 secret.** This project (new `sb_publishable_` anon key) signs tokens with asymmetric keys. **Leave `Supabase:JwtSecret` empty/unset** so the API uses the JWKS branch. Setting it forces HS256 → every request 401s.

4. **Required secrets:** `Supabase:Url`, `Supabase:ServiceRoleKey`. Web app config (`src/LeadTracker.Web/wwwroot/appsettings.json`) holds the Supabase URL + anon key + `Api:BaseUrl=https://localhost:7028`. The Web app's GoTrue client persists the session to localStorage and auto-refreshes the token, so you stay logged in across reloads and past the ~1h access-token expiry.

Symptom → cause cheat sheet: `ERR_CONNECTION_REFUSED` on :7028 → API not on https profile. `401` from `/api/...` (problem+json, no `WWW-Authenticate`) → token valid but tenancy/claim issue; `401` with `WWW-Authenticate: Bearer` → JWT rejected (likely HS256 vs ES256). `500` on startup "Missing 'Supabase:ServiceRoleKey'" → service-role secret not set. PostgREST `401`/`permission denied` on data reads → wrong/missing service-role key.

## Working style
Be concise. Skip preamble, recaps, and restating this file. Make the change, show only the relevant diff/output. Ask only when blocked by real ambiguity. Keep the build green.
