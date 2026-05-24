-- SQL Server initialisation script
-- Creates the four application databases if they do not already exist.
-- Executed on container first start via the docker-entrypoint-initdb mechanism.

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'UserDb')
    CREATE DATABASE UserDb;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'OrderDb')
    CREATE DATABASE OrderDb;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'PaymentDb')
    CREATE DATABASE PaymentDb;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'NotificationDb')
    CREATE DATABASE NotificationDb;
GO

PRINT 'All databases initialised.';
