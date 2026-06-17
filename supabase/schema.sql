


SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;


COMMENT ON SCHEMA "public" IS 'standard public schema';



CREATE EXTENSION IF NOT EXISTS "pg_stat_statements" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "pg_trgm" WITH SCHEMA "public";






CREATE EXTENSION IF NOT EXISTS "pgcrypto" WITH SCHEMA "extensions";






CREATE EXTENSION IF NOT EXISTS "supabase_vault" WITH SCHEMA "vault";






CREATE EXTENSION IF NOT EXISTS "uuid-ossp" WITH SCHEMA "extensions";






CREATE OR REPLACE FUNCTION "public"."diagnose_property_search"("p_business_id" "uuid", "p_operation" "text" DEFAULT NULL::"text", "p_zone" "text" DEFAULT NULL::"text", "p_type" "text" DEFAULT NULL::"text", "p_min_bedrooms" integer DEFAULT NULL::integer, "p_max_bedrooms" integer DEFAULT NULL::integer, "p_currency" "text" DEFAULT NULL::"text", "p_min_price" numeric DEFAULT NULL::numeric, "p_max_price" numeric DEFAULT NULL::numeric, "p_period" "text" DEFAULT NULL::"text") RETURNS TABLE("exact_count" bigint, "without_zone_count" bigint, "without_type_count" bigint, "less_bedrooms_same_zone_count" bigint, "closest_less_bedrooms_same_zone" integer, "less_bedrooms_any_zone_count" bigint, "closest_less_bedrooms_any_zone" integer, "min_price_same_bedrooms_same_zone" numeric, "min_price_same_bedrooms_any_zone" numeric, "suggested_action" "text")
    LANGUAGE "plpgsql"
    SET "search_path" TO 'public'
    AS $$declare
  v_seasonal boolean := coalesce(p_period in ('fortnight','week','day'), false);
  v_target_bedrooms int := coalesce(p_min_bedrooms, p_max_bedrooms);

  v_exact_count bigint := 0;
  v_without_zone_count bigint := 0;
  v_without_type_count bigint := 0;

  v_less_bedrooms_same_zone_count bigint := 0;
  v_closest_less_bedrooms_same_zone int := null;

  v_less_bedrooms_any_zone_count bigint := 0;
  v_closest_less_bedrooms_any_zone int := null;

  v_min_price_same_bedrooms_same_zone numeric := null;
  v_min_price_same_bedrooms_any_zone numeric := null;

  v_suggested_action text := 'no_viable_alternative';
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode = '42501';
  end if;

  with prefiltered as (
    select
      p.bedrooms,
      p.zone,
      p.type,
      case p_period
        when 'fortnight' then p.price_fortnight
        when 'week'      then p.price_week
        when 'day'       then p.price_day
        else p.price
      end as eff_price
    from public.properties p
    where p.business_id = p_business_id
      -- en temporada SOLO seasonales; en mensual/anual/venta no aplica
      and (not v_seasonal or p.is_seasonal is true)
      and (p_operation is null or p.operation ilike '%' || p_operation || '%')
      and (p_currency  is null or p.currency = p_currency)
  ),
  base as (
    select
      pf.bedrooms,
      pf.eff_price,
      (p_zone is null or pf.zone ilike '%' || p_zone || '%') as zone_ok,
      (p_type is null or pf.type ilike '%' || p_type || '%') as type_ok,
      ( (p_min_bedrooms is null or pf.bedrooms >= p_min_bedrooms)
        and (p_max_bedrooms is null or pf.bedrooms <= p_max_bedrooms) ) as bed_in_range,
      ( v_target_bedrooms is not null
        and v_target_bedrooms > 0
        and pf.bedrooms is not null
        and pf.bedrooms < v_target_bedrooms ) as bed_less,
      -- presupuesto: NEUTRALIZADO en temporada (datos de precio por período poco confiables)
      ( (p_max_price is null or v_seasonal or pf.eff_price <= p_max_price)
        and (p_min_price is null or v_seasonal or pf.eff_price >= p_min_price) ) as budget_ok,
      -- para "desde cuánto arranca": solo cota inferior, ignora p_max_price
      (p_min_price is null or v_seasonal or pf.eff_price >= p_min_price) as budget_min_ok
    from prefiltered pf
  )
  select
    count(*) filter (where b.zone_ok and b.type_ok and b.bed_in_range and b.budget_ok),
    count(*) filter (where b.type_ok and b.bed_in_range and b.budget_ok),
    count(*) filter (where b.zone_ok and b.bed_in_range and b.budget_ok),

    count(*)      filter (where b.zone_ok and b.type_ok and b.bed_less and b.budget_ok),
    max(b.bedrooms) filter (where b.zone_ok and b.type_ok and b.bed_less and b.budget_ok),

    count(*)      filter (where b.type_ok and b.bed_less and b.budget_ok),
    max(b.bedrooms) filter (where b.type_ok and b.bed_less and b.budget_ok),

    min(b.eff_price) filter (
      where b.zone_ok and b.type_ok and b.bed_in_range and b.budget_min_ok and b.eff_price is not null),
    min(b.eff_price) filter (
      where b.type_ok and b.bed_in_range and b.budget_min_ok and b.eff_price is not null)
  into
    v_exact_count,
    v_without_zone_count,
    v_without_type_count,
    v_less_bedrooms_same_zone_count,
    v_closest_less_bedrooms_same_zone,
    v_less_bedrooms_any_zone_count,
    v_closest_less_bedrooms_any_zone,
    v_min_price_same_bedrooms_same_zone,
    v_min_price_same_bedrooms_any_zone
  from base b;

  -- Acción recomendada (las de presupuesto NO disparan en temporada)
  if v_exact_count > 0 then
    v_suggested_action := 'show_exact';

  elsif p_zone is not null and v_without_zone_count > 0 then
    v_suggested_action := 'show_other_zones';

  elsif p_type is not null and v_without_type_count > 0 then
    v_suggested_action := 'show_relaxed_type';

  elsif v_less_bedrooms_same_zone_count > 0 then
    v_suggested_action := 'show_less_bedrooms_same_zone';

  elsif p_zone is not null and v_less_bedrooms_any_zone_count > 0 then
    v_suggested_action := 'show_less_bedrooms_any_zone';

  elsif not v_seasonal and v_min_price_same_bedrooms_same_zone is not null then
    v_suggested_action := 'offer_higher_budget_same_zone';

  elsif not v_seasonal and p_zone is not null and v_min_price_same_bedrooms_any_zone is not null then
    v_suggested_action := 'offer_higher_budget_any_zone';

  else
    v_suggested_action := 'no_viable_alternative';
  end if;

  if v_seasonal then
    v_min_price_same_bedrooms_same_zone := null;
    v_min_price_same_bedrooms_any_zone  := null;
  end if;

  return query
  select
    v_exact_count,
    v_without_zone_count,
    v_without_type_count,
    v_less_bedrooms_same_zone_count,
    v_closest_less_bedrooms_same_zone,
    v_less_bedrooms_any_zone_count,
    v_closest_less_bedrooms_any_zone,
    v_min_price_same_bedrooms_same_zone,
    v_min_price_same_bedrooms_any_zone,
    v_suggested_action;
