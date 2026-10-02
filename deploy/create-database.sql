-- Aibysitter database and logins. Run once as a sysadmin on the default instance (MSSQLSERVER).
-- Set @RunnerAccount, in both batches, to the Windows account the GitHub Actions runner service runs as.
-- Safe to re-run: existing objects are left as they are.

DECLARE @RunnerAccount sysname = N'::RUNNER_ACCOUNT::';
DECLARE @AppPoolAccount sysname = N'IIS APPPOOL\aibysitting.net';

IF @RunnerAccount LIKE N'::%' THROW 50000, N'Set @RunnerAccount before running this script.', 1;

IF DB_ID(N'Aibysitter') IS NULL CREATE DATABASE [Aibysitter];

IF SUSER_ID(@RunnerAccount) IS NULL EXEC (N'CREATE LOGIN ' + QUOTENAME(@RunnerAccount) + N' FROM WINDOWS;');
IF SUSER_ID(@AppPoolAccount) IS NULL EXEC (N'CREATE LOGIN ' + QUOTENAME(@AppPoolAccount) + N' FROM WINDOWS;');
GO

USE [Aibysitter];
GO

DECLARE @RunnerAccount sysname = N'::RUNNER_ACCOUNT::';
DECLARE @AppPoolAccount sysname = N'IIS APPPOOL\aibysitting.net';

IF @RunnerAccount LIKE N'::%' THROW 50000, N'Set @RunnerAccount before running this script.', 1;

-- Runner: applies migrations (owns the schema).
IF USER_ID(@RunnerAccount) IS NULL EXEC (N'CREATE USER ' + QUOTENAME(@RunnerAccount) + N' FOR LOGIN ' + QUOTENAME(@RunnerAccount) + N';');
EXEC (N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@RunnerAccount) + N';');

-- App pool: reads and writes rows only.
IF USER_ID(@AppPoolAccount) IS NULL EXEC (N'CREATE USER ' + QUOTENAME(@AppPoolAccount) + N' FOR LOGIN ' + QUOTENAME(@AppPoolAccount) + N';');
EXEC (N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@AppPoolAccount) + N';');
EXEC (N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@AppPoolAccount) + N';');
GO
