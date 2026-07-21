# CallCadence API Usage Guide

This guide provides examples of how to use the CallCadence API to manage and schedule API calls.

## Base URL

Development: `https://localhost:5001`

All API endpoints are under `/api`.

## Authentication

Currently, the API does not require authentication. For production use, implement authentication middleware.

## API Management Endpoints

### 1. Create an API Call

Create a new API call definition that can be scheduled later.

**Endpoint:** `POST /api/ApiCallManagement`

**Request Body:**
```json
{
  "title": "Daily Weather Check",
  "description": "Fetches weather data every day",
  "httpMethod": "GET",
  "endpointUrl": "https://api.openweathermap.org/data/2.5/weather?q=London&appid=YOUR_API_KEY",
  "isActive": true,
  "authenticationHeader": null,
  "payload": null,
  "otherHeaders": "Accept:application/json"
}
```

**Response:** `201 Created`
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Daily Weather Check",
  "description": "Fetches weather data every day",
  "httpMethod": "GET",
  "endpointUrl": "https://api.openweathermap.org/data/2.5/weather?q=London&appid=YOUR_API_KEY",
  "payload": null,
  "authenticationHeader": null,
  "otherHeaders": "Accept:application/json",
  "isActive": true,
  "createdAt": "2025-10-19T10:30:00Z",
  "modifiedAt": "2025-10-19T10:30:00Z"
}
```

### 2. Get All API Calls

Retrieve all API call definitions.

**Endpoint:** `GET /api/ApiCallManagement`

**Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Daily Weather Check",
    "description": "Fetches weather data every day",
    "httpMethod": "GET",
    "endpointUrl": "https://api.openweathermap.org/data/2.5/weather?q=London&appid=YOUR_API_KEY",
    "payload": null,
    "authenticationHeader": null,
    "otherHeaders": "Accept:application/json",
    "isActive": true,
    "createdAt": "2025-10-19T10:30:00Z",
    "modifiedAt": "2025-10-19T10:30:00Z"
  }
]
```

### 3. Get API Call by ID

Retrieve a specific API call definition.

**Endpoint:** `GET /api/ApiCallManagement/{id}`

**Response:** `200 OK` (same structure as Create response)

**Response:** `404 Not Found` if ID doesn't exist

### 4. Update an API Call

Update an existing API call definition. The previous version is automatically archived.

**Endpoint:** `PUT /api/ApiCallManagement/{id}`

**Request Body:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Updated Weather Check",
  "description": "Fetches weather data with new parameters",
  "httpMethod": "GET",
  "endpointUrl": "https://api.openweathermap.org/data/2.5/weather?q=Paris&appid=YOUR_API_KEY",
  "isActive": true,
  "authenticationHeader": null,
  "payload": null,
  "otherHeaders": "Accept:application/json"
}
```

**Response:** `200 OK` (updated API call)

**Response:** `404 Not Found` if ID doesn't exist

**Response:** `400 Bad Request` if ID in URL doesn't match ID in body

### 5. Delete an API Call

Delete an API call definition. This does not affect archived versions or execution logs.

**Endpoint:** `DELETE /api/ApiCallManagement/{id}`

**Response:** `204 No Content`

## Scheduling Endpoints

### 1. Schedule an API Call

Create a recurring schedule for an API call using a cron expression.

**Endpoint:** `POST /api/ApiCallScheduling`

**Request Body:**
```json
{
  "apiCallId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cronExpression": "0 */6 * * *"
}
```

**Cron Expression Examples:**
- `0 0 * * *` - Daily at midnight
- `0 */6 * * *` - Every 6 hours
- `*/15 * * * *` - Every 15 minutes
- `0 9 * * 1-5` - Weekdays at 9 AM
- `0 0 1 * *` - First day of month at midnight

**Response:** `200 OK`
```json
{
  "jobId": "apicall-3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "apiCallId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cronExpression": "0 */6 * * *",
  "message": "API call scheduled successfully"
}
```

**Response:** `404 Not Found` if API call doesn't exist

### 2. Remove a Schedule

Remove a recurring schedule for an API call.

**Endpoint:** `DELETE /api/ApiCallScheduling/{jobId}`

**Example:** `DELETE /api/ApiCallScheduling/apicall-3fa85f64-5717-4562-b3fc-2c963f66afa6`

**Response:** `200 OK`
```json
{
  "message": "Schedule removed successfully"
}
```

### 3. Get All Execution Logs

Retrieve execution logs for all API calls, ordered by most recent first.

**Endpoint:** `GET /api/ApiCallScheduling/logs`

**Response:** `200 OK`
```json
[
  {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "apiCallId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "responseCode": 200,
    "responseBody": "{\"weather\":\"sunny\"}",
    "executedAt": "2025-10-19T12:00:00Z",
    "durationMs": 245,
    "success": true,
    "errorMessage": null
  }
]
```

### 4. Get Logs for Specific API Call

Retrieve execution logs for a specific API call.

**Endpoint:** `GET /api/ApiCallScheduling/logs/{apiCallId}`

**Response:** `200 OK` (same structure as Get All Logs, filtered by apiCallId)

## Complete Workflow Example

Here's a complete example of creating, scheduling, and monitoring an API call:

### Step 1: Create an API Call

```bash
curl -X POST https://localhost:5001/api/ApiCallManagement \
  -H "Content-Type: application/json" \
  -d '{
    "title": "GitHub Status Check",
    "description": "Checks GitHub API status",
    "httpMethod": "GET",
    "endpointUrl": "https://api.github.com/status",
    "isActive": true
  }'