end;$$;


ALTER FUNCTION "public"."diagnose_property_search"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_currency" "text", "p_min_price" numeric, "p_max_price" numeric, "p_period" "text") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."get_property"("p_business_id" "uuid", "p_ref" "text") RETURNS TABLE("ref" "text", "type" "text", "operation" "text", "zone" "text", "bedrooms" integer, "bathrooms" integer, "m2" integer, "price" numeric, "currency" "text", "price_label" "text", "expenses_label" "text", "status" "text", "title" "text", "extra" "text", "url" "text", "photo_url" "text")
    LANGUAGE "plpgsql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode = '42501';
  end if;

  return query
  select p.ref, p.type, p.operation, p.zone, p.bedrooms, p.bathrooms, p.m2,
         p.price, p.currency, p.price_label, p.expenses_label, p.status,
         p.title, p.extra, p.url, p.photo_url
  from public.properties p
  where p.business_id = p_business_id
    and p.ref ilike p_ref
  limit 1;
end;
$$;


ALTER FUNCTION "public"."get_property"("p_business_id" "uuid", "p_ref" "text") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."is_business_member"("p_business_id" "uuid") RETURNS boolean
    LANGUAGE "sql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
  select exists (
    select 1 from public.business_memberships m
    where m.business_id = p_business_id
      and m.user_id = auth.uid()
  );
$$;


ALTER FUNCTION "public"."is_business_member"("p_business_id" "uuid") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."list_types"("p_business_id" "uuid") RETURNS TABLE("type" "text", "count" bigint)
    LANGUAGE "plpgsql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode = '42501';
  end if;

  return query
  select btrim(p.type) as type,
         count(*) as count
  from public.properties p
  where p.business_id = p_business_id
    and btrim(coalesce(p.type, '')) <> ''
  group by btrim(p.type)
  order by count(*) desc, btrim(p.type) asc;
end;
$$;


ALTER FUNCTION "public"."list_types"("p_business_id" "uuid") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."list_zones"("p_business_id" "uuid") RETURNS TABLE("zone" "text", "count" bigint)
    LANGUAGE "plpgsql" STABLE SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode = '42501';
  end if;

  return query
  select p.zone, count(*) as count
  from public.properties p
  where p.business_id = p_business_id
    and p.zone is not null and p.zone <> ''
  group by p.zone
  order by count desc, p.zone asc;
end;
$$;


