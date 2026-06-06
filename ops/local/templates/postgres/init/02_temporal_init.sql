-- Temporal requires two separate databases to avoid schema_version table conflicts:
-- temporal_db       → main workflow/history schema
-- temporal_visibility → visibility schema (executions_visibility table)
CREATE DATABASE temporal_db;
CREATE DATABASE temporal_visibility;

GRANT ALL PRIVILEGES ON DATABASE temporal_db TO htx_svc;
GRANT ALL PRIVILEGES ON DATABASE temporal_visibility TO htx_svc;
