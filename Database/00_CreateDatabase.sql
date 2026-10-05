USE master
GO

IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name = 'EventTicketing')
BEGIN
	CREATE DATABASE [EventTicketing]
END
GO