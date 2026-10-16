# Task Manager - API Contract (v1)

Status: Draft v4 (2026-10-10). This document is the source of truth. The C# DTOs (section 6) and `frontend/types/api.ts` must match it field for field. If one changes, all three change together. The `/api/v1` prefix is confirmed by the user. The contract does not depend on the database provider (SPEC 6.0).

---

## 1. Conventions

| Topic | Rule |
|---|---|
| Base URL (browser) | `/api/v1` is a **relative path on the frontend (Vercel) origin**. Next.js rewrites `/api/:path*` to `${BACKEND_URL}/api/:path*`. The browser never calls the backend origin. |
| Base URL (backend) | `{BACKEND_URL}/api/v1` (local: `http://localhost:5000/api/v1`) |
| Versioning | URL prefix `/api/v1`. Breaking changes need `/api/v2`. |
| Content type | Requests and responses use `application/json; charset=utf-8`. Exceptions: `204` responses (no body) and `GET /health`. |
| JSON naming | **camelCase** for every property and query parameter. C# uses PascalCase and is serialized with `JsonNamingPolicy.CamelCase`. |
| Enums | camelCase strings: `"todo" \| "inProgress" \| "done"` and `"low" \| "medium" \| "high"`. Integer values are rejected. |
| IDs | GUID strings, always lowercase in JSON, for example `"3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14"`. Path ids are matched case-insensitively. |
| Timestamps (responses) | ISO 8601 **UTC** with `Z`. `dueDate` is in whole seconds, for example `"2026-10-12T15:00:00Z"`. `createdAt`/`updatedAt` may include fractional seconds. |
| Timestamps (requests) | `dueDate` must be ISO 8601 with a **required** offset or `Z` (`"2026-10-12T17:00:00+02:00"` or `"2026-10-12T15:00:00Z"`). The server converts it to UTC and truncates it to whole seconds. Values with no offset are rejected. |
| Nulls | Nullable response fields are **always present** and set to `null` when empty. |
| Unknown request fields | Ignored. |
| Trimming | `email`, `displayName`, `title`, `description`, and `search` are trimmed by the server. Passwords are **never** trimmed. |
| Case-insensitivity | Wherever this contract says "case-insensitive" (search, title sort), it is guaranteed for **ASCII letters**. Folding of non-ASCII letters depends on the database provider (SPEC 6.0). |
| URLs in headers | Always relative (for example `Location: /api/v1/tasks/{id}`), because of the proxy. |

---

## 2. Authentication, session and CSRF

- **Scheme**: an ASP.NET Core Identity cookie session. Register and login set the cookie, and the browser sends it automatically. Client code never reads it (it is `HttpOnly`).
- **Cookie**:
  - Name `tm_session`. Attributes: `HttpOnly`, `SameSite=Lax`, `Path=/`, no `Domain`, and `Secure` (whenever `Auth__CookieSecure=Always`, which is required in every deployed environment).
  - It is a browser-session cookie (no `Expires`/`Max-Age`).
  - Lifetime: **8 hours, sliding** (`Auth__SessionHours`).
- **Unauthenticated**: an endpoint marked **Auth: Yes** that is called with no session, or an invalid or expired one, returns `401 unauthorized` (`ApiError`). The API **never** returns a 3xx redirect.
- **CSRF**:
  - Every `POST`, `PUT`, `PATCH`, and `DELETE` request under `/api/` must send `X-CSRF-Protection: 1`. This includes unauthenticated ones such as login and register.
  - Without it, the request gets `403 csrf_failed`. The check runs before authentication and validation, so nothing changes.
  - `GET` requests don't need the header.
- **CORS**: none. The API sends no CORS headers, so cross-origin browser calls fail by design.
- **Rate limited** endpoints (marked below) are limited per client IP. The default is 10 requests per 60 seconds. Over the limit, they return `429 rate_limited` with a `Retry-After` header (seconds).

---

## 3. Error response shape

