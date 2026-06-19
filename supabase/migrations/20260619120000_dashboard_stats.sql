-- Results-funnel stats for the dashboard "Estadísticas" page.
-- One call returns the whole payload (funnel + temperatura + zona + fuera-horario)
-- so the frontend never hand-rolls table selects for counts.
-- Tenancy: service_role bypasses; everyone else must be a business member.

create or replace function public.dashboard_stats(p_business_id uuid, p_from timestamptz default null, p_to timestamptz default null)
returns jsonb language plpgsql stable security definer set search_path to 'public' as $$
declare v_tz text; v_start time; v_end time; v jsonb;
begin
  if auth.role() <> 'service_role' and not public.is_business_member(p_business_id) then
    raise exception 'not authorized for business %', p_business_id using errcode='42501';
  end if;
  select coalesce(answer_tz,'America/Montevideo'), answer_start, answer_end
    into v_tz, v_start, v_end from public.businesses where id = p_business_id;
  with l as (
    select * from public.leads
    where business_id = p_business_id
      and (p_from is null or created_at >= p_from)
      and (p_to   is null or created_at <  p_to)
  )
  select jsonb_build_object(
    'total',         (select count(*) from l),
    'calificados',   (select count(*) from l where classification in ('tibio','caliente')),
    'prontos',       (select count(*) from l where ready_for_handoff),
    'caliente',      (select count(*) from l where classification='caliente'),
    'tibio',         (select count(*) from l where classification='tibio'),
    'frio',          (select count(*) from l where classification='frio'),
    'fuera_horario', (select count(*) from l where v_start is not null and v_end is not null
                        and (created_at at time zone v_tz)::time not between v_start and v_end),
    'por_zona',      (select coalesce(jsonb_agg(jsonb_build_object('zona',zona,'n',c) order by c desc),'[]'::jsonb)
                        from (select nullif(btrim(zone),'') zona, count(*) c from l
                              where nullif(btrim(zone),'') is not null group by 1 order by c desc limit 6) z),
    'por_operacion', (select coalesce(jsonb_agg(jsonb_build_object('operacion',operacion,'n',c) order by c desc),'[]'::jsonb)
                        from (select nullif(btrim(operation),'') operacion, count(*) c from l
                              where nullif(btrim(operation),'') is not null group by 1 order by c desc limit 6) o)
  ) into v; return v;
end; $$;

grant all on function public.dashboard_stats(uuid,timestamptz,timestamptz) to anon, authenticated, service_role;
