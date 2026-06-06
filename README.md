# LeadTracker — API (.NET 8)

Backend del sistema de captación de leads por WhatsApp/IG/FB para inmobiliarias.
Sirve el **dashboard** y resuelve la **autenticación** y el **aislamiento multi-tenant**.

## Arquitectura del equipo

- **n8n** (tu compañero): dos workflows.
  - *Update Propiedades*: sincroniza el CRM (CasasWeb) → tabla `properties` (upsert masivo).
  - *Agente Ventas Inmobiliarias*: Twilio WhatsApp → AI Agent (Anthropic) con 3 herramientas
    (RPCs) → guarda `conversations` + `leads` → responde por el mismo número.
- **Supabase** (ya hecho): Postgres + Auth. Fuente de verdad. RLS prendido en todas las tablas.
- **.NET API** (este repo): lee de Supabase para el dashboard y valida la identidad de cada request.
  No maneja usuarios ni contraseñas (de eso se encarga Supabase Auth).

## Esquema en Supabase (7 tablas, todas multi-tenant por `business_id`)

- `businesses` — cada inmobiliaria cliente (raíz del tenant).
- `users` + `business_memberships` — usuarios del dashboard y su relación con cada negocio,
  con rol `owner/admin/member`. **Un usuario puede pertenecer a varios negocios.**
- `business_whatsapp_numbers` — los números de WhatsApp de cada negocio.
- `properties` — inventario del CRM. `price` numérico + `currency` aparte. Única por `(business_id, ref)`.
- `conversations` — transcript del chat (`history` JSONB) + `whatsapp_number_id`. Única por `(business_id, phone)`.
- `leads` — perfil estructurado del lead (name, operation, zone, budget, classification,
  ready_for_handoff…). Única por `(business_id, phone)`.

RPCs (herramientas del agente): `search_properties`, `get_property`, `list_zones` — con guard de
tenancy. El agente entra con `service_role`; un usuario del dashboard solo ve lo de sus negocios.

## Autenticación (Supabase Auth)

El frontend hace login con Supabase y recibe un `access_token` (JWT). Lo manda en cada request
(`Authorization: Bearer <token>`). La API **solo valida** ese token; soporta los dos modos de firma:

- **Asimétrico (ES256, recomendado/por defecto):** baja las claves públicas del endpoint JWKS y
  las cachea/rota solo. Alcanza con `Supabase:Url`.
- **Legacy HS256:** solo si el proyecto usa el JWT Secret viejo → ponerlo en `Supabase:JwtSecret`.

> Verificá en el Dashboard (Authentication → JWT signing keys) qué modo usa. Si dudás, dejá
> `JwtSecret` vacío: usa JWKS.

### Multi-tenancy (importante — modelo por membresías, NO un tenant_id)

No hay un solo tenant por usuario. El token identifica al usuario (`sub` = `auth.users.id`),
y el acceso se resuelve así:

1. La API consulta `business_memberships` por el `sub` para saber a qué negocios puede acceder
   ese usuario y con qué rol.
2. El dashboard elige/scopea a un negocio.
3. **Toda** query filtra por ese `business_id`, validando que esté entre las membresías del usuario.
4. El rol (`owner/admin/member`) gatea las acciones.

> **Confirmar con el equipo:** ¿`business_memberships.user_id` apunta directo a `auth.users.id`
> (el `sub`), o hay una tabla `users` intermedia que mapea?

### Cómo conecta .NET a la DB (decisión clave, RLS ya existe)

- **Opción A (recomendada):** conectar con un rol privilegiado dedicado, RLS bypasseado, y
  enforzar el scoping por `business_id` **en código** (validado contra las membresías). Flexible;
  el filtro por `business_id` tiene que ser transversal y no opcional.
- **Opción B:** correr las queries bajo la identidad del usuario (claims del JWT en la sesión
  de Postgres / Data API con el token del usuario) y dejar que RLS haga el trabajo. Más seguro
  por defecto, más incómodo con EF Core/Npgsql.

## Configuración local (sin secretos en git)

```bash
cd src/LeadTracker.Api
dotnet user-secrets set "Supabase:Url" "https://TU-PROYECTO.supabase.co"
# Solo si el proyecto usa HS256 legacy:
# dotnet user-secrets set "Supabase:JwtSecret" "tu-jwt-secret"
```

## Correr

```bash
dotnet restore        # necesita internet (nuget.org) la primera vez
dotnet run --project src/LeadTracker.Api
curl https://localhost:5001/api/me -H "Authorization: Bearer <access_token>"
```

## Generar las entidades reales (database-first)

En vez de escribir las entidades a mano, generalas desde la DB viva:

```bash
dotnet ef dbcontext scaffold "<connection-string-de-Supabase>" Npgsql.EntityFrameworkCore.PostgreSQL \
  -o Data/Scaffolded --context LeadTrackerDbContext --schema public --no-onconfiguring
```

(Las entidades en `LeadTracker.Core` son sketches para anclar el código del dashboard; el scaffold
manda en cuanto a tipos/nullability exactos.)

## Roadmap

- [x] Repo + estructura de solución
- [x] Validación del JWT de Supabase (asimétrico + fallback HS256)
- [x] Endpoint `/api/me` de prueba
- [x] Entidades núcleo de tenancy (`Business`, `BusinessMembership`)
- [ ] Confirmar el FK de `business_memberships.user_id` ↔ `auth.users.id`
- [ ] EF Core scaffold de las 7 tablas existentes (database-first)
- [ ] Resolver membresías por `sub` → servicio de negocios accesibles del usuario
- [ ] Scoping transversal por `business_id` (validado contra membresías)
- [ ] Endpoints del dashboard: leads, conversaciones, properties, asignación
- [ ] Authorization policies por rol (`owner/admin/member`)
