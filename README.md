# CallCadence - API Scheduler with Hangfire

CallCadence is a lightweight scheduler application built with .NET Core that uses Hangfire to run and manage timed API calls. Configure static endpoints, payloads, and authentication once and let CallCadence keep your integrations on beat.

## Architecture

This project is organized into three source projects:

```
src/
├── CallCadence.API/            # Web API + EF migrations + repositories + services
├── CallCadence.Models/         # Models shared by API and UI
└── CallCadence.UI/             # MudBlazor frontend project

tests/
├── CallCadence.UnitTests/        # NUnit unit tests
└── CallCadence.IntegrationTests/ # NUnit integration tests
```

## Features

- ✅ RESTful API for managing API call definitions
- ✅ Hangfire-powered job scheduling with cron expressions
- ✅ Support for various HTTP methods (GET, POST, PUT, DELETE, etc.)
- ✅ Custom payload and authentication header support
- ✅ Hangfire Dashboard for monitoring scheduled jobs
- ✅ Simplified project structure with dependency injection
- ✅ SQL Server database storage for API calls and execution logs
- ✅ Automatic archiving of API call modifications
- ✅ Comprehensive execution logging

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- SQL Server (LocalDB for development, SQL Server for production)
- Your favorite IDE (Visual Studio, VS Code, Rider)

### Database Setup

The application uses separate databases for API call storage and Hangfire:

**Development:**
- API Calls: `callcadencedev`
- Hangfire: `callcadencehangfiredev`

**Production:**
- API Calls: `callcadence`
- Hangfire: `callcadencehangfire`

Update the connection strings in `appsettings.json` (development) or `appsettings.Production.json` (production) as needed.

### Authentication Configuration (JWT)

Authentication uses **JWT bearer tokens** so the UI and API can be deployed on **separate domains and servers**. The API issues a signed JWT on login/registration (and after SSO sign-in), and the UI sends it as a `Bearer` token on all API and SignalR calls. Configure the following settings in the API's `appsettings.json` (or via environment variables).

#### API settings (`src/CallCadence.API/appsettings.json`)

```json
{
  "Jwt": {
    "SigningKey": "",
    "Issuer": "CallCadence.API",
    "Audience": "CallCadence.UI",
    "ExpiryMinutes": 60
  },
  "AllowedUiReturnUrls": []
}
```

| Setting | Description |
| --- | --- |
| `Jwt:SigningKey` | **Required.** Secret key used to sign and validate tokens (HMAC-SHA256). Use a strong value of **at least 32 characters**. Leave empty in source control and supply it per environment. |
| `Jwt:Issuer` | Token issuer. Must match between token generation and validation. |
| `Jwt:Audience` | Intended token audience (the UI). Must match between generation and validation. |
| `Jwt:ExpiryMinutes` | Token lifetime in minutes (default `60`). |
| `AllowedUiReturnUrls` | Allowlist of UI origins the API may redirect to after SSO sign-in (open-redirect protection). Add each UI base URL, e.g. `"https://app.example.com"`. |

#### UI settings (`src/CallCadence.UI/appsettings.json`)

| Setting | Description |
| --- | --- |
| `Api:BaseUrl` | **Required.** Public base URL of the API the UI calls, e.g. `"https://api.example.com"`. |

#### Environment variable overrides

All settings can be overridden with environment variables using the standard double-underscore syntax (recommended for production secrets):

```bash
Jwt__SigningKey="<32+ character secret>"
Jwt__Issuer="CallCadence.API"
Jwt__Audience="CallCadence.UI"
Jwt__ExpiryMinutes="60"
Api__BaseUrl="https://api.example.com"

# AllowedUiReturnUrls accepts either indexed array entries...
AllowedUiReturnUrls__0="https://app.example.com"
AllowedUiReturnUrls__1="https://admin.example.com"

# ...or a single comma/semicolon-delimited string (easier for Docker/Kubernetes)
AllowedUiReturnUrls="https://app.example.com,https://admin.example.com"
```

`AllowedUiReturnUrls` supports both forms so container deployments can supply the allowlist through a single environment variable. When both are present, the indexed array takes precedence. Entries are trimmed of trailing slashes and compared case-insensitively.

Example `docker-compose.yml` snippet for the API service:

```yaml
services:
  api:
    image: callcadence-api
    environment:
      ConnectionStrings__CallCadenceDb: "Server=db;Database=callcadence;..."
      ConnectionStrings__CallCadenceHangfireDb: "Server=db;Database=callcadencehangfire;..."
      Jwt__SigningKey: "${JWT_SIGNING_KEY}"
      Jwt__Issuer: "CallCadence.API"
      Jwt__Audience: "CallCadence.UI"
      Jwt__ExpiryMinutes: "60"
      AllowedUiReturnUrls: "https://app.example.com,https://admin.example.com"
```

> **Note:** `appsettings.Development.json` ships with a development-only signing key and localhost return URLs so the app runs locally out of the box. Never reuse the development key in production.


