-- Create single application database
CREATE DATABASE htx;

-- Create dedicated service accounts (one per application service)
CREATE USER hr_svc WITH PASSWORD '2da6321b81dc8fe3';
CREATE USER onboarding_svc WITH PASSWORD '557e1406b6c501e4';

-- Grant admin full access (used by Flyway migrations)
GRANT ALL PRIVILEGES ON DATABASE htx TO htx_svc;

-- Grant service accounts connect access to the shared database.
-- Schema-level isolation (GRANT USAGE per schema) is handled by Flyway V2 migrations
-- once the schemas exist.
GRANT CONNECT ON DATABASE htx TO hr_svc;
GRANT CONNECT ON DATABASE htx TO onboarding_svc;