```

### Step 2: Schedule the API Call

Using the `id` from the create response:

```bash
curl -X POST https://localhost:5001/api/ApiCallScheduling \
  -H "Content-Type: application/json" \
  -d '{
    "apiCallId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "cronExpression": "*/30 * * * *"
  }'
```

### Step 3: Monitor Execution

Wait for the scheduled execution, then check logs:

```bash
curl https://localhost:5001/api/ApiCallScheduling/logs/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

### Step 4: View in Hangfire Dashboard

Navigate to `https://localhost:5001/hangfire` to see:
- Scheduled jobs
- Job execution history
- Success/failure statistics

## Advanced Examples

### API Call with Authentication

```json
{
  "title": "Authenticated API Call",
  "description": "Calls API with Bearer token",
  "httpMethod": "GET",
  "endpointUrl": "https://api.example.com/protected/resource",
  "isActive": true,
  "authenticationHeader": "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "otherHeaders": null,
  "payload": null
}
```

### POST Request with Payload

```json
{
  "title": "Create User API",
  "description": "Creates a new user",
  "httpMethod": "POST",
  "endpointUrl": "https://api.example.com/users",
  "isActive": true,
  "authenticationHeader": "Bearer YOUR_TOKEN",
  "otherHeaders": "Content-Type:application/json",
  "payload": "{\"name\":\"John Doe\",\"email\":\"john@example.com\"}"
}
```

### Multiple Custom Headers

Headers are separated by semicolons:

```json
{
  "title": "Multi-Header Request",
  "description": "Request with multiple custom headers",
  "httpMethod": "GET",
  "endpointUrl": "https://api.example.com/data",
  "isActive": true,
  "authenticationHeader": "Bearer YOUR_TOKEN",
  "otherHeaders": "X-Custom-Header:value1;X-Another-Header:value2;Accept:application/json",
  "payload": null
}
```

## Error Responses

### 400 Bad Request
Invalid request body or parameters.

### 404 Not Found
Resource (API call or schedule) not found.

### 500 Internal Server Error
Server-side error. Check logs for details.

## Best Practices

1. **Use Descriptive Titles**: Make API calls easy to identify
2. **Set isActive to false**: When testing new API calls before scheduling
3. **Monitor Logs**: Regularly check execution logs for failures
4. **Test Cron Expressions**: Use [crontab.guru](https://crontab.guru) to validate
5. **Archive Old Logs**: Implement log cleanup for production systems
6. **Secure Credentials**: Use Azure Key Vault or similar for production secrets

## Swagger/OpenAPI Documentation

Interactive API documentation is available at:
`https://localhost:5001/swagger`

This provides a UI to test all endpoints directly from your browser.
