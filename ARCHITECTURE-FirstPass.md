# CallCadence - API Scheduler with Hangfire

CallCadence is a lightweight scheduler application built with .NET Core that uses Hangfire to run and manage timed API calls. Configure static endpoints, payloads, and authentication once and let CallCadence keep your integrations on beat.

## Architecture

This project follows **Clean Architecture** principles with clear separation of concerns:

```
src/
├── CallCadence.Domain/         # Core domain entities and interfaces
├── CallCadence.Application/    # Business logic and services
├── CallCadence.Infrastructure/ # Hangfire implementation and repositories
└── CallCadence.API/           # Web API controllers and configuration

tests/
├── CallCadence.UnitTests/        # NUnit unit tests
└── CallCadence.IntegrationTests/ # NUnit integration tests
```

### Layer Responsibilities

- **Domain Layer**: Contains core entities (`ScheduledCall`) and interfaces without external dependencies
- **Application Layer**: Business logic for managing scheduled calls via DTOs and services
- **Infrastructure Layer**: Implements Hangfire scheduling, in-memory repository, and API execution
- **API Layer**: ASP.NET Core Web API with RESTful endpoints

## Features

- ✅ RESTful API for managing scheduled API calls
- ✅ Hangfire-powered job scheduling with cron expressions
- ✅ Support for various HTTP methods (GET, POST, PUT, DELETE, etc.)
- ✅ Custom payload and authentication header support
- ✅ Hangfire Dashboard for monitoring scheduled jobs
- ✅ Clean Architecture with dependency injection
- ✅ NUnit test coverage (unit and integration tests)
- ✅ In-memory storage (easily replaceable with database)

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Your favorite IDE (Visual Studio, VS Code, Rider)

### Building the Project

```bash
# Clone the repository
git clone https://github.com/CarpDeus/call-cadence-private.git
cd call-cadence-private

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

#### Admin access from the Blazor UI

The dashboard is restricted to authenticated administrators via `HangfireAuthorizationFilter`.
Because the Blazor UI (`CallCadence.UI`) authenticates against the API with a JWT held in the
Blazor circuit, a full-page browser navigation to `/hangfire` would not carry that token. To
bridge this, the UI's nav menu shows an admin-only **"Hangfire Dashboard"** link that opens a
new tab to the API's handshake endpoint:

```
{Api:BaseUrl}/hangfire/login?access_token={jwt}
```

The `GET /hangfire/login` endpoint validates the JWT (same issuer/audience/signing key as the
JWT bearer scheme), confirms the `Admin` role, then issues a short-lived, HttpOnly cookie
(the dedicated `Hangfire` cookie scheme, scoped to `/hangfire`, 30-minute sliding expiry) and
redirects to `/hangfire`. A path-scoped middleware authenticates that cookie for `/hangfire*`
requests and populates `HttpContext.User`, so the existing `HangfireAuthorizationFilter`
authorizes the request unchanged.

> Security note: the JWT appears once in the handshake URL (browser history and API request
> logs). Tokens are short-lived (`Jwt:ExpiryMinutes`), and the dashboard itself runs on the
> HttpOnly cookie. The handshake redirects only to the fixed local `/hangfire` path to prevent
> open redirects.


## API Endpoints

### Base URL
`https://localhost:5001/api/scheduledcalls`

### Endpoints

#### Get All Scheduled Calls
```http
GET /api/scheduledcalls
```

#### Get Scheduled Call by ID
```http
GET /api/scheduledcalls/{id}
```

#### Create Scheduled Call
```http
POST /api/scheduledcalls
Content-Type: application/json

{
  "name": "Daily Health Check",
  "endpoint": "https://api.example.com/health",
  "httpMethod": "GET",
  "cronExpression": "0 0 * * *",
  "isActive": true,
  "authenticationHeader": "Bearer your-token-here",
  "payload": null
}
```