ALTER FUNCTION "public"."list_zones"("p_business_id" "uuid") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."rls_auto_enable"() RETURNS "event_trigger"
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'pg_catalog'
    AS $$
DECLARE
  cmd record;
BEGIN
  FOR cmd IN
    SELECT *
    FROM pg_event_trigger_ddl_commands()
    WHERE command_tag IN ('CREATE TABLE', 'CREATE TABLE AS', 'SELECT INTO')
      AND object_type IN ('table','partitioned table')
  LOOP
     IF cmd.schema_name IS NOT NULL AND cmd.schema_name IN ('public') AND cmd.schema_name NOT IN ('pg_catalog','information_schema') AND cmd.schema_name NOT LIKE 'pg_toast%' AND cmd.schema_name NOT LIKE 'pg_temp%' THEN
      BEGIN
        EXECUTE format('alter table if exists %s enable row level security', cmd.object_identity);
        RAISE LOG 'rls_auto_enable: enabled RLS on %', cmd.object_identity;
      EXCEPTION
        WHEN OTHERS THEN
          RAISE LOG 'rls_auto_enable: failed to enable RLS on %', cmd.object_identity;
      END;
     ELSE
        RAISE LOG 'rls_auto_enable: skip % (either system schema or not in enforced list: %.)', cmd.object_identity, cmd.schema_name;
     END IF;
  END LOOP;
END;
$$;


ALTER FUNCTION "public"."rls_auto_enable"() OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."search_properties"("p_business_id" "uuid", "p_operation" "text" DEFAULT NULL::"text", "p_zone" "text" DEFAULT NULL::"text", "p_type" "text" DEFAULT NULL::"text", "p_min_bedrooms" integer DEFAULT NULL::integer, "p_max_bedrooms" integer DEFAULT NULL::integer, "p_min_bathrooms" integer DEFAULT NULL::integer, "p_min_price" numeric DEFAULT NULL::numeric, "p_max_price" numeric DEFAULT NULL::numeric, "p_currency" "text" DEFAULT NULL::"text", "p_min_m2" numeric DEFAULT NULL::numeric, "p_max_m2" numeric DEFAULT NULL::numeric, "p_sort" "text" DEFAULT 'best_match'::"text", "p_period" "text" DEFAULT NULL::"text", "p_limit" integer DEFAULT 8, "p_offset" integer DEFAULT 0) RETURNS TABLE("ref" "text", "type" "text", "operation" "text", "zone" "text", "bedrooms" integer, "bathrooms" integer, "m2" integer, "is_seasonal" boolean, "expenses_label" "text", "status" "text", "title" "text", "extra" "text", "url" "text", "photo_url" "text", "display_price" "text")
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
declare
  v_limit int := least(greatest(coalesce(p_limit, 8), 1), 25);
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode = '42501';
  end if;

  return query
  with req_zones as (
    select case
      when p_zone is null then null::text[]
      else coalesce(
        (select array_agg(distinct ze.equivalent_zone)
           from public.zone_equivalences ze
          where ze.requested_zone ilike p_zone),
        array[p_zone]
      )
    end as zones
  ),
  base as (
    select
      p.*,
      case
        when p_period = 'fortnight' then p.price_fortnight
        when p_period = 'week'      then p.price_week
        when p_period = 'day'       then p.price_day
        else null
      end as period_price
    from public.properties p
    cross join req_zones rz
    where p.business_id = p_business_id
      and (
        rz.zones is null
        or exists (
          select 1 from unnest(rz.zones) as z(name)
          where p.zone ilike '%' || z.name || '%'
        )
      )
      and (p_operation     is null or p.operation ilike '%' || p_operation || '%')
      and (p_type          is null or p.type      ilike '%' || p_type || '%')
      and (p_min_bedrooms  is null or p.bedrooms  >= p_min_bedrooms)
      and (p_max_bedrooms  is null or p.bedrooms  <= p_max_bedrooms)
      and (p_min_bathrooms is null or p.bathrooms >= p_min_bathrooms)
      and (p_currency      is null or p.currency   = p_currency)
      and (p_min_m2        is null or p.m2        >= p_min_m2)
      and (p_max_m2        is null or p.m2        <= p_max_m2)
      -- temporada y anual son stocks distintos: si piden temporada, solo is_seasonal
      and (
        coalesce(p_period in ('fortnight','week','day'), false) = false
        or p.is_seasonal is true
      )
  ),
  scored as (
    select
      b.*,
      case when b.is_seasonal then b.period_price else b.price end as basis_price
    from base b
  )
  select
    s.ref, s.type, s.operation, s.zone, s.bedrooms, s.bathrooms, s.m2,
    s.is_seasonal,
    s.expenses_label, s.status, s.title, s.extra, s.url, s.photo_url,
    case
      when s.is_seasonal then
        'Valores de temporada por quincena, a confirmar según fechas'
      when s.price is not null then
        coalesce(s.price_label, coalesce(s.currency, 'USD') || ' ' || to_char(s.price, 'FM999G999G990'))
      else
        'Valor a confirmar'
    end as display_price
  from scored s
  where
    (p_max_price is null or (s.is_seasonal and s.basis_price is null) or s.basis_price <= p_max_price)
    and (p_min_price is null or (s.is_seasonal and s.basis_price is null) or s.basis_price >= p_min_price)
  order by
    (s.is_seasonal and s.basis_price is null) asc,
    case when p_sort = 'price_asc'         then s.basis_price end asc  nulls last,
    case when p_sort = 'closest_to_budget' then s.basis_price end desc nulls last,
    s.created_at desc
  limit v_limit
  offset greatest(coalesce(p_offset, 0), 0);