### Running Migrations

```bash
cd src/CallCadence.Infrastructure
dotnet ef database update --project ./CallCadence.API
```

### Building the Project

```bash
# Restore dependencies and build
dotnet build

# Run tests
dotnet test
```

### Running the Application

```bash
cd src/CallCadence.API
dotnet run
```

The API will be available at `https://localhost:5001` (or the port shown in console).

### Hangfire Dashboard

Once the application is running, access the Hangfire Dashboard at:
```
https://localhost:5001/hangfire
```

The dashboard provides real-time monitoring of:
- Scheduled jobs
- Recurring jobs
- Job execution history
- Failed jobs and retries

## API Endpoints

### Base URL
`https://localhost:5001/api`

### API Call Management Endpoints

#### Get All API Calls
```http
GET /api/ApiCallManagement
```

#### Get API Call by ID
```http
GET /api/ApiCallManagement/{id}
```

#### Create API Call
```http
POST /api/ApiCallManagement
Content-Type: application/json

{
  "title": "Daily Health Check",
  "description": "Checks API health daily",
  "httpMethod": "GET",
  "endpointUrl": "https://api.example.com/health",
  "isActive": true,
  "authenticationHeader": "Bearer your-token-here",
  "payload": null,
  "otherHeaders": "X-Custom-Header:value"
}
```

#### Update API Call
```http
PUT /api/ApiCallManagement/{id}
Content-Type: application/json

{
  "id": "guid-here",
  "title": "Updated Name",
  "description": "Updated description",
  "httpMethod": "POST",
  "endpointUrl": "https://api.example.com/endpoint",
  "isActive": true,
  "authenticationHeader": "Bearer new-token",
  "payload": "{\"key\":\"value\"}",
  "otherHeaders": "X-Custom:value1;X-Another:value2"
}
```

#### Delete API Call
```http
DELETE /api/ApiCallManagement/{id}
```

### Scheduling Endpoints

#### Schedule an API Call
```http
POST /api/ApiCallScheduling
Content-Type: application/json

{
  "apiCallId": "guid-of-api-call",
  "cronExpression": "0 0 * * *"
}
```

#### Remove a Schedule
```http
DELETE /api/ApiCallScheduling/{jobId}
```

#### Get All Execution Logs
```http
GET /api/ApiCallScheduling/logs
```

#### Get Logs for Specific API Call
```http
GET /api/ApiCallScheduling/logs/{apiCallId}
```

## Cron Expression Examples

CallCadence uses standard cron expressions for scheduling:

- `0 0 * * *` - Daily at midnight
- `0 */6 * * *` - Every 6 hours
- `*/15 * * * *` - Every 15 minutes
- `0 9 * * 1-5` - Weekdays at 9 AM
- `0 0 1 * *` - First day of every month at midnight

For more examples, visit [crontab.guru](https://crontab.guru/)

## Technology Stack

- **Framework**: .NET 10.0
- **Scheduler**: Hangfire 1.8.21
- **Database**: Entity Framework Core 10.0 with SQL Server
- **API Documentation**: Swagger/OpenAPI
- **Testing**: NUnit 4.2.2
- **Mocking**: Moq 4.20.72
- **Assertions**: FluentAssertions 8.7.1

## Project Structure

The project is split into:

### CallCadence.API
Contains:
- `ApiCall` - Main entity for API call definitions
- `ApiCallArchive` - Archived versions of modified API calls
- `ApiCallLog` - Execution logs for API calls
- Repository interfaces
- `ApiCallManagementService` - Service for managing API calls
- `CallCadenceDbContext` - Entity Framework DbContext
- Repository implementations
- `CallApiService` - Executes API calls and logs results
- Hangfire configuration
- API controllers and startup configuration

### CallCadence.Models
Contains DTOs and request/response models shared by API and UI.

### CallCadence.UI
Contains a MudBlazor frontend project scaffold.

## Database Schema

### ApiCalls Table
Stores API call definitions with fields for:
- Title, Description
- HTTP Method, Endpoint URL
- Payload, Authentication Header, Other Headers
- Active flag
- Created/Modified timestamps

### ApiCallArchives Table
Stores historical versions of API calls whenever they are modified.

### ApiCallLogs Table
Logs each execution with:
- Response code and body
- Execution timestamp
- Duration in milliseconds
- Success flag
- Error messages (if any)

## Documentation

For detailed information, see:
- **[DATABASE-SETUP.md](DATABASE-SETUP.md)** - Database configuration and setup instructions
- **[API-USAGE.md](API-USAGE.md)** - Complete API usage guide with examples
- **[ARCHITECTURE-FirstPass.md](ARCHITECTURE-FirstPass.md)** - Original architecture overview

## License

See the [LICENSE](LICENSE) file for details.

## Support

For issues and questions:
- GitHub Issues: [Report an issue](https://github.com/CarpDeus/call-cadence-private/issues)

---

Built with ❤️ using .NET Core, Hangfire, and MudBlazor.
