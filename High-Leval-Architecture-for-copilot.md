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

## Overview

### Permanent Storage 

#### ApiCall Definition
This project allows you to define an API call which is stored as JSON in a database and referenced by a GUID. That API call includes:
* the type of HTTP Method (GET, POST, PUT, DELETE, etc)
* the endpoint URL
* optional payload (for POST/PUT)
* optional authentication headers
* optional other headers

The following additional fields are stored in the database with eeach API call:
* GuidID for reference 
* Title of the API Call
* Description of the API Call
* Active flag
* date created
* date modified

There will be an ApiCallArchive table which stores previous versions of the API call whenever it is modified.

There will also be an ApiCallLog table which stores each time the API call is executed, along with the response code, response body, date/time executed, and duration.

### Services

#### CallApiService
This service will be a static class with a static method `ExecuteApiCall(ApiCall apiCall)` which takes an ApiCall object as input. This will be called by Hangfire. It will:
* Get the ApiCall object from the database using the provided GUID 
** the ApiCall must be active, otherwise log an error and exit
* Construct the HTTP request using HttpClient
* Send the HTTP request and await the response
* Log the response code, response body, date/time executed, and duration to the ApiCallLog table

#### ApiCallManagementService
This service will provide methods to create and update ApiCalls in the database. 

### Controllers

#### ApiCallManagementController
This controller will provide RESTful endpoints to manage ApiCalls:

#### ApiCallSchedulingController
This controller will provide RESTful endpoints to schedule ApiCalls using Hangfire:
* POST /api/schedule - Schedule an ApiCall by providing the GUID and cron expression
* DELETE /api/schedule/{jobId} - Remove a scheduled ApiCall by job ID
* GET /api/schedule - List all scheduled ApiCalls with their job IDs and cron expressions
** should show any ApiCall objects that are not active in the permanent storage
* GET /api/schedule/{jobId} - Get details of a specific scheduled ApiCall by job ID
* GET /api/schedule/logs - Get execution logs for all ApiCalls
* GET /api/schedule/logs/{apiCallId} - Get execution logs for a specific ApiCall by GUID

### Other Considerations

* Use dependency injection to manage services and repositories
* Use configuration to manage Hangfire settings 
* Support Hangfire Dashboard for monitoring scheduled jobs
* Support the following for permanent storage of ApiCall definitions 
  * SQLite 
  * SQL Server 
  * Postgresql
  * Mysql
* Hangfire storage options 
  * In-memory (for development/testing)
  * SQL Server
  * Postgresql
  * Redis