end;
$$;


ALTER FUNCTION "public"."search_properties"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_min_bathrooms" integer, "p_min_price" numeric, "p_max_price" numeric, "p_currency" "text", "p_min_m2" numeric, "p_max_m2" numeric, "p_sort" "text", "p_period" "text", "p_limit" integer, "p_offset" integer) OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text" DEFAULT 'manual'::"text") RETURNS "void"
    LANGUAGE "plpgsql" SECURITY DEFINER
    SET "search_path" TO 'public'
    AS $$
begin
  if not exists (
    select 1
    from business_memberships m
    where m.business_id = p_business_id
      and m.user_id = auth.uid()
  ) then
    raise exception 'no sos miembro de este negocio';
  end if;

  update conversations
     set bot_enabled         = p_enabled,
         bot_disabled_reason  = case when p_enabled then null else p_reason end,
         bot_disabled_at      = case when p_enabled then null else now()    end
   where business_id = p_business_id
     and phone       = p_phone;
end;
$$;


ALTER FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text") OWNER TO "postgres";


CREATE OR REPLACE FUNCTION "public"."set_updated_at"() RETURNS "trigger"
    LANGUAGE "plpgsql"
    AS $$
begin
  new.updated_at = now();
  return new;
end;
$$;


ALTER FUNCTION "public"."set_updated_at"() OWNER TO "postgres";

SET default_tablespace = '';

SET default_table_access_method = "heap";


CREATE TABLE IF NOT EXISTS "public"."business_memberships" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "business_id" "uuid" NOT NULL,
    "user_id" "uuid" NOT NULL,
    "role" "text" DEFAULT 'member'::"text" NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."business_memberships" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."business_whatsapp_numbers" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "business_id" "uuid" NOT NULL,
    "whatsapp_number" "text" NOT NULL,
    "label" "text",
    "active" boolean DEFAULT true NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."business_whatsapp_numbers" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."businesses" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "name" "text" NOT NULL,
    "slug" "text",
    "whatsapp_number" "text",
    "crm_account" "text",
    "active" boolean DEFAULT true NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."businesses" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."conversations" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "business_id" "uuid" NOT NULL,
    "phone" "text" NOT NULL,
    "profile_name" "text",
    "history" "jsonb" DEFAULT '[]'::"jsonb" NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "whatsapp_number_id" "uuid",
    "bot_enabled" boolean DEFAULT true NOT NULL,
    "bot_disabled_reason" "text",
    "bot_disabled_at" timestamp with time zone
);


ALTER TABLE "public"."conversations" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."leads" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "business_id" "uuid" NOT NULL,
    "conversation_id" "uuid",
    "phone" "text" NOT NULL,
    "profile_name" "text",
    "name" "text",
    "operation" "text",
    "zone" "text",
    "type" "text",
    "bedrooms" integer,
    "budget" "text",
    "budget_amount" numeric,
    "timeline" "text",
    "financing" "text",
    "property_of_interest" "text",
    "classification" "text" DEFAULT 'frio'::"text" NOT NULL,
    "reason" "text",
    "ready_for_handoff" boolean DEFAULT false NOT NULL,
    "handoff_done" boolean DEFAULT false NOT NULL,
    "last_message" "text",
    "last_reply" "text",
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "whatsapp_number_id" "uuid"
);


ALTER TABLE "public"."leads" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."message_buffer" (
    "id" bigint NOT NULL,
    "business_id" "uuid" NOT NULL,
    "phone" "text" NOT NULL,
    "message" "text",
    "message_sid" "text",
    "processed" boolean DEFAULT false NOT NULL,
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."message_buffer" OWNER TO "postgres";


ALTER TABLE "public"."message_buffer" ALTER COLUMN "id" ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME "public"."message_buffer_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);



CREATE TABLE IF NOT EXISTS "public"."properties" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "business_id" "uuid" NOT NULL,
    "ref" "text" NOT NULL,
    "type" "text",
    "operation" "text",
    "zone" "text",
    "bedrooms" integer,
    "bathrooms" integer,
    "m2" integer,
    "price" numeric,
    "currency" "text" DEFAULT 'USD'::"text",
    "price_label" "text",
    "expenses_amount" numeric,
    "expenses_label" "text",
    "status" "text",
    "title" "text",
    "extra" "text",
    "url" "text",
    "photo_url" "text",
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "price_fortnight" numeric,
    "price_week" numeric,
    "price_day" numeric,
    "is_seasonal" boolean DEFAULT false
);


