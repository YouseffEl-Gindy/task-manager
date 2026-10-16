# Structure

How the codebase is organized and how the pieces fit together. For running, testing and deploying, see [`README.md`](README.md); for requirements and decisions, see [`docs/SPEC.md`](docs/SPEC.md) and the API contract in [`docs/API.md`](docs/API.md).

## Big picture

One repository, two deployable parts:

```
Browser ──> frontend/ (Next.js on Vercel) ──rewrite /api/*──> backend/ (ASP.NET Core on Azure App Service) ──> Azure SQL
```

- The browser only ever talks to the Next.js origin. `frontend/next.config.ts` rewrites `/api/:path*` to `${BACKEND_URL}/api/:path*`, so the `tm_session` cookie is first-party and there is no CORS.
- The backend is the only thing that touches the database. The same code runs on SQLite (local default, tests) or SQL Server (production), chosen by `Database:Provider`.
- `docs/API.md`, the C# DTOs in `backend/src/TaskManager.Api/Dtos/` and `frontend/types/api.ts` describe the same contract. A contract change updates all three; the backend contract tests read `frontend/types/api.ts` to catch drift.

## Folder map

```
.github/workflows/
  ci.yml              5 jobs on push/PR to main: backend (SQLite), migrations-check,
                      backend-sqlserver (throwaway container), frontend, e2e
  deploy-api.yml      after CI succeeds on main: publish on Windows -> Azure App Service -> poll /health
backend/
  TaskManager.sln
  .env.example        documents the backend env vars (placeholders only)
  src/TaskManager.Api/
    Program.cs        composition root and middleware order (see below)
    Auth/             Identity + cookie setup, CSRF header middleware, AppUserManager (lockout on
                      TimeProvider), cookie events that return JSON 401/403, Identity error mapping
    Controllers/      AuthController, AccountController, TasksController (all /api/v1),
                      ApiErrorResults + ServiceResultExtensions (service result -> HTTP response)
    Services/         AccountService, TaskService (business rules), TaskQuery (filter/sort/search),
                      ServiceResult
    Data/             AppDbContext, entity configurations, UTC DateTime converter, provider-specific
                      collations and LIKE escaping, DatabaseSettings (provider/schema guards),
                      design-time factory, Migrations/ (SQL Server only)
    Entities/         User (IdentityUser<Guid>), TaskItem + status/priority enums
    Dtos/, Mapping/   request/response records and entity <-> DTO mapping (entities never leave the API)
    Validation/       request validators (field-keyed errors)
    Json/             camelCase + string enums, strict DateTimeOffset converter (offset required)
    Middleware/       exception -> ApiError 500, status-code fallback (404/405/415) -> ApiError
    RateLimiting/     per-client-IP fixed window for login/register/change-password/delete-account
    OpenApi/          /openapi/v1.json + /swagger, only when OpenApi:Enabled (Development)
    appsettings*.json non-secret defaults; Development = SQLite + EnsureCreated
  tests/
    TaskManager.Api.UnitTests/         services, validators, settings, JSON, LIKE escaping
    TaskManager.Api.IntegrationTests/  WebApplicationFactory over HTTP; one test class per area
                                       (auth, tasks, list queries, isolation, contract, CSRF/errors,
                                       rate limit, startup guards, OpenAPI)
      Infrastructure/                  TaskManagerApiFactory (SQLite in-memory, or SQL Server via
                                       TEST_DATABASE_PROVIDER), helpers, FrontendApiTypes (reads api.ts)
frontend/
  next.config.ts      the /api rewrite; throws if BACKEND_URL is missing
  app/
    (public)/         login, register (redirect away if already signed in)
    (app)/            tasks list, new, [id] edit, account; wrapped in AuthGuard
    layout.tsx, page.tsx, not-found.tsx
  components/
    auth/             AuthGuard, forms, logout, redirects
    tasks/, account/  feature components (small, one job each)
    layout/, ui/      header and shared UI pieces
  lib/
    api/client.ts     the single fetch layer: relative /api/v1, CSRF header on unsafe methods,
                      ApiError parsing, non-ApiError -> "server unavailable", 401 notifies subscribers
    api/*.ts          typed calls per area (auth, account, tasks)
    auth/             AuthContext (current user), safeReturnUrl
    tasks/            URL query params <-> filters, local time <-> UTC, isOverdue, data hooks
  types/api.ts        shared contract types
  tests/unit/         Vitest + React Testing Library (only tests/unit/** is picked up)
  e2e/                Playwright specs (one per area) + support/ (API helpers, ports, DB reset)
  playwright.config.ts starts its own backend :5100 (throwaway SQLite) and frontend :3100
docs/                 SPEC.md (requirements, acceptance criteria, decisions log), API.md (contract)
dotnet-tools.json     pins the local dotnet-ef tool
testResults.txt       last full local test report
```

## Mechanisms worth knowing

**Backend request pipeline** (`Program.cs`, in order): exception handler → status-code-to-ApiError fallback → CSRF header check (`X-CSRF-Protection: 1` on POST/PUT/PATCH/DELETE) → routing → authentication (cookie) → rate limiter → authorization → controllers. `/health` is mapped outside `/api` and is not proxied by Vercel.

**One error shape.** Every non-2xx response under `/api/v1` is `ApiError { status, code, message, errors, traceId }`. Cookie events return JSON 401/403 instead of redirects. The frontend branches on `code`, never on message text.

**Database provider and schema.** `Data/DatabaseSettings.cs` reads `Database:Provider` (`Sqlite` | `SqlServer`, required) and `Database:Schema` (`Migrate` | `EnsureCreated` | `None`) and refuses mismatches at startup. SQLite uses `EnsureCreated`, so a model change means deleting `taskmanager.db*`. SQL Server applies `Data/Migrations` at app startup (production) or per test database (CI). Migrations are generated through `AppDbContextDesignTimeFactory`, so no database is needed to create them.

**Configuration sources.** Non-secret defaults are in `appsettings*.json`. Local Azure SQL settings are .NET user secrets, loaded only in Development (the `UserSecretsId` is in `TaskManager.Api.csproj`). Production settings and the connection string are App Service settings. Integration tests run as `Testing`, and Playwright passes environment variables, so neither ever reads the user secrets.

**Auth state in the frontend.** `lib/api/client.ts` never navigates. A 401 `unauthorized` notifies subscribers; `AuthContext` clears the user and `AuthGuard` redirects to `/login?returnUrl=...` (validated by `safeReturnUrl`). Filters, search and sort live in the URL (`lib/tasks/queryParams.ts`). `cacheComponents` and `partialPrefetching` are off on purpose, so one user's pages are never kept alive after logout.

**Dates.** Due dates are stored as UTC `DateTime` (SQLite can't sort `DateTimeOffset`). The API requires an explicit offset or `Z` and always returns `Z`. The UI converts to and from local time in `lib/tasks/dateTime.ts`.

**Release flow.** Work on a branch, open a pull request (CI runs), then merge into `main`. CI runs again on `main`; on success, `deploy-api.yml` deploys the backend, and Vercel promotes the frontend build only after the 5 CI checks pass (Vercel Deployment Checks).

**Dependency pin.** `TaskManager.Api.csproj` pins `System.Security.Cryptography.Pkcs` 10.0.13 because `Microsoft.Data.SqlClient` 6.1.x still pulls in a vulnerable 9.0.11. Remove the pin once SqlClient depends on a fixed version (`dotnet list package --vulnerable --include-transitive` tells you).
