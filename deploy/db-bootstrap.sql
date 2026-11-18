-- Least-privilege runtime role for AvaluxAuth (H-07 / L-07).
--
-- Run once per database as the initdb superuser, from a shell that has
-- DB__APP_PASS in the environment:
--
--   PGPASSWORD="$DB__PASS" psql -h auth-postgres -U "$DB__USER" -d "$DB__NAME" \
--     -v ON_ERROR_STOP=1 -f deploy/db-bootstrap.sql
--
-- The role name and database name are fixed to this installation; adjust them
-- together with the stack environment if renamed.
\set ON_ERROR_STOP on
\getenv app_password DB__APP_PASS

CREATE ROLE avaluxauth_app LOGIN PASSWORD :'app_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

ALTER DATABASE avaluxauth OWNER TO avaluxauth_app;

-- REASSIGN OWNED BY <superuser> fails here ("objects ... required by the
-- database system") because the initdb superuser owns the system catalogs.
-- Reassign only the application objects in user schemas instead; indexes,
-- composite types and TOAST tables follow their tables automatically.
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT n.nspname, c.relname, c.relkind
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relowner = 'avaluxauth'::regrole
          AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'pg_toast')
          AND c.relkind IN ('r', 'p', 'v', 'm', 'S', 'f')
    LOOP
        EXECUTE format('ALTER %s %I.%I OWNER TO avaluxauth_app',
            CASE r.relkind
                WHEN 'r' THEN 'TABLE'
                WHEN 'p' THEN 'TABLE'
                WHEN 'v' THEN 'VIEW'
                WHEN 'm' THEN 'MATERIALIZED VIEW'
                WHEN 'S' THEN 'SEQUENCE'
                WHEN 'f' THEN 'FOREIGN TABLE'
            END,
            r.nspname, r.relname);
    END LOOP;
END $$;

ALTER SCHEMA public OWNER TO avaluxauth_app;
REVOKE CONNECT ON DATABASE avaluxauth FROM PUBLIC;