ALTER TABLE "public"."properties" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."users" (
    "id" "uuid" DEFAULT "gen_random_uuid"() NOT NULL,
    "email" "text" NOT NULL,
    "full_name" "text",
    "created_at" timestamp with time zone DEFAULT "now"() NOT NULL,
    "updated_at" timestamp with time zone DEFAULT "now"() NOT NULL
);


ALTER TABLE "public"."users" OWNER TO "postgres";


CREATE TABLE IF NOT EXISTS "public"."zone_equivalences" (
    "requested_zone" "text" NOT NULL,
    "equivalent_zone" "text" NOT NULL
);


ALTER TABLE "public"."zone_equivalences" OWNER TO "postgres";


ALTER TABLE ONLY "public"."business_memberships"
    ADD CONSTRAINT "business_memberships_business_id_user_id_key" UNIQUE ("business_id", "user_id");



ALTER TABLE ONLY "public"."business_memberships"
    ADD CONSTRAINT "business_memberships_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."business_whatsapp_numbers"
    ADD CONSTRAINT "business_whatsapp_numbers_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."business_whatsapp_numbers"
    ADD CONSTRAINT "business_whatsapp_numbers_whatsapp_number_key" UNIQUE ("whatsapp_number");



ALTER TABLE ONLY "public"."businesses"
    ADD CONSTRAINT "businesses_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."businesses"
    ADD CONSTRAINT "businesses_slug_key" UNIQUE ("slug");



ALTER TABLE ONLY "public"."conversations"
    ADD CONSTRAINT "conversations_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."conversations"
    ADD CONSTRAINT "conversations_whatsapp_number_id_phone_key" UNIQUE ("whatsapp_number_id", "phone");



ALTER TABLE ONLY "public"."leads"
    ADD CONSTRAINT "leads_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."leads"
    ADD CONSTRAINT "leads_whatsapp_number_id_phone_key" UNIQUE ("whatsapp_number_id", "phone");



ALTER TABLE ONLY "public"."message_buffer"
    ADD CONSTRAINT "message_buffer_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."properties"
    ADD CONSTRAINT "properties_business_id_ref_key" UNIQUE ("business_id", "ref");



ALTER TABLE ONLY "public"."properties"
    ADD CONSTRAINT "properties_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."users"
    ADD CONSTRAINT "users_email_key" UNIQUE ("email");



ALTER TABLE ONLY "public"."users"
    ADD CONSTRAINT "users_pkey" PRIMARY KEY ("id");



ALTER TABLE ONLY "public"."zone_equivalences"
    ADD CONSTRAINT "zone_equivalences_pkey" PRIMARY KEY ("requested_zone", "equivalent_zone");



CREATE UNIQUE INDEX "conversations_business_phone_key" ON "public"."conversations" USING "btree" ("business_id", "phone");



CREATE INDEX "idx_business_whatsapp_numbers_business" ON "public"."business_whatsapp_numbers" USING "btree" ("business_id");



CREATE INDEX "idx_conversations_biz_phone" ON "public"."conversations" USING "btree" ("business_id", "phone");



CREATE INDEX "idx_leads_biz_class" ON "public"."leads" USING "btree" ("business_id", "classification");



CREATE INDEX "idx_leads_biz_handoff" ON "public"."leads" USING "btree" ("business_id", "ready_for_handoff", "handoff_done");



CREATE INDEX "idx_memberships_business" ON "public"."business_memberships" USING "btree" ("business_id");



CREATE INDEX "idx_memberships_user" ON "public"."business_memberships" USING "btree" ("user_id");



CREATE INDEX "idx_properties_biz_beds" ON "public"."properties" USING "btree" ("business_id", "bedrooms");



CREATE INDEX "idx_properties_biz_op" ON "public"."properties" USING "btree" ("business_id", "operation");



CREATE INDEX "idx_properties_biz_price" ON "public"."properties" USING "btree" ("business_id", "price");



CREATE INDEX "idx_properties_type_trgm" ON "public"."properties" USING "gin" ("type" "public"."gin_trgm_ops");



CREATE INDEX "idx_properties_zone_trgm" ON "public"."properties" USING "gin" ("zone" "public"."gin_trgm_ops");



CREATE UNIQUE INDEX "leads_business_phone_key" ON "public"."leads" USING "btree" ("business_id", "phone");



CREATE INDEX "message_buffer_lookup_idx" ON "public"."message_buffer" USING "btree" ("business_id", "phone", "processed", "created_at");



