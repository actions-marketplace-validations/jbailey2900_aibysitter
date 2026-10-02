-- Aibysitter database and logins. Run once as a sysadmin on the default instance (MSSQLSERVER).
-- Set @RunnerAccount to the Windows account the GitHub Actions runner service runs as.
-- Safe to re-run: existing objects and role memberships are left as they are.
-- Names are quoted with QUOTENAME into @sql and run with sp_executesql; QUOTENAME cannot sit inline in DDL.

SET NOCOUNT ON;

DECLARE @Database sysname = N'Aibysitter';
DECLARE @RunnerAccount sysname = N'::RUNNER_ACCOUNT::';
DECLARE @AppPoolAccount sysname = N'IIS APPPOOL\aibysitting.net';
DECLARE @LoginSource nvarchar(200) = N'FROM WINDOWS';

IF @RunnerAccount LIKE N'::%'
    THROW 50000, N'Set @RunnerAccount before running this script.', 1;

DECLARE @sql nvarchar(max);

IF DB_ID(@Database) IS NULL
BEGIN
    SET @sql = N'CREATE DATABASE ' + QUOTENAME(@Database) + N';';
    EXEC sys.sp_executesql @sql;
END;

IF SUSER_ID(@RunnerAccount) IS NULL
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@RunnerAccount) + N' ' + @LoginSource + N';';
    EXEC sys.sp_executesql @sql;
END;

IF SUSER_ID(@AppPoolAccount) IS NULL
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@AppPoolAccount) + N' ' + @LoginSource + N';';
    EXEC sys.sp_executesql @sql;
END;

-- Runner: db_owner, applies migrations. App pool: db_datareader and db_datawriter.
SET @sql = N'USE ' + QUOTENAME(@Database) + N';
IF USER_ID(@runner) IS NULL CREATE USER ' + QUOTENAME(@RunnerAccount) + N' FOR LOGIN ' + QUOTENAME(@RunnerAccount) + N';
IF ISNULL(IS_ROLEMEMBER(N''db_owner'', @runner), 0) = 0 ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@RunnerAccount) + N';
IF USER_ID(@app) IS NULL CREATE USER ' + QUOTENAME(@AppPoolAccount) + N' FOR LOGIN ' + QUOTENAME(@AppPoolAccount) + N';
IF ISNULL(IS_ROLEMEMBER(N''db_datareader'', @app), 0) = 0 ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@AppPoolAccount) + N';
IF ISNULL(IS_ROLEMEMBER(N''db_datawriter'', @app), 0) = 0 ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@AppPoolAccount) + N';';
EXEC sys.sp_executesql @sql, N'@runner sysname, @app sysname', @runner = @RunnerAccount, @app = @AppPoolAccount;

PRINT N'Database ' + QUOTENAME(@Database) + N' ready: ' + QUOTENAME(@RunnerAccount) + N' db_owner; ' + QUOTENAME(@AppPoolAccount) + N' db_datareader, db_datawriter.';
