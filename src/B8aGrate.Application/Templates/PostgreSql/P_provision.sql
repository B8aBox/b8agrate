-- Provision bootstrap for PostgreSQL.
-- Runs through the admin connection before versioned and repeatable migrations.
-- Keep this script idempotent; only one P__*.sql script is allowed.

-- Example:
-- CREATE ROLE your_app_user LOGIN PASSWORD 'change-me';
--
-- CREATE DATABASE your_database OWNER your_app_user;