**Every** non-2xx response from `/api/v1/*` uses this body. That includes model validation, malformed JSON, 401, CSRF 403, routing 404, 405, 415, 429, and unhandled exceptions. `GET /health` is the only exception (5.13).

```json
{
  "status": 400,
  "code": "validation_failed",
  "message": "One or more fields are invalid.",
  "errors": {
    "title": ["Title is required."],
    "dueDate": ["Due date must be in the future."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Field | Type | Notes |
|---|---|---|
| `status` | number | Same as the HTTP status code. |
| `code` | `ApiErrorCode` (string) | Machine-readable. **The frontend branches on `code`, never on `message`.** |
| `message` | string | Short, readable text that is safe to show. |
| `errors` | `Record<string, string[]> \| null` | Not null **only** when `code = "validation_failed"`. Keys are camelCase field or query-parameter names. If malformed JSON can't be tied to a field, the key is `body`. |
| `traceId` | string | `Activity.Current?.Id ?? HttpContext.TraceIdentifier` |

### Error codes

| `code` | HTTP | When | Default `message` |
|---|---|---|---|
| `validation_failed` | 400 | DTO or query validation failed, malformed JSON, wrong current password (change password / delete account) | `One or more fields are invalid.` |
| `bad_request` | 400 / 405 / 415 | Any other client error with no specific code | `The request could not be processed.` |
| `invalid_credentials` | 401 | Login failed: unknown email, wrong password, or locked-out account | `Email or password is incorrect.` |
| `unauthorized` | 401 | No session, or an invalid or expired one, on a protected endpoint | `Authentication is required.` |
| `csrf_failed` | 403 | Unsafe method without `X-CSRF-Protection: 1` | `Missing or invalid CSRF header.` |
| `not_found` | 404 | Task not found or not owned by the caller, unknown route, malformed GUID in the path | `The requested resource was not found.` |
| `email_taken` | 409 | Register with an existing email | `An account with this email already exists.` |
| `rate_limited` | 429 | Rate limit exceeded. `Retry-After` is set. | `Too many requests. Please try again later.` |
| `internal_error` | 500 | Unhandled exception, no details | `An unexpected error occurred.` |

---

## 4. Endpoint summary

| # | Method | Path | Auth | CSRF hdr | Rate limited | Success |
|---|---|---|---|---|---|---|
| 1 | POST | `/api/v1/auth/register` | No | Yes | Yes | 201 `UserDto` + Set-Cookie |
| 2 | POST | `/api/v1/auth/login` | No | Yes | Yes | 200 `UserDto` + Set-Cookie |
| 3 | POST | `/api/v1/auth/logout` | No (session optional) | Yes | No | 204 + expired cookie |
| 4 | GET | `/api/v1/auth/me` | Yes | No | No | 200 `UserDto` |
| 5 | PUT | `/api/v1/account/profile` | Yes | Yes | No | 200 `UserDto` |
| 6 | POST | `/api/v1/account/change-password` | Yes | Yes | Yes | 204 |
| 7 | POST | `/api/v1/account/delete` | Yes | Yes | Yes | 204 + expired cookie |
| 8 | GET | `/api/v1/tasks` | Yes | No | No | 200 `TaskDto[]` |
| 9 | GET | `/api/v1/tasks/{id}` | Yes | No | No | 200 `TaskDto` |
| 10 | POST | `/api/v1/tasks` | Yes | Yes | No | 201 `TaskDto` |
| 11 | PUT | `/api/v1/tasks/{id}` | Yes | Yes | No | 200 `TaskDto` |
| 12 | DELETE | `/api/v1/tasks/{id}` | Yes | Yes | No | 204 |
| 13 | GET | `/health` (backend only, not proxied) | No | No | No | 200 `text/plain` |

The per-endpoint error tables below leave out these errors, which can apply to any endpoint:
- `500 internal_error`, on every endpoint.
- `401 unauthorized`, on every endpoint marked **Auth: Yes**.
- `403 csrf_failed`, on every endpoint marked **CSRF hdr: Yes**.
- `429 rate_limited`, on every endpoint marked **Rate limited: Yes**.

**Password rules**, used in register and change password:
- Required, 8-128 chars.
- At least one letter `[A-Za-z]` and one digit `[0-9]`.
- Not trimmed.

---

## 5. Endpoints

### 5.1 Register
`POST /api/v1/auth/register` | **Auth: No**

**Request** `RegisterRequest`

| Field | Type | Rules |
|---|---|---|
| `displayName` | string | Required. 1-50 chars after trim. |
| `email` | string | Required. After trim: 1-254 chars, matches `^[^@\s]+@[^@\s]+\.[^@\s]+$`. Stored lowercased. Uniqueness ignores case. |
| `password` | string | Password rules |

**Success**: `201 Created` with a `UserDto` body. The user is logged in (`Set-Cookie: tm_session=...`).

**Errors**: `400 validation_failed` (keys `displayName`, `email`, `password`) and `409 email_taken`.

```http
POST /api/v1/auth/register
Content-Type: application/json
X-CSRF-Protection: 1

