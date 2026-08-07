# Solution Documentation

**Candidate Name:** [Chandresh Vaghasia]  
**Completion Date:** [08/08/2026]

---

## Problems Identified

_Describe the issues you found in the original implementation. Consider aspects like:_
- Architecture and design patterns
- Code quality and maintainability
- Security vulnerabilities
- Performance concerns
- Testing gaps

- API endpoints do not follow RESTful conventions. All operations use POST instead of the appropriate HTTP methods (GET, PUT, DELETE).
- Several unit tests fail because the SQLite database schema is not initialized correctly, resulting in "no such table: Todos" errors.
- Project does not implement API versioning and has no authentication/authorization configured
- The SQLite connection string is hardcoded instead of being managed through configuration.
- Controllers instantiate services directly rather than using Dependency Injection.
- The service layer does not expose interfaces, making testing and extensibility more difficult.
- The application lacks proper input validation.
- Exception handling is inconsistent and exposes internal exception messages to API consumers.
- Logging is not implemented.
- Test coverage is limited and does not include sufficient negative or edge case scenarios.
- Single class doing too much: the service contained SQL, schema setup, and business logic. That makes the code hard to test and maintain.
- No clear data layer: there was no repository abstraction, so swapping or mocking the database was harder.
- Tests mixed and unit styles: some controller tests used the real DB which made tests slow and brittle.
- No pagination: initially the API returned all items at once which won't scale.
- No caching: repeated reads hit the database every time.

---

## Architectural Decisions

_Explain the architecture you chose and why. Consider:_
- Design patterns applied
- Project structure changes
- Technology choices
- Separation of concerns

- Layering: Controller --> Service --> (currently direct SQLite in service).
  - Controllers are thin and only map HTTP to service calls.
  - Service contains business rules and cache logic.
- Caching: memory cache (IMemoryCache) with a version token for simple invalidation.
  - This is easy to implement and works for a single-server app.
  - If you run multiple instances, switch to distributed cache (Redis).
- Pagination: LIMIT/OFFSET with a COUNT query. Stable ordering by Id (ORDER BY Id ASC).
- API versioning - Use URL-segment versioning (e.g., /api/v{version}/...) via Microsoft.AspNetCore.Mvc.Versioning and the versioned API explorer.
- Tests:
  - Controller tests: mocked ITodoService using Moq (fast, unit tests).
  - Service tests: integration-style using a temporary SQLite DB for correctness and cache tests.

Why this way:
- Keep the change small and focused so the app stays stable and tests run fast.
- The structure is simple to extend later (e.g., add a repository layer or split projects).

---

## Trade-offs

_Discuss compromises you made and the reasoning behind them. Consider:_
- What did you prioritize?
- What did you defer or simplify?
- What alternatives did you consider?

- Chose in-memory cache (IMemoryCache) instead of distributed cache to keep the scope small.
  - Trade-off: works for single instance; for multiple instances you need Redis.
- Left raw SQLite SQL rather than switching to EF Core.
  - Trade-off: faster to deliver and easy to reason about, EF Core would add migrations and convenience but more work.
- Kept everything in one project for the exercise.
  - Trade-off: easier to manage for the 2 day limit. Splitting into Core/Api/Infrastructure would be better for large apps but requires more changes.

---

## How to Run

### Prerequisites
[List required software, versions, etc.]
.NET SDK 7+ installed (dotnet on PATH)
(Optional) sqlite3 if you want to inspect the DB file

### Build
```bash
# Add your build commands
dotnet build
```

### Run
```bash
# Add your run commands
dotnet run --project TodoApi
By default the app uses the connection string in appsettings.json: "ConnectionStrings": { "TodoDatabase": "Data Source=todos.db" }
```

### Test
```bash
# Add your test commands
dotnet test
```

---

## API Documentation

### Endpoints

#### Create TODO
```
Method: [POST]
URL: [/api/todos]
Request Body: 
{
  "title": "Buy milk",
  "description": "2 liters, whole milk",
  "isCompleted": false
}
Response: (201 Created)
Headers:
  Location: /api/todos/{id}
Body:
{
  "id": 1,
  "title": "Buy milk",
  "description": "2 liters, whole milk",
  "isCompleted": false,
  "createdAt": "2026-08-07T12:34:56Z",
  "version": 1
}
```

#### Get TODO(s)
```
Method: [GET]
URL: [/api/todos?pageNumber={pageNumber}&pageSize={pageSize}]
Notes:
  - pageNumber: 1-based, default = 1
  - pageSize: default = 20, max = 100
Example:
  GET /api/todos?pageNumber=1&pageSize=20
Response: (200 OK)
Body:
{
  "items": [
    {
      "id": 1,
      "title": "Buy milk",
      "description": "2 liters",
      "isCompleted": false,
      "createdAt": "2026-08-07T12:34:56Z",
      "version": 1
    },
    {
      "id": 2,
      "title": "Send email",
      "description": "Follow up with Bob",
      "isCompleted": true,
      "createdAt": "2026-08-06T09:10:11Z",
      "version": 1
    }
  ],
  "totalCount": 42,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

#### Get TODO by id
```
Method: [GET]
URL: [/api/todos/{id}]
Example:
  GET /api/todos/1
Response: Success Response (200 OK)
Body:
{
  "id": 1,
  "title": "Buy milk",
  "description": "2 liters",
  "isCompleted": false,
  "createdAt": "2026-08-07T12:34:56Z",
  "version": 1
}
Not Found Response (404 Not Found)
Body: empty
```

#### Update TODO
```
Method: [PUT]
URL: [/api/todos/{id}]
Request Body:
{
  "title": "Buy milk and bread",
  "description": "2 liters, whole milk; 1 loaf",
  "isCompleted": false,
  "version": 1     // client's last-seen version for optimistic concurrency
}
Response: (200 OK)
Body:
{
  "id": 1,
  "title": "Buy milk and bread",
  "description": "2 liters, whole milk; 1 loaf",
  "isCompleted": false,
  "createdAt": "2026-08-07T12:34:56Z",
  "version": 2  // version incremented by server
}
Not Found Response (404 Not Found)
Body: empty

Conflict Response (409 Conflict) — version mismatch
Body (ProblemDetails):
{
  "type": "about:blank",
  "title": "Conflict",
  "status": 409,
  "detail": "The todo was updated by another client. Fetch the latest version and retry."
}
```

#### Delete TODO
```
Method: [DELETE]
URL: [/api/todos/{id}]
Example:
  DELETE /api/todos/1
Response: (204 No Content)
Body: empty
Not Found Response (404 Not Found)
Body: empty
```

---

## Future Improvements

_What would you do if you had more time? Consider:_
- Additional features
- Performance optimizations
- Enhanced testing
- Better documentation
- Deployment considerations

- Move data access (SQL) into a repository layer (ITodoRepository/TodoRepository). This makes the service easier to test and the DB easier to replace.
- Split the solution into projects:
  - TodoApi.Core (models & interfaces)
  - TodoApi.Infrastructure (repository & caching)
  - TodoApi.Api (controllers & DI)
- Replace raw SQL with EF Core + migrations.
- Add richer validation and API error standard (ProblemDetails details).
- Improve logging (structured logs) and add correlation IDs.
- Add basic integration tests using WebApplicationFactory for end-to-end coverage.
- Add authentication & authorization (JWT/OAuth2) to protect endpoints.