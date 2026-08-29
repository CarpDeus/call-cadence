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

### Docker deploy
You can find a sample docker-compose files here:
- **[docker-compose.yml](docker-compose.yml)** - Docker compose including SQL Server express 
- **[docker-compose-local-sql.yml](docker-compose-local-sql.yml)** - Docker compose using a seperate SQL Server

#### Environmental Variables
| Variable | Description |
| -------- | ----------- |
| SA_PASSWORD | This is the password created for the sa account if including SQL Server in the docker compose. |
| ASPNETCORE_ENVIRONMENT | The environment represented, defaults to Production. |
| SENTRY_DSN | URI for Sentry or Bugsink if you are using it to log bugs |
| UI_URI | The URI for the Call Cadence UI Server. |
| API_URI | The URI for the Call Cadence API Server. |
| CALLCADENCE_DB | The connection string for the Call Cadence database if you are using a SQL Server outside of the docker stack. |
| CALLCADENCE_HANGFIRE_DB | The connection string for the Call Cadence hangfire database if you are using a SQL Server outside of the docker stack. |


### Database Setup

The application uses SQL Server for the permanent data store for both API call storage and Hangfire. It is your choice to either one or two databases, however, hangfire requires the database to exist so using one database allows for cleaner docker deploys.

### Authentication Configuration (JWT)

Authentication uses **JWT bearer tokens** so the UI and API can be deployed on **separate domains and servers**. The API issues a signed JWT on login/registration (and after SSO sign-in), and the UI sends it as a `Bearer` token on all API and SignalR calls. This is configured in the docker-compose.


## Cron Expression Examples

CallCadence uses standard cron expressions for scheduling:

- `0 0 * * *` - Daily at midnight
- `0 */6 * * *` - Every 6 hours
- `*/15 * * * *` - Every 15 minutes
- `0 9 * * 1-5` - Weekdays at 9 AM
- `0 0 1 * *` - First day of every month at midnight

For more examples, visit [crontab.guru](https://crontab.guru/)

## Documentation

For detailed information, see:
- **[API-USAGE.md](API-USAGE.md)** - Complete API usage guide with examples
- **[ARCHITECTURE-FirstPass.md](ARCHITECTURE-FirstPass.md)** - Original architecture overview

## License

See the [LICENSE](LICENSE) file for details.

## Support

For issues and questions:
- GitHub Issues: [Report an issue](https://github.com/CarpDeus/call-cadence-private/issues)

---

Built with ❤️ using .NET Core, Hangfire, and MudBlazor.