{ "displayName": " Ada Lovelace ", "email": "  Ada@Example.com ", "password": "correcthorse1" }
```
```http
HTTP/1.1 201 Created
Set-Cookie: tm_session=CfDJ8...; path=/; secure; samesite=lax; httponly
Content-Type: application/json

{
  "id": "8d0f5a52-2c1b-4f4e-9a57-0b6c2f1e9d31",
  "email": "ada@example.com",
  "displayName": "Ada Lovelace",
  "createdAt": "2026-10-10T14:30:00.1234567Z"
}
```

---

### 5.2 Login
`POST /api/v1/auth/login` | **Auth: No**

**Request** `LoginRequest`

| Field | Type | Rules |
|---|---|---|
| `email` | string | Required, not blank, max 254 chars. Trimmed and lowercased before lookup. |
| `password` | string | Required, not empty, max 128 chars. No complexity check at login. |

**Success**: `200 OK` with a `UserDto` body, plus `Set-Cookie`.

**Errors**:
- `400 validation_failed`.
- `401 invalid_credentials`, with an identical body for an unknown email, a wrong password, and a locked-out account (locked for 15 minutes after 5 failures).

```http
POST /api/v1/auth/login
Content-Type: application/json
X-CSRF-Protection: 1

{ "email": "ada@example.com", "password": "wrongpass1" }
```
```http
HTTP/1.1 401 Unauthorized
Content-Type: application/json

{ "status": 401, "code": "invalid_credentials", "message": "Email or password is incorrect.", "errors": null, "traceId": "00-9c1e...-01" }
```

---

### 5.3 Logout
`POST /api/v1/auth/logout` | **Auth: No** (works with or without a session)

**Request**: no body.

**Success**: `204 No Content`. The response expires the cookie.

**Errors**: only the common ones.

```http
POST /api/v1/auth/logout
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...
```
```http
HTTP/1.1 204 No Content
Set-Cookie: tm_session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; secure; samesite=lax; httponly
```

---

### 5.4 Current user
`GET /api/v1/auth/me` | **Auth: Yes**

**Success**: `200 OK` with a `UserDto` body.

**Errors**: `401 unauthorized`. This includes the case where the session's user has been deleted.

```http
GET /api/v1/auth/me
Cookie: tm_session=CfDJ8...
```
```json
{ "id": "8d0f5a52-2c1b-4f4e-9a57-0b6c2f1e9d31", "email": "ada@example.com", "displayName": "Ada Lovelace", "createdAt": "2026-10-10T14:30:00.1234567Z" }
```

---

### 5.5 Update profile
`PUT /api/v1/account/profile` | **Auth: Yes**

**Request** `UpdateProfileRequest`

| Field | Type | Rules |
|---|---|---|
| `displayName` | string | Required. 1-50 chars after trim. |

**Success**: `200 OK` with the updated `UserDto` body.

**Errors**: `400 validation_failed` (key `displayName`).

```http
PUT /api/v1/account/profile
Content-Type: application/json
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...