CREATE UNIQUE INDEX "properties_business_ref_key" ON "public"."properties" USING "btree" ("business_id", "ref");



CREATE OR REPLACE TRIGGER "trg_business_whatsapp_numbers_updated" BEFORE UPDATE ON "public"."business_whatsapp_numbers" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



CREATE OR REPLACE TRIGGER "trg_businesses_updated" BEFORE UPDATE ON "public"."businesses" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



CREATE OR REPLACE TRIGGER "trg_conversations_updated" BEFORE UPDATE ON "public"."conversations" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



CREATE OR REPLACE TRIGGER "trg_leads_updated" BEFORE UPDATE ON "public"."leads" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



CREATE OR REPLACE TRIGGER "trg_properties_updated" BEFORE UPDATE ON "public"."properties" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



CREATE OR REPLACE TRIGGER "trg_users_updated" BEFORE UPDATE ON "public"."users" FOR EACH ROW EXECUTE FUNCTION "public"."set_updated_at"();



ALTER TABLE ONLY "public"."business_memberships"
    ADD CONSTRAINT "business_memberships_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."business_memberships"
    ADD CONSTRAINT "business_memberships_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "public"."users"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."business_whatsapp_numbers"
    ADD CONSTRAINT "business_whatsapp_numbers_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."conversations"
    ADD CONSTRAINT "conversations_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."conversations"
    ADD CONSTRAINT "conversations_whatsapp_number_id_fkey" FOREIGN KEY ("whatsapp_number_id") REFERENCES "public"."business_whatsapp_numbers"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."leads"
    ADD CONSTRAINT "leads_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."leads"
    ADD CONSTRAINT "leads_conversation_id_fkey" FOREIGN KEY ("conversation_id") REFERENCES "public"."conversations"("id") ON DELETE SET NULL;



ALTER TABLE ONLY "public"."leads"
    ADD CONSTRAINT "leads_whatsapp_number_id_fkey" FOREIGN KEY ("whatsapp_number_id") REFERENCES "public"."business_whatsapp_numbers"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."message_buffer"
    ADD CONSTRAINT "message_buffer_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE ONLY "public"."properties"
    ADD CONSTRAINT "properties_business_id_fkey" FOREIGN KEY ("business_id") REFERENCES "public"."businesses"("id") ON DELETE CASCADE;



ALTER TABLE "public"."business_memberships" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."business_whatsapp_numbers" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."businesses" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."conversations" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."leads" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."message_buffer" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."properties" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."users" ENABLE ROW LEVEL SECURITY;


ALTER TABLE "public"."zone_equivalences" ENABLE ROW LEVEL SECURITY;




ALTER PUBLICATION "supabase_realtime" OWNER TO "postgres";


GRANT USAGE ON SCHEMA "public" TO "postgres";
GRANT USAGE ON SCHEMA "public" TO "anon";
GRANT USAGE ON SCHEMA "public" TO "authenticated";
GRANT USAGE ON SCHEMA "public" TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_in"("cstring") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_in"("cstring") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_in"("cstring") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_in"("cstring") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_out"("public"."gtrgm") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_out"("public"."gtrgm") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_out"("public"."gtrgm") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_out"("public"."gtrgm") TO "service_role";






















































































































































