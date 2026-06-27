-- Provision undo for PostgreSQL.
-- Runs through the admin connection when `b8agrate unprovision` is executed.
-- Keep this paired with P__*.sql and make it safe to rerun where possible.

-- Example:
-- DROP DATABASE your_database;
--
-- DROP ROLE your_app_user;
