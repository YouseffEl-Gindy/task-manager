# Task Manager - Product Specification (MVP)

Status: Draft v6 (2026-10-10). This version includes all of the user's decisions (see section 11, Decisions log). There are no open questions. Read `docs/API.md` for the API contract. Shared TypeScript types are in `frontend/types/api.ts`.

---

## 1. Goal

A personal task manager web app. A user signs up and creates tasks. Each task has a title, an optional description, a status, a priority, and an optional due date and time. Users can filter, search, and sort their own tasks, and manage their own account. Each user sees and changes only their own tasks.

Stack:
- **Frontend**: Next.js (App Router, TypeScript strict, Tailwind CSS), deployed to **Vercel**.
- **Backend**: ASP.NET Core Web API on **.NET 10**, with EF Core and ASP.NET Core Identity, hosted on **Azure App Service (Free F1, Windows, 32-bit worker)**.
- **Database**: **SQLite for local dev and tests, SQL Server in production** (the Azure SQL Database free offer). The provider is selected by `Database__Provider`. EF Core migrations target **SQL Server only**; SQLite gets its schema from `EnsureCreated`.
- **Auth**: cookie sessions. The browser only ever talks to the Vercel origin, and Next.js proxies `/api/*` to the backend.

---

## 2. Features

1. **Register**: display name, email, and password. The user is logged in straight after (a session cookie is set).
2. **Log in / log out**: email and password log-in creates a session cookie. Logging out clears it.
3. **Current user**: the frontend checks for a session on load (`GET /auth/me`).
4. **Account settings**: update display name, change password (requires the current password), and delete account (requires the password; deletes all of the user's tasks and logs out).
5. **Create task**: title (required), description, status, priority, due date and time (all optional). A due date must be in the future.
6. **View task list**: every task the user owns, in one list (no pagination).
7. **Filter / search / sort tasks**: filter by status and priority, search text in title and description, and sort by created date, due date, priority, or title.
8. **View and edit task**: edit all fields of one task.
9. **Quick status change**: change a task's status straight from the list.
10. **Delete task**: delete a task after a confirmation prompt. This is a hard delete.
11. **Ownership isolation**: a user can never read, change, or delete another user's tasks.

Out of scope for the MVP (future candidates):
- **Forgot/reset password**. It was cancelled for now, so the MVP sends no email at all. Until it exists, a user who forgets their password cannot get back into their account.
- Email verification, changing the account email, and "remember me".
- Sharing or teams, tags or labels, subtasks, attachments, notifications, bulk actions, and pagination.

---

## 3. User stories

| ID | Story |
|---|---|
| US-1 | As a visitor, I want to register with a display name, email, and password so that I can keep my own task list. |
| US-2 | As a registered user, I want to log in so that I can reach my tasks. |
| US-3 | As a logged-in user, I want to log out so that nobody else can use my session on this browser. |
| US-4 | As a user, I want to change my display name. |
| US-5 | As a user, I want to change my password. |
| US-6 | As a user, I want to delete my account and all my data. |
| US-7 | As a user, I want to create a task with a title, description, priority, status, and due date and time. |
| US-8 | As a user, I want to see all my tasks with their status, priority, and due date. |
| US-9 | As a user, I want to filter by status and priority and search by text. |
| US-10 | As a user, I want to sort tasks by due date, priority, title, or creation date. |
| US-11 | As a user, I want to edit a task, even after its due date has passed. |
| US-12 | As a user, I want to mark a task In Progress or Done straight from the list. |
| US-13 | As a user, I want to delete a task I no longer need. |
| US-14 | As a user, I want my tasks and account to be private. |

---

## 4. Functional requirements

### 4.1 Auth and sessions
- FR-A1: Auth uses **ASP.NET Core Identity** with the **cookie** scheme: `AddIdentityCore<User>()` + `AddSignInManager()` + `AddEntityFrameworkStores<AppDbContext>()`, and identity cookies. Identity handles password hashing, lockout, and security stamps. Identity's built-in `MapIdentityApi` endpoints are **not** used, because the API needs its own routes and the `ApiError` shape.
- FR-A2: Register takes `displayName`, `email`, and `password`. The email is trimmed and lowercased, then stored as both `Email` and `UserName`. Emails are unique (case-insensitive, see 6.1). If the email is taken, register returns `409 email_taken`.
- FR-A3: Identity password options must match the DTO rules: `RequiredLength = 8`, `RequireDigit = true`, `RequireLowercase = false`, `RequireUppercase = false`, `RequireNonAlphanumeric = false`, `RequiredUniqueChars = 1`. Two rules Identity can't express ("at least one letter" and "max 128") are enforced by DTO validation. Identity errors are mapped to `validation_failed` on the matching field (for example `password` or `newPassword`). `DuplicateUserName` / `DuplicateEmail` map to `409 email_taken`.
- FR-A4: **Session cookie**:
  - Name `tm_session`, `HttpOnly`, `SameSite=Lax`, `Path=/`, no `Domain`. It is a browser-session cookie (not persistent, no `Expires`). There is no "remember me".
  - `Secure` is controlled by `Auth__CookieSecure`: `Always` in production, `SameAsRequest` for local HTTP development only.
  - **Session lifetime: 8 hours, sliding.** Each authenticated request after half the window has passed renews the session. After 8 hours of inactivity, the user must log in again. The length is set by `Auth__SessionHours`.
- FR-A5: The cookie handler must **never redirect**. `OnRedirectToLogin` returns `401 unauthorized` and `OnRedirectToAccessDenied` returns `403`, both with an `ApiError` JSON body.
- FR-A6: **CSRF protection: a required custom header.** Every `POST`, `PUT`, `PATCH`, and `DELETE` request to `/api/*` must include the header `X-CSRF-Protection: 1`. This applies to login and register too.
  - A middleware runs before authentication and model binding. Without the header, the request is rejected with `403 csrf_failed` and nothing changes.
  - Why this works: a browser cannot attach a custom header to a cross-site request without a CORS preflight. The API enables **no CORS**, so every such preflight fails.
  - This works together with `SameSite=Lax`. ASP.NET antiforgery tokens are not used.
- FR-A7: Login uses `SignInManager.PasswordSignInAsync(..., isPersistent: false, lockoutOnFailure: true)`. **Lockout**: after 5 failed attempts, the account is locked for 15 minutes.
  - An unknown email, a wrong password, and a locked-out account all return the same `401 invalid_credentials` response, so the API doesn't reveal which accounts exist.
  - Email confirmation is not required (`SignIn.RequireConfirmedEmail = false`).
- FR-A8: Logout (`POST /auth/logout`) signs out and expires the cookie. It returns `204` even if no session exists.
- FR-A9: The security stamp is checked every `Auth__SecurityStampValidationSeconds` (default 60). After a password change or an account deletion, other sessions stop working within that interval.
- FR-A10: ASP.NET Data Protection keys (which protect the session cookie) are **persisted in the app database through EF Core**, with `PersistKeysToDbContext<AppDbContext>()`. This works on both providers and keeps sessions valid when App Service restarts or unloads the app.

### 4.2 Account management
- FR-ACC1: **Update display name** (`PUT /account/profile`). The name is trimmed and must be 1-50 chars. It is not unique.
- FR-ACC2: **Change password** (`POST /account/change-password`).
  - Requires `currentPassword`. A wrong current password returns `400 validation_failed` on the `currentPassword` field. It is **not** a 401, so the client doesn't treat it as a logout.
  - `newPassword` must follow the password rules and differ from `currentPassword`.
  - On success, the current session is refreshed (`RefreshSignInAsync`) and stays valid. Other sessions are invalidated (see FR-A9).
- FR-ACC3: **Delete account** (`POST /account/delete`).
  - Requires `password`. A wrong password returns `400 validation_failed` on the `password` field.
  - On success, it deletes the user. The user's tasks are removed by the database cascade delete, along with the Identity child rows. Then it signs out (the cookie is expired) and returns `204`.

### 4.3 Tasks
- FR-T1: Every task belongs to exactly one user (`userId`, taken from the session). The client never sends `userId`.
- FR-T2: Create defaults: `status = todo`, `priority = medium`, `description = null`, `dueDate = null`.
- FR-T3: `createdAt` and `updatedAt` are set by the server in UTC from an injected `TimeProvider`. `updatedAt` is refreshed on every successful update.
- FR-T4: Update is a **full replace (`PUT`)**. Every field must be sent. `description` and `dueDate` may be `null`. The quick status change from the list uses the same `PUT`, sending the task's other current values unchanged. There is no PATCH and no separate status endpoint.
- FR-T5: Any request for a task that does not exist **or belongs to another user** returns `404 not_found`, never `403`.
- FR-T6: The list endpoint returns **all** of the caller's matching tasks as a plain array, with no pagination. Query parameters: `status`, `priority`, `search`, `sortBy`, `sortDir`.
- FR-T7: Search is a case-insensitive substring match on `title` OR `description`.
  - It uses `EF.Functions.Like(column, pattern, "\\")` with `\` as the explicit escape character.
  - Before building the `%...%` pattern, the escaping is per provider (6.0):
    - Both providers: escape `\`, `%`, and `_`.
    - SqlServer provider only: also escape `[`.
  - Case-insensitivity per provider:
    - SQLite: its `LIKE` ignores case for **ASCII letters only**.
    - SqlServer provider: it comes from the case-insensitive column collation.
- FR-T8: Sorting:
  - Default: `createdAt desc`.
  - `priority` order is low < medium < high.
  - `dueDate`: tasks with no due date always come **last**, whichever direction is used. Tasks that have a due date are ordered by the instant (UTC).
  - `title`: case-insensitive, through the column collation (6.0).
  - Tie-breakers: `createdAt desc`, then `id asc`.
- FR-T9: **Due date is a date and time.**
  - The API accepts ISO 8601 with a **required** offset or `Z` (for example `2026-10-12T17:00:00+02:00`). Values with no offset are rejected.
  - The server converts the value to UTC and **truncates it to whole seconds**, then stores it as a UTC `DateTime`.
  - Responses always return UTC with `Z`.
  - The UI shows and edits due dates in the user's local time zone.
- FR-T10: **Due-date-in-the-future rule** (confirmed):
  - **Create**: a non-null `dueDate` must be strictly later than the server's current UTC time. Otherwise the request fails with `400 validation_failed` on `dueDate`.
  - **Update**: a past `dueDate` is rejected **only when the value is changing**. "Changing" means the normalized UTC instant is different from the stored value, or the stored value is `null`.
    - Re-sending the stored due date unchanged is always allowed, even if it is now in the past. This lets an overdue task be edited, for example marked done. The quick status change depends on this.
    - Setting `dueDate` to `null` is always allowed.
    - Changing it to a different past instant is rejected.
  - Because this check needs the stored task, it runs after the ownership lookup. A task that isn't found returns 404 before the due-date rule is checked.
- FR-T11: "Overdue" is shown only on the frontend: `dueDate < now AND status != done`. The backend does not return it.
- FR-T12: Concurrent edits use **last-write-wins**. There is no concurrency token and no `409` on conflict.

---

## 5. Non-functional requirements

### 5.1 Validation
- All request DTOs are validated on the server. The client also validates, but only to give the user quicker feedback. Validation is what enforces maximum string lengths, because SQLite does not (6.0).
- `email`, `displayName`, `title`, `description`, and `search` are trimmed before validation and storage. Passwords are never trimmed. If `description` is empty after trimming, it is stored as `null`.
- Unknown JSON properties are ignored.
- Enums are sent as camelCase strings. Integer values are rejected (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)`). Query-string enums must be valid names; numeric strings are rejected.
- `dueDate` uses a strict `DateTimeOffset` JSON converter that rejects strings with no offset. `DateTimeOffset` is used **only** in request DTOs. It is converted to a UTC `DateTime` before it reaches EF.
- The full rules for each field are in API.md.

### 5.2 Error handling
- Every error response the **backend** produces under `/api/*` uses the `ApiError` shape (API.md section 3). This covers validation, malformed JSON, auth (no redirects), CSRF, 404s, 429s, and unhandled exceptions. `GET /health` is the one documented exception.
- Validation error keys use camelCase field names. When malformed JSON can't be tied to a field, the key is `body`.
- A 500 response never includes stack traces or exception text. It returns `internal_error` with a `traceId`.
- Frontend:
  - On a protected page, an `unauthorized` error from any call redirects to `/login?returnUrl=<path>`.
  - The `invalid_credentials` 401 from login is shown on the form. It does **not** trigger the redirect.
  - Validation errors are shown next to their form fields.
  - Some responses don't come from the backend, so they are **not** `ApiError` JSON: a network error, a proxy `502`/`504` while App Service or the database wakes up, or the App Service "site stopped" page when the F1 daily CPU quota is used up. The API client must handle these without crashing and show "The server is starting up or unavailable. Please try again in a moment."

### 5.3 Security
- No secrets in source control.
  - The production connection string and other secrets live only in Azure App Service configuration, GitHub secrets, or Vercel environment variables.
  - `.env.example` files contain placeholders only.
  - The local SQLite connection string and the design-time placeholder connection string (6.0) are not secrets.
- Session, CSRF, lockout, and security stamp handling: see 4.1.
- **No CORS policy** on the API. The browser only calls the Vercel origin, and Next.js proxies the calls.
- **Rate limiting** applies to `register`, `login`, `account/change-password`, and `account/delete`.
  - The limit is per client IP. The default is 10 requests per 60 seconds, set by `RateLimiting__AuthPermitLimit` and `RateLimiting__AuthWindowSeconds`.
  - Over the limit, the API returns `429 rate_limited` with a `Retry-After` header.
  - Behind the Vercel proxy, the client IP is the **left-most** `X-Forwarded-For` value when `RateLimiting__UseForwardedFor=true`. Vercel sets it to the browser's IP, and App Service appends its own hop to the right. Otherwise the connection IP is used.
  - Accepted risk: the `azurewebsites.net` URL is publicly reachable, so someone calling it directly could fake this header. Identity lockout still protects each account.
- App Service **HTTPS Only** is on. Vercel calls the backend over `https://`.
- Ownership is enforced in every task query (`WHERE UserId = currentUserId`, written in LINQ).
- **No raw SQL anywhere** (`FromSql*` / `ExecuteSql*`). All data access is LINQ through EF Core.
- EF entities are never serialized. Controllers return DTOs only.
- The backend never sets the cookie `Domain`, so the cookie belongs to the Vercel origin.

### 5.4 Deployment architecture and local run
```
Browser --(https, same origin)--> Next.js on Vercel
                                    | next.config.ts rewrites: /api/:path* -> ${BACKEND_URL}/api/:path*
                                    v
                                  https://<app>.azurewebsites.net   (Azure App Service, Free F1, Windows, .NET 10)
                                    |
                                    v
                                  Azure SQL Database (free offer, serverless, auto-pause)   [Database__Provider=SqlServer]
```
- The frontend calls **relative** URLs (`/api/v1/...`). It never calls the backend origin directly.
- `BACKEND_URL` in Vercel is `https://<app>.azurewebsites.net` (no trailing slash). It is read when the Next.js build runs, so it must be set in each Vercel environment (Production and Preview) **before building**. Change it, then redeploy.
- The proxy passes `Set-Cookie` through, so the session cookie belongs to the Vercel origin. The proxy only covers `/api/*`, which means `/health` is reached on the backend host directly.
- Because of the proxy, response headers that contain URLs must use **relative** paths, not absolute ones (for example `Location: /api/v1/tasks/{id}`).
- **Runtime** (updated 2026-10-16): the web app runs on a **Windows** plan (Free F1) with the **.NET 10** runtime stack. Free Windows plans run a **32-bit** worker only, so the app is published **framework-dependent and portable** (AnyCPU, `dotnet publish -c Release -p:UseAppHost=false`; never x64-only). The generated `web.config` starts `dotnet .\TaskManager.Api.dll` in-process; no startup command is needed. Fallback if the plan ever lacks .NET 10: publish self-contained for the 32-bit worker (`dotnet publish -c Release -r win-x86 --self-contained true`).
- **Production schema**: with `Database__Schema=Migrate`, the app applies the SQL Server migrations at startup (see 12, DevOps).
- **Surviving the auto-paused database**: in production, register the SqlServer provider with `EnableRetryOnFailure(maxRetryCount: 6, maxRetryDelay: 30s)`. Use a connection string with `Connect Timeout=60`. Azure SQL's "database not currently available" error while it resumes is one of EF Core's default transient errors, so it is retried. The code uses no explicit transactions; if one is ever added, it must run inside `Database.CreateExecutionStrategy()`.
- **No uptime pinger**: don't point a frequent uptime monitor or keep-alive at `/health`. It would keep the database awake and use up the free monthly allowance.

**Local run (no Docker, no database server):**
1. Backend: `cd backend/src/TaskManager.Api`, then `dotnet run`.
   - It uses `appsettings.Development.json` (`Database__Provider=Sqlite`, `Database__Schema=EnsureCreated`).
   - It creates `taskmanager.db` in that folder if it doesn't exist, and listens on `http://localhost:5000`.
2. Frontend: `cd frontend`, then `npm run dev`. It runs on `http://localhost:3000` and proxies `/api/*` to the backend.
3. **When the data model changes** (a new or changed entity or column), stop the backend, **delete `taskmanager.db`** (and any `taskmanager.db-shm` / `taskmanager.db-wal` files), and start it again. `EnsureCreated` only creates a database that doesn't exist. It never updates an existing one, so an old file causes errors such as "no such column". Local data is throwaway.

### 5.5 Configuration (environment variables)

| Variable | Used by | Local dev / tests | Production |
|---|---|---|---|
| `Database__Provider` | backend | `Sqlite` (`appsettings.Development.json`, SQLite tests). The SQL Server CI job uses `SqlServer`. | **`SqlServer`** (App Service setting). There is no default in `appsettings.json`, and startup **fails** if the value is missing or unknown. |
| `ConnectionStrings__Default` | backend | `Data Source=taskmanager.db` (the file is created in the backend project folder and git-ignored) | **Secret.** Set as an App Service *connection string* named `Default`, type `SQLAzure`, which ASP.NET Core reads as `ConnectionStrings:Default`. Example shape: `Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;User ID=<app-user>;Password=<secret>;Encrypt=True;Connect Timeout=60;`. Never in the repo. |
| `Database__Schema` | backend | `EnsureCreated` (local dev and SQLite tests). `Migrate` in the SQL Server CI job. | **`Migrate`** |
| `ASPNETCORE_ENVIRONMENT` | backend | `Development` | `Production` |
| `Auth__SessionHours` | backend | `8` | `8` |
| `Auth__CookieSecure` | backend | `SameAsRequest` | **`Always`** |
| `Auth__SecurityStampValidationSeconds` | backend | `60` (tests: `0`) | `60` |
| `RateLimiting__AuthPermitLimit` | backend | `10` | `10` |
| `RateLimiting__AuthWindowSeconds` | backend | `60` | `60` |
| `RateLimiting__UseForwardedFor` | backend | `true` when calls go through the Next.js proxy | `true` |
| `BACKEND_URL` | frontend (server/build only) | `http://localhost:5000` | `https://<app>.azurewebsites.net` (Vercel env var) |

`Database__Schema` has three values:
- `Migrate`: run `Database.Migrate()` at startup. Use it **only with `SqlServer`**, because the migrations are SQL Server migrations.
- `EnsureCreated`: run `Database.EnsureCreated()` at startup. Use it **only with `Sqlite`**.
- `None`: do nothing. This is the default in `appsettings.json`.

The app **fails at startup** for `Migrate` + `Sqlite` and for `EnsureCreated` + `SqlServer`.

Local ports: backend `http://localhost:5000`, frontend `http://localhost:3000`.

### 5.6 Quality
- Every feature needs automated tests before it counts as done.
- Backend code takes time from `TimeProvider`, so tests can use `FakeTimeProvider` for due dates and session expiry.
- **Integration tests on SQLite in-memory** (local and CI, no Docker):
  - Each `WebApplicationFactory` (one per test class) opens one `SqliteConnection("DataSource=:memory:")` and **keeps it open** for its whole lifetime.
  - The factory replaces the `AppDbContext` registration with `UseSqlite(thatConnection)` and sets `Database__Schema=EnsureCreated`.
  - Tests in a class share that database, so each test creates its own users (unique emails).
- **Integration tests on SQL Server** (a CI job on **every** run): the same suite runs in GitHub Actions against a SQL Server service container.
  - Set `TEST_DATABASE_PROVIDER=SqlServer`, `TEST_DATABASE_CONNECTION`, and `Database__Schema=Migrate`.
  - Each factory gets a uniquely named database. **The real migrations are applied to it at startup**, and it is dropped with `EnsureDeleted()` when the factory is disposed.
  - This tests the migrations and catches differences in collation, LIKE, ordering, and cascades.
- **Migrations check** (CI, every run): `dotnet ef migrations has-pending-model-changes` must report no pending changes.
- TypeScript `strict: true`. `any` is not allowed (ESLint `@typescript-eslint/no-explicit-any: error`).
- Components are small, with one responsibility each.

---

## 6. Data model (provider-neutral)

### 6.0 Provider rules

`AppDbContext` is registered with a switch on `Database__Provider`:
- `Sqlite` -> `UseSqlite(connectionString)`. Used for local dev and tests.
- `SqlServer` -> `UseSqlServer(connectionString, o => o.EnableRetryOnFailure(6, TimeSpan.FromSeconds(30), null))`. Used in **production** and in the SQL Server CI job.
- Anything else: startup fails.

Both provider packages are referenced. Provider-specific model settings go in `OnModelCreating`, using `Database.IsSqlite()` / `Database.IsSqlServer()`.

| Concern | Rule (both providers) | Sqlite provider (dev/tests) | SqlServer provider (production) |
|---|---|---|---|
| Strings | `HasMaxLength(N)` on every bounded string | `TEXT`. The max length is **not enforced** by SQLite; DTO validation enforces it. | `nvarchar(N)` / `nvarchar(max)` |
| Timestamps | **Always `DateTime` in UTC**, never `DateTimeOffset` for app-owned columns. The EF Core SQLite provider can't translate `ORDER BY` or comparisons on `DateTimeOffset`. Request `DateTimeOffset` values are converted to UTC `DateTime` before storing. | `TEXT` (`yyyy-MM-dd HH:mm:ss.FFFFFFF`, sorts correctly as text) | `datetime2` |
| UTC kind | Both providers return `Kind=Unspecified`. A model-wide value converter in `ConfigureConventions` marks every `DateTime`/`DateTime?` read from the database as UTC. | same | same |
| Identity `LockoutEnd` | The one `DateTimeOffset?` column, which comes from `IdentityUser`. Identity only reads and writes it in C#. It is never used in a LINQ filter or sort. | `TEXT` | `datetimeoffset` |
| GUID keys | Leave `Id` as `Guid.Empty` on insert and let EF's **default value generation** assign it. Never assign ids yourself, and don't use `Guid.CreateVersion7()`. | Random GUIDs, stored as `TEXT` (uppercase). | `uniqueidentifier`, with EF's SQL Server-sequential generator, which avoids clustered-index fragmentation. |
| Case-insensitive `Title` (sort) and `Title`/`Description` (search) | Set `UseCollation(...)` per provider on `Title` and `Description`. | `NOCASE`, which folds **ASCII letters only** | `Latin1_General_100_CI_AS`, set on the column, so the database's default collation doesn't matter |
| Search `LIKE` | `EF.Functions.Like(col, pattern, "\\")`. Escape `\`, `%`, `_`. | `LIKE` is case-insensitive for ASCII only. Don't escape `[`, because it isn't special here. | Also escape `[`. Case-insensitivity comes from the column collation. |
| Email uniqueness | No collation needed. The API stores email trimmed and **lowercased**. Identity also keeps `NormalizedEmail` / `NormalizedUserName` (upper-cased), and `NormalizedUserName` has a unique index. Lookups and uniqueness use the normalized columns, so they ignore case on both providers. | same | same |
| Enums | `Status` is stored as a string, `Priority` as an int (Low=0, Medium=1, High=2), so sorting by number gives low < medium < high. | `TEXT` / `INTEGER` | `nvarchar(20)` / `int` |
| Cascade delete | FK `ON DELETE CASCADE` from tasks and Identity child rows to the user. | It relies on `PRAGMA foreign_keys=ON`, which EF Core turns on when it opens the connection. Don't disable it in the connection string. | Native |
| Data Protection keys | `DataProtectionKeys` table through `PersistKeysToDbContext` | works | works |
| Schema creation | - | `EnsureCreated()` from the model. **No migrations, ever.** | **Migrations** (the only migrations set) |
| Raw SQL | None. LINQ only. | - | - |

**Migrations (SQL Server only):**
- There is exactly **one** migrations set, in `backend/src/TaskManager.Api/Data/Migrations`, and it targets **SQL Server**. There are no SQLite migrations, now or later.
- The migrations are generated **without a running SQL Server**, using a design-time factory. The `dotnet ef` tools use it instead of the app's startup configuration. Adding a migration builds the model only and never connects, so the placeholder connection string is never used:

  ```csharp
  // backend/src/TaskManager.Api/Data/AppDbContextDesignTimeFactory.cs
  public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
  {
      public AppDbContext CreateDbContext(string[] args)
      {
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseSqlServer("Server=localhost;Database=TaskManager_DesignTime;Trusted_Connection=True;TrustServerCertificate=True")
              .Options;
          return new AppDbContext(options);
      }
  }
  ```
  Because the factory uses `UseSqlServer`, `Database.IsSqlServer()` is true in `OnModelCreating`, and the migrations get the SQL Server collations and types.
- **Prerequisites (once)**:
  - `dotnet-ef` is a **local** tool pinned in `dotnet-tools.json` at the repository root (matches EF Core 10). Run `dotnet tool restore` once after cloning, and in CI.
  - The API project references `Microsoft.EntityFrameworkCore.Design`.
- **Add a migration**, from the repository root:

  ```
  dotnet ef migrations add <MigrationName> --project backend/src/TaskManager.Api --startup-project backend/src/TaskManager.Api --output-dir Data/Migrations
  ```

  The first one is `InitialCreate`. After any change to the model, add a new migration with a descriptive name and commit it.
- **Check that the migrations match the model** (CI):

  ```
  dotnet ef migrations has-pending-model-changes --project backend/src/TaskManager.Api --startup-project backend/src/TaskManager.Api
  ```
- **Where the migrations are applied**: in the SQL Server CI job on every run (`Database__Schema=Migrate`), and in production at app startup (`Database__Schema=Migrate`). SQLite (local dev and tests) never uses them; it uses `EnsureCreated`, so after a model change you delete `taskmanager.db` locally (5.4).

### 6.1 `User` (extends `IdentityUser<Guid>`, table `AspNetUsers`)

| Field | C# type | Constraints |
|---|---|---|
| Id | `Guid` | PK, default EF value generation (6.0) |
| UserName / NormalizedUserName | `string` | Identity, max 256. UserName = email. Unique index on NormalizedUserName. |
| Email / NormalizedEmail | `string` | Identity, max 256. Stored trimmed and lowercased. API rule: max 254 chars. |
| PasswordHash | `string` | Identity (PBKDF2), unbounded string |
| SecurityStamp, ConcurrencyStamp | `string` | Identity |
| LockoutEnd | `DateTimeOffset?` | Identity (see 6.0) |
| LockoutEnabled, AccessFailedCount, EmailConfirmed | `bool`, `int`, `bool` | Identity |
| *(other IdentityUser columns: PhoneNumber, TwoFactorEnabled, ...)* | | Unused |
| **DisplayName** | `string` | Required, max 50, 1-50 chars after trim |
| **CreatedAt** | `DateTime` (UTC) | Required |
| Tasks | `ICollection<TaskItem>` | Navigation |

`AppDbContext : IdentityUserContext<User, Guid>, IDataProtectionKeyContext`. This gives the Identity user tables only (no roles), plus `DataProtectionKeys`.

### 6.2 `TaskItem` (table `Tasks`)

| Field | C# type | Constraints |
|---|---|---|
| Id | `Guid` | PK, default EF value generation (6.0) |
| UserId | `Guid` | Required, FK -> `AspNetUsers.Id`, `ON DELETE CASCADE` |
| Title | `string` | Required, max 200, 1-200 chars after trim. Case-insensitive collation (6.0). |
| Description | `string?` | Optional, max 2000. Case-insensitive collation (6.0). |
| Status | `TaskItemStatus` | Required, string conversion (max 20), default `Todo` |
| Priority | `TaskItemPriority` | Required, int conversion, default `Medium` |
| DueDate | `DateTime?` (UTC) | Optional, truncated to whole seconds |
| CreatedAt | `DateTime` (UTC) | Required |
| UpdatedAt | `DateTime` (UTC) | Required |
| User | `User` | Navigation |

**Indexes**: only `IX_Tasks_UserId_CreatedAt` (`UserId`, `CreatedAt`). It serves the default list query, and it also works as the `UserId` foreign-key index (EF Core skips the separate FK index when a composite index starts with `UserId`). Add more indexes only after measuring slow queries.

### 6.3 Enums
```
TaskItemStatus   { Todo, InProgress, Done }   // JSON: "todo" | "inProgress" | "done"
TaskItemPriority { Low, Medium, High }        // JSON: "low" | "medium" | "high"
```

### 6.4 Other tables
- `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`: created by `IdentityUserContext`. They are unused by the MVP and cascade-delete with the user.
- `DataProtectionKeys`: persisted Data Protection key ring (FR-A10).

### 6.5 Relationships
- `User` 1 --- * `TaskItem`. Deleting the account deletes the user's tasks (FR-ACC3).

---

## 7. Proposed folder structure

### 7.1 Backend (`/backend`)
```
backend/
  TaskManager.sln
  .env.example
  src/
    TaskManager.Api/
      Program.cs                    # DI, provider switch, schema step, Identity + cookie, CSRF, rate limiting, JSON, errors
      appsettings.json              # non-secret defaults only; no Database:Provider; Database:Schema=None
      appsettings.Development.json  # Database:Provider=Sqlite, Data Source=taskmanager.db, Database:Schema=EnsureCreated
      taskmanager.db                # created locally at runtime; git-ignored; delete after model changes
      Controllers/
        AuthController.cs           # register, login, logout, me
        AccountController.cs        # profile, change-password, delete
        TasksController.cs
      Dtos/
        Auth/                       # RegisterRequest, LoginRequest, UserDto
        Account/                    # UpdateProfileRequest, ChangePasswordRequest, DeleteAccountRequest
        Tasks/                      # TaskDto, CreateTaskRequest, UpdateTaskRequest, TaskListQuery, TaskSortBy, SortDirection
        Common/                     # ApiError, ApiErrorCodes
      Entities/
        User.cs                     # : IdentityUser<Guid>
        TaskItem.cs
        TaskItemStatus.cs
        TaskItemPriority.cs
      Data/
        AppDbContext.cs             # IdentityUserContext<User, Guid>, IDataProtectionKeyContext; per-provider collation
        AppDbContextDesignTimeFactory.cs # UseSqlServer + placeholder connection string, for `dotnet ef` only
        DatabaseServiceExtensions.cs # Database:Provider switch + Database:Schema startup step and its guards
        Configurations/
        Conventions/UtcDateTimeConverter.cs
        LikePattern.cs              # provider-aware escaping for search
        Migrations/                 # SQL Server migrations (the only set)
      Services/
        ITaskService.cs / TaskService.cs
        IAccountService.cs / AccountService.cs
      Auth/
        CookieAuthEvents.cs         # 401/403 ApiError instead of redirects
        CsrfHeaderMiddleware.cs
        CurrentUserExtensions.cs
        IdentityErrorMapper.cs      # IdentityError -> validation_failed / email_taken
      Json/
        StrictDateTimeOffsetConverter.cs
      Mapping/
        TaskMappings.cs, UserMappings.cs
      Validation/
      Middleware/
        ExceptionHandlingMiddleware.cs
  tests/
    TaskManager.Api.UnitTests/          # xUnit: services, validators, sorting, due-date rule, LIKE escaping, error mapping
    TaskManager.Api.IntegrationTests/   # xUnit + WebApplicationFactory + SQLite in-memory (SQL Server + migrations via env in CI) + FakeTimeProvider
.github/
  workflows/
    ci.yml                          # build + tests (SQLite and SQL Server) + migrations check + frontend + E2E
    deploy-api.yml                  # publish + deploy to Azure App Service
```

### 7.2 Frontend (`/frontend`)
```
frontend/
  .env.example
  next.config.ts                    # rewrites /api/:path* -> ${BACKEND_URL}/api/:path*
  playwright.config.ts              # webServer starts the backend (dotnet run) and the frontend
  tsconfig.json                     # strict: true
  app/
    layout.tsx
    page.tsx                        # -> /tasks or /login
    not-found.tsx
    (public)/
      login/page.tsx
      register/page.tsx
    (app)/
      layout.tsx                    # AuthGuard + AppHeader
      tasks/page.tsx
      tasks/new/page.tsx
      tasks/[id]/page.tsx
      account/page.tsx
  components/
    auth/      LoginForm, RegisterForm, AuthGuard, LogoutButton
    account/   ProfileForm, ChangePasswordForm, DeleteAccountSection
    layout/    AppHeader
    tasks/     TaskList, TaskListItem, TaskFilters, TaskSortControl, TaskSearchInput, TaskForm,
               TaskStatusSelect, StatusBadge, PriorityBadge, DueDateLabel, DueDateTimeInput, DeleteTaskButton
    ui/        Button, TextField, PasswordField, Select, ConfirmDialog, FormError, ErrorMessage, EmptyState, Spinner
  lib/
    api/
      client.ts                     # relative base "/api/v1", same-origin credentials, CSRF header, ApiError + non-ApiError handling
      auth.ts, account.ts, tasks.ts
    auth/
      AuthContext.tsx               # current user, login/logout/refresh
      safeReturnUrl.ts
    tasks/
      queryParams.ts                # URL params <-> TaskListQuery
      isOverdue.ts
      dateTime.ts                   # local datetime-local value <-> UTC ISO string
  types/
    api.ts
  tests/
    unit/                           # Vitest + React Testing Library
    e2e/                            # Playwright
```

---

## 8. Frontend pages and components

### 8.1 Pages

| Route | Auth | Purpose |
|---|---|---|
| `/` | - | Goes to `/tasks` if `GET /auth/me` succeeds, otherwise to `/login`. |
| `/login` | public | `LoginForm`. On success, goes to `returnUrl` or `/tasks`. Links to `/register`. |
| `/register` | public | `RegisterForm` (display name, email, password, confirm password). On success, goes to `/tasks`. |
| `/tasks` | required | All tasks, with filters, search, sort, and a "New task" button. The filter state lives in the URL. |
| `/tasks/new` | required | `TaskForm` in create mode. |
| `/tasks/[id]` | required | `TaskForm` in edit mode, plus `DeleteTaskButton`. Shows "Task not found" on a 404. |
| `/account` | required | `ProfileForm`, `ChangePasswordForm`, and `DeleteAccountSection`. |

- `returnUrl` is only accepted if it is a relative path that starts with `/` and not `//` (`safeReturnUrl`).
- Logged-in users who visit `/login` or `/register` are sent to `/tasks`.
- `AuthGuard` (in the `(app)` layout) calls `GET /auth/me` on mount. On `unauthorized`, it redirects to `/login?returnUrl=...`. If the server is unavailable (see 5.2), it shows the retry message instead of redirecting.

### 8.2 Component responsibilities

| Component | Responsibility |
|---|---|
| `LoginForm`, `RegisterForm` | Collect input, validate on the client, call the API, and show field or general errors. |
| `AuthGuard` | Blocks rendering until the session is confirmed, otherwise redirects. |
| `LogoutButton` | Calls `POST /auth/logout`, clears the auth context, and goes to `/login`. |
| `AppHeader` | App name, links to Tasks and Account, the user's display name, `LogoutButton`. |
| `ProfileForm` | Edit the display name. Updates the auth context on success. |
| `ChangePasswordForm` | Current, new, and confirm-new password fields. Shows a success notice and clears the fields. |
| `DeleteAccountSection` | Explains that deletion is permanent. Asks for the password, then `ConfirmDialog`, then calls the API and goes to `/login`. |
| `TaskFilters` | Status and priority selects (with an "All" option). |
| `TaskSearchInput` | Text input, debounced 300 ms, writes `search` to the URL. |
| `TaskSortControl` | `sortBy` select plus an asc/desc toggle. |
| `TaskList` / `TaskListItem` | Render the tasks / one row (title link, badges, `DueDateLabel`, `TaskStatusSelect`). Shows `EmptyState` when there are none. |
| `TaskStatusSelect` | `PUT /tasks/{id}` with the task's other fields unchanged, including `dueDate`. |
| `StatusBadge` / `PriorityBadge` | Show the enum as a colored label. |
| `DueDateLabel` | Shows the due date and time in local time (`Intl.DateTimeFormat`), and "Overdue" when `isOverdue`. |
| `DueDateTimeInput` | `<input type="datetime-local">` with `min` set to now. Converts the local value to a UTC ISO string (`Z`) and back. Has a clear button that sets `null`. |
| `TaskForm` | Create/edit form with validation. In edit mode, an unchanged past due date is allowed. |
| `DeleteTaskButton` | `ConfirmDialog`, then DELETE, then `/tasks`. |

### 8.3 URL query behavior (`/tasks`)
- Query keys match the API: `status`, `priority`, `search`, `sortBy`, `sortDir`.
- Reloading the page or sharing the URL brings back the same view.
- Invalid values in the URL are dropped (the default is used) and are never sent to the API.

---

## 9. Acceptance criteria

Each criterion has an ID.
- "API" criteria need backend integration tests (5.6). They run on SQLite in-memory, and again on SQL Server with the real migrations in CI.
- "UI" criteria need frontend unit and/or Playwright tests.
- Unless a criterion says otherwise, every unsafe API request in a test includes `X-CSRF-Protection: 1`.
- Test data for case-insensitivity uses **ASCII** letters, which both providers fold (6.0).

### Auth and sessions
- **AC-AUTH-1 (API)**: Register with a valid display name, email, and password returns `201` and a `UserDto`. `email` is lowercased and `displayName` is trimmed. The response sets `tm_session` (HttpOnly, SameSite=Lax, Path=/, no Expires/Max-Age, and Secure when `Auth__CookieSecure=Always`). `GET /auth/me` with that cookie returns `200`.
- **AC-AUTH-2 (API)**: Registering an existing email, in any letter case or with extra spaces, returns `409 email_taken`.
- **AC-AUTH-3 (API)**: Register with an invalid email, a blank display name or one over 50 chars, or a password that breaks a rule returns `400 validation_failed`, keyed by the matching field.
- **AC-AUTH-4 (API)**: Login with correct credentials returns `200` with a `UserDto` and sets the cookie.
- **AC-AUTH-5 (API)**: A wrong password and an unknown email both return `401 invalid_credentials` with the same body (apart from `traceId`).
- **AC-AUTH-6 (API)**: After 5 failed logins for one account, a 6th attempt **with the correct password** returns `401 invalid_credentials`. After 15 minutes (FakeTimeProvider), login succeeds.
- **AC-AUTH-7 (API)**: A protected endpoint with no cookie or a tampered cookie returns `401 unauthorized` as `ApiError` JSON. It is **never** a 3xx and never has a `Location` header.
- **AC-AUTH-8 (API)**: `POST /auth/logout` returns `204` and expires the cookie. Without a session, it still returns `204`.
- **AC-AUTH-9 (API)**: A session with no activity for more than `Auth__SessionHours` (FakeTimeProvider) returns `401`. A session used regularly within the window stays valid past 8 hours in total (sliding).
- **AC-AUTH-10 (API)**: The 11th rate-limited request from one client IP within the window returns `429 rate_limited` with `Retry-After`.
- **AC-CSRF-1 (API)**: `POST`, `PUT`, or `DELETE` to any `/api/v1` endpoint (including login and register) **without** `X-CSRF-Protection: 1` returns `403 csrf_failed`, and nothing changes. A `GET` without the header works.
- **AC-AUTH-11 (UI)**: After registering or logging in, the user lands on `/tasks` and the header shows their display name.
- **AC-AUTH-12 (UI)**: Opening `/tasks` without a session redirects to `/login?returnUrl=%2Ftasks`. After logging in, the user is sent back to `/tasks`. A `returnUrl` of `//evil.com` is ignored.
- **AC-AUTH-13 (UI)**: Logout goes to `/login`. After that, browser back to `/tasks` redirects to login again.
- **AC-AUTH-14 (UI)**: The register form shows client-side errors for a blank display name, an invalid email, a weak password, and a confirmation that doesn't match, without calling the API.
- **AC-PROXY-1 (E2E)**: With the frontend served by Next.js and the API behind the rewrite, the browser makes no requests to the backend origin. The `tm_session` cookie is set on the frontend origin.

### Account
- **AC-PROFILE-1 (API)**: `PUT /account/profile` with a valid display name returns `200` with the updated `UserDto`. `GET /auth/me` shows the new name.
- **AC-PROFILE-2 (API)**: A blank display name, or one over 50 chars after trimming, returns `400` keyed `displayName`.
- **AC-PWD-1 (API)**: Change password with the correct `currentPassword` returns `204`. The same session still gets `200` on `GET /auth/me`. Login with the old password fails and the new one works.
- **AC-PWD-2 (API)**: A wrong `currentPassword` returns `400 validation_failed` keyed `currentPassword`, and the status is not 401.
- **AC-PWD-3 (API)**: A `newPassword` that breaks the rules, or equals `currentPassword`, returns `400` keyed `newPassword`.
- **AC-PWD-4 (API)**: A second session for the same user (a separate cookie jar) gets `401` after the change, once the security stamp validation interval has passed. Tests set `Auth__SecurityStampValidationSeconds=0`.
- **AC-DELACCT-1 (API)**: `POST /account/delete` with the correct password returns `204` and expires the cookie. `GET /auth/me` then returns `401` and login fails. The user's tasks are removed from the database by the cascade, which also confirms SQLite foreign keys are on. Another user's tasks are untouched.
- **AC-DELACCT-2 (API)**: A wrong password returns `400` keyed `password`, and the account and its tasks remain.
- **AC-ACCT-UI-1 (UI)**: The account page updates the display name, and the header shows the new name without a reload.
- **AC-ACCT-UI-2 (UI)**: The change-password form shows an inline error for a wrong current password and a success notice on success. The user stays logged in.
- **AC-ACCT-UI-3 (UI)**: Deleting the account requires the password and a confirmation. Cancel does nothing. Confirm goes to `/login`.

### Create task
- **AC-CREATE-1 (API)**: `POST /tasks` with only `title` returns `201`, `Location: /api/v1/tasks/{id}` (a relative path), and a `TaskDto` with the defaults, `dueDate = null`, and `createdAt == updatedAt`.
- **AC-CREATE-2 (API)**: A missing or blank title, a title over 200 chars, a description over 2000 chars, an invalid or numeric enum, or a malformed `dueDate` returns `400` with the matching field key.
- **AC-CREATE-3 (API)**: `title` and `description` are trimmed. A description that is only whitespace is stored as `null`.
- **AC-CREATE-4 (API)**: A `userId` in the body is ignored. The task belongs to the caller.
- **AC-CREATE-5 (UI)**: An empty title shows an inline error and makes no API call. A valid submit goes to `/tasks` and the task appears.

### Due date
- **AC-DUE-1 (API)**: Create with `dueDate` 1 hour in the future returns `201`.
- **AC-DUE-2 (API)**: Create with `dueDate` in the past, or equal to the current time (FakeTimeProvider), returns `400` keyed `dueDate`.
- **AC-DUE-3 (API)**: `"2026-10-12T17:00:00+02:00"` is stored and returned as `"2026-10-12T15:00:00Z"`. `"2026-10-12T15:00:00.789Z"` is returned as `"2026-10-12T15:00:00Z"`.
- **AC-DUE-4 (API)**: A `dueDate` with no offset (`"2026-10-12T15:00:00"`) or a date only (`"2026-10-12"`) returns `400` keyed `dueDate`.
- **AC-DUE-5 (API)**: For a task whose stored due date has passed (FakeTimeProvider advanced), a `PUT` that sends the **same instant** returns `200`. The same instant written with a different offset counts as the same. The `PUT` may change other fields, for example `status = done`.
- **AC-DUE-6 (API)**: A `PUT` that changes `dueDate` to a different past instant, or from `null` to a past instant, returns `400` keyed `dueDate`.
- **AC-DUE-7 (API)**: A `PUT` that sets `dueDate` to `null` on an overdue task returns `200`. A `PUT` that sets it to a future instant returns `200`.
- **AC-DUE-8 (UI)**: The due date input shows and accepts local time. The value sent to the API is the matching UTC `Z` string. Picking a past time in the create form shows an inline error.
- **AC-DUE-9 (UI)**: Changing the status of an overdue task from the list works (no due-date error).

### List / filter / search / sort
- **AC-LIST-1 (API)**: `GET /tasks` returns a JSON **array** with only the caller's tasks. A user with no tasks gets `[]`.
- **AC-LIST-2 (API)**: With no parameters, the result is sorted `createdAt desc`.
- **AC-LIST-3 (API)**: `status` and `priority` filters each work, and using both combines them with AND.
- **AC-LIST-4 (API)**: `search=MiLk` matches "milk" in the title or the description (ASCII, so it ignores case on both providers). `search=%`, `search=_`, and `search=\` each match only tasks that contain that literal character. `search=[` matches only tasks that contain a literal `[`, which also exercises the SQL Server-only escape in the SQL Server CI run.
- **AC-LIST-5 (API)**: `sortBy=priority&sortDir=asc` puts low first. `sortBy=dueDate` puts tasks with a null due date last for both `asc` and `desc`, and orders due dates by instant.
- **AC-LIST-6 (API)**: `sortBy=title` is case-insensitive: "apple" < "Banana" < "cherry" (ASCII, through `NOCASE` on SQLite and the CI collation on SQL Server).
- **AC-LIST-7 (API)**: An invalid `status`, `priority`, `sortBy`, or `sortDir`, or a `search` over 100 chars, returns `400 validation_failed`.
- **AC-LIST-8 (UI)**: Changing a filter, the search, or the sort updates the URL and the list. Reloading the page keeps the same view.
- **AC-LIST-9 (UI)**: A task with a due date before now and a status other than `done` shows "Overdue". The same task with status `done` does not.
- **AC-LIST-10 (UI)**: When there are no results, an empty state is shown. When the user has no tasks at all, it offers "Create your first task".

### View / edit / delete task
- **AC-EDIT-1 (API)**: `GET /tasks/{id}` for the caller's own task returns `200 TaskDto`.
- **AC-EDIT-2 (API)**: `GET`, `PUT`, or `DELETE` on another user's task, a task id that doesn't exist, or a malformed id returns `404 not_found`. The other user's task stays unchanged.
- **AC-EDIT-3 (API)**: A valid `PUT` replaces the fields, refreshes `updatedAt`, and leaves `createdAt` unchanged. Sending `null` for `description` or `dueDate` clears them.
- **AC-EDIT-4 (API)**: A `PUT` missing `status`, `priority`, or the `description`/`dueDate` **keys** returns `400`.
- **AC-EDIT-5 (UI)**: The edit page loads the current values into the form (due date in local time). Saving goes to `/tasks`. A task id that doesn't exist shows "Task not found".
- **AC-EDIT-6 (UI)**: A status change from the list updates the badge without a reload. The other fields stay unchanged on the server.
- **AC-DEL-1 (API)**: `DELETE /tasks/{id}` returns `204`, and a later `GET` returns `404`.
- **AC-DEL-2 (UI)**: Delete asks for confirmation. Cancel keeps the task. Confirm deletes it and goes to `/tasks`.

### Cross-cutting
- **AC-ERR-1 (API)**: Every non-2xx response the backend produces under `/api/v1/*` (including 401, 403, 404, 405, 429, and 500) has the `ApiError` body with `status`, `code`, `message`, `errors`, and `traceId` all present. `GET /health` is excluded.
- **AC-ERR-2 (API)**: Malformed JSON returns `400 validation_failed`, not 500.
- **AC-ERR-3 (API)**: An unhandled exception returns `500 internal_error` with no stack trace or exception text.
- **AC-ERR-4 (UI)**: An `unauthorized` error on a protected page redirects to `/login?returnUrl=...`. The `invalid_credentials` 401 on the login form does not redirect.
- **AC-ERR-5 (UI)**: A network failure, or a `502`/`503`/`504` response with a non-JSON body (mocked), shows the "server is starting up or unavailable" message. There is no crash and no redirect to login.
- **AC-DB-1 (API)**: The app fails to start when `Database__Provider` is missing or unknown, or when `Database__Schema` doesn't fit the provider (`Migrate` + `Sqlite`, `EnsureCreated` + `SqlServer`).
- **AC-DB-2 (CI)**: In the SQL Server CI job, applying all migrations to an empty database succeeds, and the full API suite passes on it. `dotnet ef migrations has-pending-model-changes` reports no pending changes.
- **AC-HEALTH-1 (API)**: `GET /health` returns `200` without auth when the database is reachable.

---

## 10. Open questions

None. Every question raised so far has been answered by the user (see section 11).

---

## 11. Decisions log (user-confirmed, 2026-10-10)

| Topic | Decision |
|---|---|
| Database | **SQLite for local dev and tests, SQL Server in production.** The provider is chosen by config: `Database__Provider` = `Sqlite` (dev and tests) / **`SqlServer` (production)**. The data model is provider-neutral: UTC `DateTime` only, per-provider collations, LINQ only (6.0). |
| Migrations | **Migrations target SQL Server from day one; SQLite uses EnsureCreated.** There is one migrations set, generated without a database through `AppDbContextDesignTimeFactory`. It is applied in the SQL Server CI job on every run and in production at app startup. Locally, delete `taskmanager.db` after model changes. |
| API docs (added 2026-10-11) | **Swagger UI at `/swagger`**, reading the built-in .NET OpenAPI document at `/openapi/v1.json`. Both exist only when `OpenApi__Enabled=true` (set in `appsettings.Development.json`; never in production). Swagger UI adds the `X-CSRF-Protection: 1` header to every request; the session cookie works because Swagger UI and the API share an origin. |
| Tests | `WebApplicationFactory` + SQLite in-memory (`EnsureCreated`). Testcontainers is not used. The same suite runs against SQL Server in GitHub Actions on every CI run, with the real migrations applied (`Database__Schema=Migrate`), and it must pass before any deploy. |
| Backend host | **Azure App Service, Free F1 (Windows, 32-bit worker; changed from Linux on 2026-10-16), .NET 10 runtime**, with a self-contained `win-x86` publish as the fallback if the plan doesn't offer .NET 10. It is reached as `https://<app>.azurewebsites.net`, and Vercel's `BACKEND_URL` points there. **Trade-offs:** <br>- **Cold starts:** F1 has no Always On, so the app unloads when idle and the first request afterwards is slow. <br>- **Daily CPU quota:** once the F1 quota is used up, the app stops until the next day. <br>- **No scale-out and no deployment slots.** <br>- **No uptime pinger on `/health`.** |
| Production database | **Azure SQL Database, free offer** (serverless), set to **auto-pause the database when the free monthly limit is reached**, so it never bills. **Trade-offs:** <br>- After the auto-pause delay, the database pauses while idle. Resuming it can take up to about a minute. This is mitigated by `EnableRetryOnFailure` and `Connect Timeout=60`. <br>- If the monthly allowance runs out, the database stays paused until the next month. <br>- The firewall allows Azure services (so App Service can reach it). |
| Production config | `Database__Provider=SqlServer`. The connection string lives only as an App Service connection string (never in the repo). `Database__Schema=Migrate`, `Auth__CookieSecure=Always`, `RateLimiting__UseForwardedFor=true`, `ASPNETCORE_ENVIRONMENT=Production`, and HTTPS Only is on. |
| Runtime | **.NET 10.** |
| Auth | **Cookie sessions** with ASP.NET Core Identity. HttpOnly, Secure, SameSite=Lax cookie `tm_session`. CSRF through the required `X-CSRF-Protection: 1` header. `POST /auth/logout`. 401/403 are JSON, never redirects. |
| Session | No "remember me" and no refresh. A browser-session cookie with an **8-hour sliding** lifetime. |
| Display name | **Yes**: required, 1-50 chars, not unique. |
| Password rules | 8-128 chars, at least one letter and one digit. |
| Past due dates | **Not allowed** on create. On update, rejected only when the value changes to a past instant; an unchanged past value is accepted (FR-T10). The rule is confirmed as written. |
| Due date and time | **Date and time.** Stored as UTC `DateTime`. The API accepts ISO 8601 with an offset or `Z` and returns `Z`. The UI shows local time. |
| Statuses | `todo`, `inProgress`, `done`. |
| Task delete | Hard delete. |
| Update style and concurrency | **`PUT` full replace, last-write-wins**, also for the quick status change from the list. No PATCH, no status endpoint, no concurrency token. |
| Pagination | **None.** `GET /tasks` returns a plain array. Filters and sort stay. |
| Account management | **Change password, update display name, and delete account** are in scope. **Forgot/reset password is cancelled for now** (moved to future work). The MVP sends no email, and no email provider is needed. |
| API versioning | Keep the **`/api/v1`** URL prefix. |
| Frontend hosting | **Vercel.** Next.js rewrites proxy `/api/*` to the backend so the cookie belongs to the Vercel origin. |
| Indexes | Only `IX_Tasks_UserId_CreatedAt`. Add more only after measuring. |
| Hosting and local database (added 2026-10-16) | **Windows App Service** (Free F1, Italy North, .NET 10) instead of Linux. Free Windows plans run a 32-bit worker only, so the deploy publishes framework-dependent AnyCPU (`-p:UseAppHost=false`) on a Windows runner; fallback: self-contained `win-x86`. Deploy uses the publish profile secret `AZURE_WEBAPP_PUBLISH_PROFILE`, only after CI succeeds on `main`, then polls `/health`. **One Azure SQL database** serves both production and the developer's local runs for now (no separate dev database), so the tables are emptied before real users. Local runs reach it through **.NET user secrets** (`Database:Provider`, `Database:Schema`, `ConnectionStrings:Default`; never in the repo); `dotnet user-secrets clear` switches back to SQLite. Tests always use SQLite or a throwaway CI SQL Server, never Azure. |

---

## 12. Task split

All agents build against `docs/API.md` and `frontend/types/api.ts`. A contract change must update API.md, the C# DTOs, and `api.ts` together.

### Backend agent
- Scaffold `backend/` as in 7.1 (.NET 10 solution, API project, two test projects). Reference both EF Core providers (Sqlite and SqlServer) and `Microsoft.EntityFrameworkCore.Design`.
- Register the `Database__Provider` switch, and fail at startup if the value is missing or unknown.
  - For SqlServer, use `EnableRetryOnFailure(6, 30s)`.
  - Add the `Database__Schema` startup step (`Migrate` / `EnsureCreated` / `None`), with the provider guards from 5.5.
- Entities (`User : IdentityUser<Guid>`, `TaskItem`) and `AppDbContext : IdentityUserContext<User, Guid>, IDataProtectionKeyContext`, following every rule in 6.0:
  - max lengths;
  - per-provider `UseCollation` on `Title` and `Description`;
  - string-stored status and int-stored priority;
  - cascade delete;
  - the single `IX_Tasks_UserId_CreatedAt` index;
  - the model-wide UTC `DateTime` converter;
  - default GUID generation;
  - no `DateTimeOffset` in app-owned columns;
  - no explicit transactions.
- **Create `AppDbContextDesignTimeFactory`** (6.0) and the **initial SQL Server migration** (`InitialCreate`) in `Data/Migrations` with the documented `dotnet ef migrations add` command. Add a new migration for every later model change. Never create SQLite migrations.
- `appsettings.Development.json`: `Database:Provider=Sqlite`, `ConnectionStrings:Default=Data Source=taskmanager.db`, `Database:Schema=EnsureCreated`.
- Identity setup per 4.1 (password options, lockout, cookie options and events, security stamp interval, Data Protection keys in the database).
- `CsrfHeaderMiddleware` and rate limiting (forwarded-IP partitioning).
- JSON setup: camelCase, string enums with no integers, the strict `DateTimeOffset` converter for requests, nulls always written, UTC `Z`.
- `AuthController`, `AccountController`, `TasksController`, and `/health` (with an EF database check), exactly as in API.md.
  - Every error goes through `ApiError` (model state factory, exception middleware, cookie events, rate limiter rejection, status-code fallback for routing 404/405/415).
  - **LINQ only, no raw SQL.** Provider-aware LIKE escaping lives in `LikePattern`.
- Use `TimeProvider` everywhere. Unit tests for services, validators, the due-date rule, LIKE escaping (both providers), sorting, and the Identity error mapping. Write `backend/.env.example`.

### Frontend agent
- Scaffold `frontend/` as in 7.2 (Next.js App Router, TS strict, Tailwind, ESLint `no-explicit-any: error`).
- `next.config.ts` rewrites `/api/:path*` -> `${BACKEND_URL}/api/:path*`. Never call the backend origin from the browser.
- Use `frontend/types/api.ts` as given.
- `lib/api/client.ts`:
  - relative `/api/v1` base, same-origin credentials, and `X-CSRF-Protection: 1` on POST/PUT/DELETE;
  - parse `ApiError` responses, and handle **non-ApiError responses** (network errors, proxy 502/504, the App Service quota page) with the retry message (5.2);
  - no global redirect; `AuthGuard` handles redirects.
- All pages and components in section 8, including the account page. Local-time due date handling (`dateTime.ts`, `DueDateTimeInput`). Overdue display. Filter state in the URL.
- Unit tests (Vitest + RTL) for components, `queryParams`, `isOverdue`, `dateTime`, `safeReturnUrl`, and the API client (including the non-ApiError case). Write `frontend/.env.example` (`BACKEND_URL`).

### Tester agent
- Backend integration tests with `WebApplicationFactory` + **SQLite in-memory**, set up as in 5.6:
  - one kept-open connection per factory, with `Database__Schema=EnsureCreated`, and unique users per test;
  - `FakeTimeProvider`;
  - a cookie-aware `HttpClient` with base address `https://localhost` (so Secure cookies are sent) and the CSRF header added by default.
- Cover every **API** acceptance criterion, with a two-user isolation test for each task and account endpoint.
- Make the factory provider-switchable through `TEST_DATABASE_PROVIDER` / `TEST_DATABASE_CONNECTION`. In SqlServer mode, the factory uses a uniquely named database per factory, sets `Database__Schema=Migrate` so the **real migrations** are applied, and drops the database on dispose.
- Contract tests: field names and types match API.md (camelCase, string enums, `Z` timestamps, a plain array for the list, `errors`/`traceId` always present on errors, relative `Location`).
- Playwright E2E for every **UI** criterion and AC-PROXY-1. Run against the local stack started by Playwright's `webServer` (backend `dotnet run` with a fresh SQLite file, plus Next.js). No Docker needed.
- Name each test with its AC ID. Report any criterion that cannot be tested.

### DevOps agent
- Root `.gitignore`, including bin/obj, node_modules, .next, `.env*` except `.env.example`, `*.db`, `*.db-shm`, `*.db-wal`, and publish profiles (`*.PublishSettings`). Also `git init`.
- Root `README.md`: the local run steps from 5.4 (including "delete `taskmanager.db` after model changes"), how to add a migration (6.0), the env vars, the URLs, and the Azure setup below.
- **CI** (`.github/workflows/ci.yml`, on every push and PR). Any failing test fails the run.
  - **Backend:** build, unit tests, and integration tests on SQLite in-memory.
  - **Migrations check:** `dotnet ef migrations has-pending-model-changes` (6.0). Install `dotnet-ef` in the job. It uses the design-time factory, so no database is needed.
  - **SQL Server job:** the integration suite against a `mcr.microsoft.com/mssql/server:2022-latest` service container, with `TEST_DATABASE_PROVIDER=SqlServer` and `Database__Schema=Migrate`. This applies the real migrations on every run (AC-DB-2).
  - **Frontend:** lint, `tsc --noEmit`, unit tests, and `next build`.
  - **E2E:** Playwright against the `webServer` stack.
- **One-time Azure portal setup** (document it step by step in the README):
  1. Create an Azure account and subscription.
  2. Create a **resource group** (for example `rg-taskmanager`) in one region, and put everything below in it.
  3. Create an **Azure SQL logical server**, with SQL authentication and an admin login. Store the admin password in a password manager, never in the repo.
  4. Create the database with the **free offer** applied (serverless). Set "Behavior when free limit is reached" to **Auto-pause the database until next month**.
  5. On the server's **networking**, turn on "**Allow Azure services and resources to access this server**". Optionally add your own client IP for admin tools.
  6. Create a dedicated contained database user for the app. It needs read/write and DDL rights, because migrations run at startup.
  7. Create an **App Service plan: Windows, Free F1** (32-bit worker only).
  8. Create a **web app** on that plan with the **.NET 10** runtime stack. If .NET 10 isn't offered, use the self-contained `win-x86` fallback from 5.4.
  9. Turn **HTTPS Only** on.
  10. Set the app settings `Database__Provider=SqlServer`, `Database__Schema=Migrate`, `Auth__CookieSecure=Always`, `RateLimiting__UseForwardedFor=true`, and `ASPNETCORE_ENVIRONMENT=Production`.
  11. Add the **connection string** `Default` (type `SQLAzure`, including `Connect Timeout=60`).
  12. Set up deployment credentials for GitHub Actions. **Recommended for simplicity: a publish profile.** Enable "SCM Basic Auth Publishing Credentials" on the web app, download the publish profile, and store it as the GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`. The alternative is OIDC (an app registration with a federated credential, and `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` as GitHub secrets). It has no long-lived secret but takes more setup steps.
  13. In **Vercel**, set the project root directory to `frontend`. Set `BACKEND_URL=https://<app>.azurewebsites.net` for Production and Preview, then redeploy.
- **Deploy workflow** (`.github/workflows/deploy-api.yml`): it runs on push to `main` **only after CI passes**, including the SQL Server job and the migrations check.
  1. `dotnet publish -c Release -p:UseAppHost=false` on a Windows runner (framework-dependent, AnyCPU, so it runs in the 32-bit worker; self-contained `win-x86` if needed).
  2. `azure/webapps-deploy` using the publish profile secret, or `azure/login` with OIDC.
- **Applying migrations in production: at app startup** (`Database__Schema=Migrate` runs `Database.Migrate()` when the app boots). Why this option:
  - The Azure SQL firewall already lets App Service in ("Allow Azure services"). GitHub-hosted runners have changing IPs, so running a migration bundle from CI would need temporary firewall rules or an open server.
  - F1 runs a single instance, so there's no race between instances, and EF Core takes a migration lock anyway.
  - Downsides: the first start after a deploy is slower, and a failing migration stops the app from starting. That shows up in the App Service log stream, and you fix it by deploying a new version. The CI SQL Server job, which applies the same migrations on every run, makes this unlikely.
- Don't set up an uptime pinger or keep-alive on `/health`.

### Reviewer agent (checklist)
- DTOs and `api.ts` match API.md field for field. No EF entity is returned from a controller. No `any`. No hardcoded secrets (no connection strings for SQL Server or Azure in the repo).
- No raw SQL. No `DateTimeOffset` in app-owned entity columns. Provider-specific code exists only behind `IsSqlite()`/`IsSqlServer()` checks. Migrations exist for SQL Server only, and every model change comes with a migration.
- Every `/api` error uses `ApiError`, and there are no auth redirects. The CSRF header is enforced on unsafe methods.
- Every task query is filtered by the current user. Each AC has a test.