{ "displayName": "Ada L." }
```
```json
{ "id": "8d0f5a52-2c1b-4f4e-9a57-0b6c2f1e9d31", "email": "ada@example.com", "displayName": "Ada L.", "createdAt": "2026-10-10T14:30:00.1234567Z" }
```

---

### 5.6 Change password
`POST /api/v1/account/change-password` | **Auth: Yes**

**Request** `ChangePasswordRequest`

| Field | Type | Rules |
|---|---|---|
| `currentPassword` | string | Required, not empty, max 128 chars. Must be the correct current password. |
| `newPassword` | string | Password rules. Must differ from `currentPassword`. |

**Success**: `204 No Content`.
- The current session is refreshed and stays valid, and a new cookie may be issued.
- Other sessions end within `Auth__SecurityStampValidationSeconds`.

**Errors**: `400 validation_failed`.
- A wrong current password gives the key `currentPassword` with the message `"Current password is incorrect."`. It is **never** a 401.
- Rule violations give the key `newPassword`.

```http
POST /api/v1/account/change-password
Content-Type: application/json
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...

{ "currentPassword": "correcthorse1", "newPassword": "batterystaple2" }
```
```http
HTTP/1.1 204 No Content
```

---

### 5.7 Delete account
`POST /api/v1/account/delete` | **Auth: Yes**

This uses `POST` because a body on `DELETE` is not reliably forwarded.

**Request** `DeleteAccountRequest`

| Field | Type | Rules |
|---|---|---|
| `password` | string | Required, not empty, max 128 chars. Must be the correct password. |

**Success**: `204 No Content`.
- The user and **all their tasks** are permanently deleted (database cascade).
- The response expires the session cookie.

**Errors**: `400 validation_failed` (key `password`, message `"Password is incorrect."` when it's wrong).

```http
POST /api/v1/account/delete
Content-Type: application/json
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...