#### Update Scheduled Call
```http
PUT /api/scheduledcalls/{id}
Content-Type: application/json

{
  "id": "guid-here",
  "name": "Updated Name",
  "endpoint": "https://api.example.com/endpoint",
  "httpMethod": "POST",
  "cronExpression": "0 */6 * * *",
  "isActive": true,
  "authenticationHeader": "Bearer new-token",
  "payload": "{\"key\":\"value\"}"
}
```

#### Delete Scheduled Call
```http
DELETE /api/scheduledcalls/{id}
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
- **Storage**: Hangfire.MemoryStorage (in-memory, can be replaced with SQL/Redis)
- **Testing**: NUnit 4.2.2
- **Mocking**: Moq 4.20.72
- **Assertions**: FluentAssertions 8.7.1
- **Integration Testing**: Microsoft.AspNetCore.Mvc.Testing 10.0.0

## Project Structure

```
CallCadence/
├── src/
│   ├── CallCadence.Domain/
│   │   ├── Entities/
│   │   │   └── ScheduledCall.cs
│   │   └── Interfaces/
│   │       ├── IScheduledCallRepository.cs
│   │       └── ISchedulerService.cs
│   │
│   ├── CallCadence.Application/
│   │   ├── DTOs/
│   │   │   ├── CreateScheduledCallDto.cs
│   │   │   ├── UpdateScheduledCallDto.cs
│   │   │   └── ScheduledCallDto.cs
│   │   └── Services/
│   │       └── ScheduledCallService.cs
│   │
│   ├── CallCadence.Infrastructure/
│   │   ├── Jobs/
│   │   │   └── ApiCallJob.cs
│   │   ├── Repositories/
│   │   │   └── InMemoryScheduledCallRepository.cs
│   │   └── Services/
│   │       └── HangfireSchedulerService.cs
│   │
│   └── CallCadence.API/
│       ├── Controllers/
│       │   └── ScheduledCallsController.cs
│       └── Program.cs
│
└── tests/
    ├── CallCadence.UnitTests/
    │   ├── Repositories/
    │   │   └── InMemoryScheduledCallRepositoryTests.cs
    │   └── Services/
    │       └── ScheduledCallServiceTests.cs
    │
    └── CallCadence.IntegrationTests/
        └── Controllers/
            └── ScheduledCallsControllerTests.cs
```

## Testing

The project includes comprehensive test coverage:

### Unit Tests (17 tests)
- Repository tests for CRUD operations
- Service layer tests with mocked dependencies
- Validation of business logic

### Integration Tests (5 tests)
- End-to-end API testing
- Full application stack testing
- HTTP request/response validation

Run all tests:
```bash
dotnet test
```

Run with verbose output:
```bash
dotnet test --verbosity normal
```

## Extending the Application

### Adding Database Persistence

Replace `InMemoryScheduledCallRepository` with a database implementation:

1. Add Entity Framework Core packages
2. Create a `DbContext` with `ScheduledCall` entity
3. Implement `IScheduledCallRepository` with EF Core
4. Update dependency injection in `Program.cs`

### Adding Authentication

1. Install `Microsoft.AspNetCore.Authentication.JwtBearer`
2. Configure JWT authentication in `Program.cs`
3. Add `[Authorize]` attributes to controllers

### Custom Job Storage

Replace MemoryStorage with persistent storage:

```csharp
// For SQL Server
builder.Services.AddHangfire(config => config
    .UseSqlServerStorage("connection-string"));

// For Redis
builder.Services.AddHangfire(config => config
    .UseRedisStorage("redis-connection"));
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. Follow the existing clean architecture structure
2. Write tests for new features
3. Update documentation
4. Follow C# coding conventions

## License

See the [LICENSE](LICENSE) file for details.

## Support

For issues and questions:
- GitHub Issues: [Report an issue](https://github.com/CarpDeus/call-cadence-private/issues)
- Email: Contact repository owner

---

Built with ❤️ using .NET Core, Hangfire, and Clean Architecture principles.
