# CallCadence Database Setup

This document explains how to set up the databases for CallCadence.

## Prerequisites

- SQL Server or SQL Server LocalDB installed
- .NET EF Core tools installed (`dotnet tool install --global dotnet-ef`)

## Database Configuration

CallCadence uses two separate databases:

### Development Environment
- **API Database**: `callcadencedev`
- **Hangfire Database**: `callcadencehangfiredev`

### Production Environment
- **API Database**: `callcadence`
- **Hangfire Database**: `callcadencehangfire`

## Connection Strings

Update connection strings in the appropriate configuration file:

### Development (`appsettings.json`)
```json
{
  "ConnectionStrings": {
    "CallCadenceDevDb": "Server=(localdb)\\mssqllocaldb;Database=callcadencedev;Trusted_Connection=True;MultipleActiveResultSets=true",
    "CallCadenceHangfireDevDb": "Server=(localdb)\\mssqllocaldb;Database=callcadencehangfiredev;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### Production (`appsettings.Production.json`)
```json
{
  "ConnectionStrings": {
    "CallCadenceDb": "Server=localhost;Database=callcadence;User Id=sa;Password=YourPassword;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "CallCadenceHangfireDb": "Server=localhost;Database=callcadencehangfire;User Id=sa;Password=YourPassword;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

**Note**: Update the production connection strings with your actual SQL Server credentials.

## Running Migrations

### From the solution root directory:

```bash
cd src/CallCadence.API
dotnet ef database update
```

This will:
1. Create the API database (callcadencedev or callcadence depending on environment)
2. Apply all migrations to create the necessary tables:
   - ApiCalls
   - ApiCallArchives
   - ApiCallLogs

### Hangfire Database

The Hangfire database will be automatically created and configured when you first run the application. Hangfire manages its own schema.

## Verify Database Creation

### Using SQL Server Management Studio (SSMS)

1. Connect to your SQL Server instance
2. Verify the following databases exist:
   - `callcadencedev` (or `callcadence` for production)
   - `callcadencehangfiredev` (or `callcadencehangfire` for production)

### Using sqlcmd

```bash
sqlcmd -S (localdb)\mssqllocaldb -Q "SELECT name FROM sys.databases WHERE name LIKE 'callcadence%'"
```

## Database Schema

### ApiCalls Table
- **Id** (uniqueidentifier, PK)
- **Title** (nvarchar(200), NOT NULL)
- **Description** (nvarchar(1000))
- **HttpMethod** (nvarchar(10), NOT NULL)
- **EndpointUrl** (nvarchar(2000), NOT NULL)
- **Payload** (nvarchar(MAX))
- **AuthenticationHeader** (nvarchar(500))
- **OtherHeaders** (nvarchar(2000))
- **IsActive** (bit, NOT NULL)
- **CreatedAt** (datetime2, NOT NULL)
- **ModifiedAt** (datetime2, NOT NULL)

### ApiCallArchives Table
- **Id** (uniqueidentifier, PK)
- **ApiCallId** (uniqueidentifier, NOT NULL)
- **Title** (nvarchar(200), NOT NULL)
- **Description** (nvarchar(1000))
- **HttpMethod** (nvarchar(10), NOT NULL)
- **EndpointUrl** (nvarchar(2000), NOT NULL)
- **Payload** (nvarchar(MAX))
- **AuthenticationHeader** (nvarchar(500))
- **OtherHeaders** (nvarchar(2000))
- **IsActive** (bit, NOT NULL)
- **ArchivedAt** (datetime2, NOT NULL)
- **OriginalCreatedAt** (datetime2, NOT NULL)
- **OriginalModifiedAt** (datetime2, NOT NULL)
- Index on ApiCallId

### ApiCallLogs Table
- **Id** (uniqueidentifier, PK)
- **ApiCallId** (uniqueidentifier, NOT NULL)
- **ResponseCode** (int, NOT NULL)
- **ResponseBody** (nvarchar(MAX))
- **ExecutedAt** (datetime2, NOT NULL)
- **DurationMs** (bigint, NOT NULL)
- **Success** (bit, NOT NULL)
- **ErrorMessage** (nvarchar(2000))
- Index on ApiCallId
- Index on ExecutedAt

## Troubleshooting

### Error: Cannot connect to LocalDB

Ensure SQL Server LocalDB is installed:
```bash
sqllocaldb info
```

If not installed, download from Microsoft.

### Error: Database already exists

If you need to recreate the database:
```bash
cd src/CallCadence.API
dotnet ef database drop
dotnet ef database update
```

### Permission Issues

Ensure your SQL Server user has sufficient permissions to:
- Create databases
- Create tables
- Insert/Update/Delete data

## Next Steps

After setting up the databases:
1. Run the application: `dotnet run --project src/CallCadence.API`
2. Access Swagger UI: `https://localhost:5001/swagger`
3. Access Hangfire Dashboard: `https://localhost:5001/hangfire`