{ "password": "batterystaple2" }
```
```http
HTTP/1.1 204 No Content
Set-Cookie: tm_session=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; secure; samesite=lax; httponly
```

---

### 5.8 List tasks
`GET /api/v1/tasks` | **Auth: Yes**

Returns **all** of the caller's tasks that match the filters, as a plain array. There is no pagination.

**Query parameters** `TaskListQuery` (all optional)

| Param | Type | Default | Rules |
|---|---|---|---|
| `status` | `"todo" \| "inProgress" \| "done"` | (no filter) | Valid enum name. Numeric strings are rejected. |
| `priority` | `"low" \| "medium" \| "high"` | (no filter) | Same as `status` |
| `search` | string | (no filter) | Trimmed. Max 100 chars. Empty means no filter. Case-insensitive substring match (ASCII letters guaranteed) on `title` OR `description`. Every character matches literally, including `%`, `_`, `\`, and `[`; the server escapes them for `LIKE`, depending on the provider. |
| `sortBy` | `"createdAt" \| "dueDate" \| "priority" \| "title"` | `"createdAt"` | |
| `sortDir` | `"asc" \| "desc"` | `"desc"` | |

Sorting rules:
- `priority` order is low < medium < high.
- `dueDate`: tasks with a `null` due date come **last** in both directions. Other tasks are ordered by their UTC instant.
- `title`: case-insensitive (ASCII letters guaranteed).
- Tie-breakers: `createdAt desc`, then `id asc`. The `id` tie-break only guarantees a stable order; the order itself may differ between database providers.

Filters combine with AND.

**Success**: `200 OK` with a `TaskDto[]` body (`[]` when there are none).

**Errors**: `400 validation_failed` (key = the parameter name).

```http
GET /api/v1/tasks?status=todo&priority=high&search=report&sortBy=dueDate&sortDir=asc
Cookie: tm_session=CfDJ8...
```
```json
[
  {
    "id": "3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14",
    "title": "Write quarterly report",
    "description": "Include Q3 numbers.",
    "status": "todo",
    "priority": "high",
    "dueDate": "2026-10-15T09:00:00Z",
    "createdAt": "2026-10-10T14:35:12.441Z",
    "updatedAt": "2026-10-10T14:35:12.441Z"
  }
]
```

---

### 5.9 Get task
`GET /api/v1/tasks/{id}` | **Auth: Yes**

**Success**: `200 OK` with a `TaskDto` body (same shape as in 5.8).

**Errors**: `404 not_found`, when the task doesn't exist, **belongs to another user**, or `id` is not a valid GUID.

```http
GET /api/v1/tasks/3f2b8c1e-9a4d-4c7e-8f61-2d5a7b9c0e14
Cookie: tm_session=CfDJ8...
```

---

### 5.10 Create task
`POST /api/v1/tasks` | **Auth: Yes**

**Request** `CreateTaskRequest`

| Field | Type | Required | Default | Rules |
|---|---|---|---|---|
| `title` | string | yes | - | 1-200 chars after trim |
| `description` | string \| null | no | `null` | Max 2000 chars after trim. Blank is stored as `null`. |
| `status` | `TaskItemStatus` | no | `"todo"` | Valid enum string |
| `priority` | `TaskItemPriority` | no | `"medium"` | Valid enum string |
| `dueDate` | string (ISO 8601 with offset) \| null | no | `null` | Offset or `Z` required. After normalizing to UTC whole seconds, it must be **strictly later than the server's current UTC time**. |

Any `id`, `userId`, `createdAt`, or `updatedAt` in the body is ignored.

**Success**: `201 Created`, with `Location: /api/v1/tasks/{id}` (relative) and a `TaskDto` body (`createdAt == updatedAt`).

**Errors**: `400 validation_failed` (keys `title`, `description`, `status`, `priority`, `dueDate`, or `body`).

```http
POST /api/v1/tasks
Content-Type: application/json
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...

{ "title": "  Buy milk  ", "priority": "low", "dueDate": "2026-10-12T19:30:00+02:00" }
```
```http
HTTP/1.1 201 Created
Location: /api/v1/tasks/b7e4c2d9-1f3a-4e8b-a6c5-9d2f0e1b3a47
Content-Type: application/json

{
  "id": "b7e4c2d9-1f3a-4e8b-a6c5-9d2f0e1b3a47",
  "title": "Buy milk",
  "description": null,
  "status": "todo",
  "priority": "low",
  "dueDate": "2026-10-12T17:30:00Z",
  "createdAt": "2026-10-10T14:40:00Z",
  "updatedAt": "2026-10-10T14:40:00Z"
}
```

Past due date:
```json
{ "status": 400, "code": "validation_failed", "message": "One or more fields are invalid.", "errors": { "dueDate": ["Due date must be in the future."] }, "traceId": "00-1a2b...-01" }
```

---

### 5.11 Update task (full replace)
`PUT /api/v1/tasks/{id}` | **Auth: Yes**

This is the only way to change a task: a full replace with last-write-wins. The quick status change from the list also uses it. There is no PATCH, no status endpoint, and no concurrency token.

**Request** `UpdateTaskRequest`. **Every key must be present.** `description` and `dueDate` may be `null`.

| Field | Type | Required | Rules |
|---|---|---|---|
| `title` | string | yes | 1-200 chars after trim |
| `description` | string \| null | key required | Max 2000 chars after trim. Blank is stored as `null`. |
| `status` | `TaskItemStatus` | yes | Valid enum string |
| `priority` | `TaskItemPriority` | yes | Valid enum string |
| `dueDate` | string (ISO 8601 with offset) \| null | key required | Offset or `Z` required. **Future rule only when changing** (see below). |

**Due-date rule on update.** Let `newDue` be the request value normalized to UTC whole seconds, and `oldDue` the stored value.
- `newDue == null`: always allowed.
- `newDue == oldDue` (the same instant, whatever offset was sent): always allowed, even if it is in the past.
- Otherwise (the value is changing, including `oldDue == null`): `newDue` must be strictly later than now. If not, the request fails with `400 validation_failed`, key `dueDate`.

**Order of checks**:
1. CSRF.
2. Auth.
3. Request validation that doesn't need the task (400).
4. Ownership lookup (404).
5. The due-date rule (400).

The server sets `updatedAt` to now. `createdAt` doesn't change.

**Success**: `200 OK` with the updated `TaskDto` body.

**Errors**:
- `400 validation_failed` (a broken rule, a missing key, malformed JSON, or a past due date that is changing).
- `404 not_found`.

```http
PUT /api/v1/tasks/b7e4c2d9-1f3a-4e8b-a6c5-9d2f0e1b3a47
Content-Type: application/json
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...

{ "title": "Buy milk", "description": "2 liters, oat", "status": "done", "priority": "low", "dueDate": "2026-10-12T17:30:00Z" }
```
```json
{
  "id": "b7e4c2d9-1f3a-4e8b-a6c5-9d2f0e1b3a47",
  "title": "Buy milk",
  "description": "2 liters, oat",
  "status": "done",
  "priority": "low",
  "dueDate": "2026-10-12T17:30:00Z",
  "createdAt": "2026-10-10T14:40:00Z",
  "updatedAt": "2026-10-13T08:02:41.512Z"
}
```
This succeeds even though `2026-10-12T17:30:00Z` is now in the past, because `dueDate` is unchanged.

---

### 5.12 Delete task
`DELETE /api/v1/tasks/{id}` | **Auth: Yes**

This is a hard delete.

**Success**: `204 No Content`.

**Errors**: `404 not_found` (including a task that was already deleted).

```http
DELETE /api/v1/tasks/b7e4c2d9-1f3a-4e8b-a6c5-9d2f0e1b3a47
X-CSRF-Protection: 1
Cookie: tm_session=CfDJ8...
```
```http
HTTP/1.1 204 No Content
```

---

### 5.13 Health
`GET /health` | **Auth: No**

This endpoint is on the backend only. It is not under `/api` and not proxied by Vercel. The host's health probe and CI use it.

**Success**: `200 OK`, `text/plain`, body `Healthy` (ASP.NET Core health checks, including an EF Core database connectivity check for whichever provider is configured).

**Failure**: `503 Service Unavailable`, `text/plain`, body `Unhealthy`. This is the one documented exception to `ApiError`.

---

## 6. DTOs - C# (PascalCase) and JSON (camelCase)

All records are `sealed`. Nullable reference types are enabled. The enums are shared by entities and DTOs; they are not entities. `DateTimeOffset` appears **only** in request DTOs. It is converted to a UTC `DateTime` before it reaches EF (SPEC 6.0).

```csharp
// ---------- Enums (TaskManager.Api.Entities) ----------
public enum TaskItemStatus   { Todo, InProgress, Done }   // "todo" | "inProgress" | "done"
public enum TaskItemPriority { Low, Medium, High }        // "low" | "medium" | "high"

// ---------- Auth (TaskManager.Api.Dtos.Auth) ----------
public sealed record RegisterRequest(string DisplayName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(Guid Id, string Email, string DisplayName, DateTime CreatedAt);

// ---------- Account (TaskManager.Api.Dtos.Account) ----------
public sealed record UpdateProfileRequest(string DisplayName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record DeleteAccountRequest(string Password);

// ---------- Tasks (TaskManager.Api.Dtos.Tasks) ----------
public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    TaskItemPriority Priority,
    DateTime? DueDate,          // UTC, whole seconds, serialized with "Z"
    DateTime CreatedAt,         // UTC
    DateTime UpdatedAt);        // UTC

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    TaskItemStatus? Status,     // null/missing -> Todo
    TaskItemPriority? Priority, // null/missing -> Medium
    DateTimeOffset? DueDate);   // offset required (StrictDateTimeOffsetConverter); stored as UTC DateTime

public sealed record UpdateTaskRequest(
    string Title,
    [property: JsonRequired] string? Description,        // key required, value may be null
    [property: JsonRequired] TaskItemStatus Status,      // key required: a missing enum would silently default to todo
    [property: JsonRequired] TaskItemPriority Priority,  // key required: a missing enum would silently default to low
    [property: JsonRequired] DateTimeOffset? DueDate);   // key required, value may be null; stored as UTC DateTime

public enum TaskSortBy    { CreatedAt, DueDate, Priority, Title }  // "createdAt" | "dueDate" | "priority" | "title"
public enum SortDirection { Asc, Desc }                            // "asc" | "desc"

// Bound with [FromQuery]. Query keys are camelCase (binding is case-insensitive).
public sealed record TaskListQuery
{
    public TaskItemStatus?   Status   { get; init; }
    public TaskItemPriority? Priority { get; init; }
    public string?           Search   { get; init; }
    public TaskSortBy        SortBy   { get; init; } = TaskSortBy.CreatedAt;
    public SortDirection     SortDir  { get; init; } = SortDirection.Desc;
}

// ---------- Common (TaskManager.Api.Dtos.Common) ----------
public sealed record ApiError(
    int Status,
    string Code,                                   // one of ApiErrorCodes
    string Message,
    IReadOnlyDictionary<string, string[]>? Errors, // non-null only for validation_failed
    string TraceId);

public static class ApiErrorCodes
{
    public const string ValidationFailed   = "validation_failed";
    public const string BadRequest         = "bad_request";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Unauthorized       = "unauthorized";
    public const string CsrfFailed         = "csrf_failed";
    public const string NotFound           = "not_found";
    public const string EmailTaken         = "email_taken";
    public const string RateLimited        = "rate_limited";
    public const string InternalError      = "internal_error";
}
```

The list endpoint returns `IReadOnlyList<TaskDto>` (JSON array).

Required JSON setup (`Program.cs`):
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false))`
- `Converters.Add(new StrictDateTimeOffsetConverter())`: rejects ISO strings with no offset or `Z`.
- `DefaultIgnoreCondition = JsonIgnoreCondition.Never`, so nullable fields are always written.
- Every outgoing `DateTime` has `Kind=Utc`, so it serializes with `Z`. The EF converter marks values read from the database as UTC on every provider.
- Query-string enum binding rejects numeric values and undefined names with `400 validation_failed`.
- JSON deserialization errors (including a `JsonRequired` key that is missing) are mapped to `validation_failed`. The key is the camelCase property name, or `body`.

### Field-by-field mapping

| C# record | TS type | Fields (JSON name: TS type) |
|---|---|---|
| `RegisterRequest` | `RegisterRequest` | `displayName: string`, `email: string`, `password: string` |
| `LoginRequest` | `LoginRequest` | `email: string`, `password: string` |
| `UserDto` | `UserDto` | `id: string`, `email: string`, `displayName: string`, `createdAt: string` |
| `UpdateProfileRequest` | `UpdateProfileRequest` | `displayName: string` |
| `ChangePasswordRequest` | `ChangePasswordRequest` | `currentPassword: string`, `newPassword: string` |
| `DeleteAccountRequest` | `DeleteAccountRequest` | `password: string` |
| `TaskDto` | `TaskDto` | `id: string`, `title: string`, `description: string \| null`, `status: TaskItemStatus`, `priority: TaskItemPriority`, `dueDate: string \| null`, `createdAt: string`, `updatedAt: string` |
| `CreateTaskRequest` | `CreateTaskRequest` | `title: string`, `description?: string \| null`, `status?: TaskItemStatus`, `priority?: TaskItemPriority`, `dueDate?: string \| null` |
| `UpdateTaskRequest` | `UpdateTaskRequest` | `title: string`, `description: string \| null`, `status: TaskItemStatus`, `priority: TaskItemPriority`, `dueDate: string \| null` |
| `TaskListQuery` | `TaskListQuery` | `status?`, `priority?`, `search?`, `sortBy?`, `sortDir?` |
| `ApiError` | `ApiError` | `status: number`, `code: ApiErrorCode`, `message: string`, `errors: Record<string, string[]> \| null`, `traceId: string` |

The TypeScript definitions are in `frontend/types/api.ts`.