GRANT ALL ON FUNCTION "public"."diagnose_property_search"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_currency" "text", "p_min_price" numeric, "p_max_price" numeric, "p_period" "text") TO "anon";
GRANT ALL ON FUNCTION "public"."diagnose_property_search"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_currency" "text", "p_min_price" numeric, "p_max_price" numeric, "p_period" "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."diagnose_property_search"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_currency" "text", "p_min_price" numeric, "p_max_price" numeric, "p_period" "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."get_property"("p_business_id" "uuid", "p_ref" "text") TO "anon";
GRANT ALL ON FUNCTION "public"."get_property"("p_business_id" "uuid", "p_ref" "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."get_property"("p_business_id" "uuid", "p_ref" "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."gin_extract_query_trgm"("text", "internal", smallint, "internal", "internal", "internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gin_extract_query_trgm"("text", "internal", smallint, "internal", "internal", "internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gin_extract_query_trgm"("text", "internal", smallint, "internal", "internal", "internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gin_extract_query_trgm"("text", "internal", smallint, "internal", "internal", "internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gin_extract_value_trgm"("text", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gin_extract_value_trgm"("text", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gin_extract_value_trgm"("text", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gin_extract_value_trgm"("text", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gin_trgm_consistent"("internal", smallint, "text", integer, "internal", "internal", "internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gin_trgm_consistent"("internal", smallint, "text", integer, "internal", "internal", "internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gin_trgm_consistent"("internal", smallint, "text", integer, "internal", "internal", "internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gin_trgm_consistent"("internal", smallint, "text", integer, "internal", "internal", "internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gin_trgm_triconsistent"("internal", smallint, "text", integer, "internal", "internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gin_trgm_triconsistent"("internal", smallint, "text", integer, "internal", "internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gin_trgm_triconsistent"("internal", smallint, "text", integer, "internal", "internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gin_trgm_triconsistent"("internal", smallint, "text", integer, "internal", "internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_compress"("internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_compress"("internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_compress"("internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_compress"("internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_consistent"("internal", "text", smallint, "oid", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_consistent"("internal", "text", smallint, "oid", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_consistent"("internal", "text", smallint, "oid", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_consistent"("internal", "text", smallint, "oid", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_decompress"("internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_decompress"("internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_decompress"("internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_decompress"("internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_distance"("internal", "text", smallint, "oid", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_distance"("internal", "text", smallint, "oid", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_distance"("internal", "text", smallint, "oid", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_distance"("internal", "text", smallint, "oid", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_options"("internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_options"("internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_options"("internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_options"("internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_penalty"("internal", "internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_penalty"("internal", "internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_penalty"("internal", "internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_penalty"("internal", "internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_picksplit"("internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_picksplit"("internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_picksplit"("internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_picksplit"("internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_same"("public"."gtrgm", "public"."gtrgm", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_same"("public"."gtrgm", "public"."gtrgm", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_same"("public"."gtrgm", "public"."gtrgm", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_same"("public"."gtrgm", "public"."gtrgm", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."gtrgm_union"("internal", "internal") TO "postgres";
GRANT ALL ON FUNCTION "public"."gtrgm_union"("internal", "internal") TO "anon";
GRANT ALL ON FUNCTION "public"."gtrgm_union"("internal", "internal") TO "authenticated";
GRANT ALL ON FUNCTION "public"."gtrgm_union"("internal", "internal") TO "service_role";



GRANT ALL ON FUNCTION "public"."is_business_member"("p_business_id" "uuid") TO "anon";
GRANT ALL ON FUNCTION "public"."is_business_member"("p_business_id" "uuid") TO "authenticated";
GRANT ALL ON FUNCTION "public"."is_business_member"("p_business_id" "uuid") TO "service_role";



GRANT ALL ON FUNCTION "public"."list_types"("p_business_id" "uuid") TO "anon";
GRANT ALL ON FUNCTION "public"."list_types"("p_business_id" "uuid") TO "authenticated";
GRANT ALL ON FUNCTION "public"."list_types"("p_business_id" "uuid") TO "service_role";



GRANT ALL ON FUNCTION "public"."list_zones"("p_business_id" "uuid") TO "anon";
GRANT ALL ON FUNCTION "public"."list_zones"("p_business_id" "uuid") TO "authenticated";
GRANT ALL ON FUNCTION "public"."list_zones"("p_business_id" "uuid") TO "service_role";



GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "anon";
GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "authenticated";
GRANT ALL ON FUNCTION "public"."rls_auto_enable"() TO "service_role";



GRANT ALL ON FUNCTION "public"."search_properties"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_min_bathrooms" integer, "p_min_price" numeric, "p_max_price" numeric, "p_currency" "text", "p_min_m2" numeric, "p_max_m2" numeric, "p_sort" "text", "p_period" "text", "p_limit" integer, "p_offset" integer) TO "anon";
GRANT ALL ON FUNCTION "public"."search_properties"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_min_bathrooms" integer, "p_min_price" numeric, "p_max_price" numeric, "p_currency" "text", "p_min_m2" numeric, "p_max_m2" numeric, "p_sort" "text", "p_period" "text", "p_limit" integer, "p_offset" integer) TO "authenticated";
GRANT ALL ON FUNCTION "public"."search_properties"("p_business_id" "uuid", "p_operation" "text", "p_zone" "text", "p_type" "text", "p_min_bedrooms" integer, "p_max_bedrooms" integer, "p_min_bathrooms" integer, "p_min_price" numeric, "p_max_price" numeric, "p_currency" "text", "p_min_m2" numeric, "p_max_m2" numeric, "p_sort" "text", "p_period" "text", "p_limit" integer, "p_offset" integer) TO "service_role";



REVOKE ALL ON FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text") FROM PUBLIC;
GRANT ALL ON FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text") TO "anon";
GRANT ALL ON FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."set_bot_enabled"("p_business_id" "uuid", "p_phone" "text", "p_enabled" boolean, "p_reason" "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."set_limit"(real) TO "postgres";
GRANT ALL ON FUNCTION "public"."set_limit"(real) TO "anon";
GRANT ALL ON FUNCTION "public"."set_limit"(real) TO "authenticated";
GRANT ALL ON FUNCTION "public"."set_limit"(real) TO "service_role";



GRANT ALL ON FUNCTION "public"."set_updated_at"() TO "anon";
GRANT ALL ON FUNCTION "public"."set_updated_at"() TO "authenticated";
GRANT ALL ON FUNCTION "public"."set_updated_at"() TO "service_role";



GRANT ALL ON FUNCTION "public"."show_limit"() TO "postgres";
GRANT ALL ON FUNCTION "public"."show_limit"() TO "anon";
GRANT ALL ON FUNCTION "public"."show_limit"() TO "authenticated";
GRANT ALL ON FUNCTION "public"."show_limit"() TO "service_role";



GRANT ALL ON FUNCTION "public"."show_trgm"("text") TO "postgres";
GRANT ALL ON FUNCTION "public"."show_trgm"("text") TO "anon";
GRANT ALL ON FUNCTION "public"."show_trgm"("text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."show_trgm"("text") TO "service_role";



GRANT ALL ON FUNCTION "public"."similarity"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."similarity"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."similarity"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."similarity"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."similarity_dist"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."similarity_dist"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."similarity_dist"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."similarity_dist"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."similarity_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."similarity_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."similarity_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."similarity_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."strict_word_similarity"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."strict_word_similarity"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."strict_word_similarity"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."strict_word_similarity"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."strict_word_similarity_commutator_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_commutator_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_commutator_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_commutator_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_commutator_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_commutator_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_commutator_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_commutator_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_dist_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."strict_word_similarity_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."strict_word_similarity_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."word_similarity"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."word_similarity"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."word_similarity"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."word_similarity"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."word_similarity_commutator_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."word_similarity_commutator_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."word_similarity_commutator_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."word_similarity_commutator_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."word_similarity_dist_commutator_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_commutator_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_commutator_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_commutator_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."word_similarity_dist_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."word_similarity_dist_op"("text", "text") TO "service_role";



GRANT ALL ON FUNCTION "public"."word_similarity_op"("text", "text") TO "postgres";
GRANT ALL ON FUNCTION "public"."word_similarity_op"("text", "text") TO "anon";
GRANT ALL ON FUNCTION "public"."word_similarity_op"("text", "text") TO "authenticated";
GRANT ALL ON FUNCTION "public"."word_similarity_op"("text", "text") TO "service_role";


















GRANT ALL ON TABLE "public"."business_memberships" TO "anon";
GRANT ALL ON TABLE "public"."business_memberships" TO "authenticated";
GRANT ALL ON TABLE "public"."business_memberships" TO "service_role";



GRANT ALL ON TABLE "public"."business_whatsapp_numbers" TO "anon";
GRANT ALL ON TABLE "public"."business_whatsapp_numbers" TO "authenticated";
GRANT ALL ON TABLE "public"."business_whatsapp_numbers" TO "service_role";



GRANT ALL ON TABLE "public"."businesses" TO "anon";
GRANT ALL ON TABLE "public"."businesses" TO "authenticated";
GRANT ALL ON TABLE "public"."businesses" TO "service_role";



GRANT ALL ON TABLE "public"."conversations" TO "anon";
GRANT ALL ON TABLE "public"."conversations" TO "authenticated";
GRANT ALL ON TABLE "public"."conversations" TO "service_role";



GRANT ALL ON TABLE "public"."leads" TO "anon";
GRANT ALL ON TABLE "public"."leads" TO "authenticated";
GRANT ALL ON TABLE "public"."leads" TO "service_role";



GRANT ALL ON TABLE "public"."message_buffer" TO "anon";
GRANT ALL ON TABLE "public"."message_buffer" TO "authenticated";
GRANT ALL ON TABLE "public"."message_buffer" TO "service_role";



GRANT ALL ON SEQUENCE "public"."message_buffer_id_seq" TO "anon";
GRANT ALL ON SEQUENCE "public"."message_buffer_id_seq" TO "authenticated";
GRANT ALL ON SEQUENCE "public"."message_buffer_id_seq" TO "service_role";



GRANT ALL ON TABLE "public"."properties" TO "anon";
GRANT ALL ON TABLE "public"."properties" TO "authenticated";
GRANT ALL ON TABLE "public"."properties" TO "service_role";



GRANT ALL ON TABLE "public"."users" TO "anon";
GRANT ALL ON TABLE "public"."users" TO "authenticated";
GRANT ALL ON TABLE "public"."users" TO "service_role";



GRANT ALL ON TABLE "public"."zone_equivalences" TO "anon";
GRANT ALL ON TABLE "public"."zone_equivalences" TO "authenticated";
GRANT ALL ON TABLE "public"."zone_equivalences" TO "service_role";









ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON SEQUENCES TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON FUNCTIONS TO "service_role";






ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "postgres";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "anon";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "authenticated";
ALTER DEFAULT PRIVILEGES FOR ROLE "postgres" IN SCHEMA "public" GRANT ALL ON TABLES TO "service_role";



































