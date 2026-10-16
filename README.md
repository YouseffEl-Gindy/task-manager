# Task Manager

A small full-stack task manager. You register with an email and a display name, log in, and manage your own tasks: title, description, status (todo / in progress / done), priority (low / medium / high) and a due date with a time. You can filter, search and sort your tasks, and change your password, rename yourself or delete your account. Nobody sees anyone else's tasks.

| Part | Tech | Hosted on |
|---|---|---|
| Frontend (`frontend/`) | Next.js 16 (App Router), React, TypeScript (strict), Tailwind | Vercel |
| Backend (`backend/`) | ASP.NET Core Web API on .NET 10, EF Core, ASP.NET Core Identity (cookie sessions) | Azure App Service (Windows, Free F1) |
| Database | SQLite locally by default, SQL Server in production | Azure SQL Database (free offer) |

```
Browser --> Next.js on Vercel --(rewrites /api/*)--> ASP.NET Core API on Azure App Service --> Azure SQL
```

The browser only ever calls the Vercel site. Next.js forwards `/api/*` to the backend, so the login cookie belongs to the Vercel site and no CORS is needed.

More detail: [`docs/SPEC.md`](docs/SPEC.md) (spec, decisions log) and [`docs/API.md`](docs/API.md) (API contract).

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 24](https://nodejs.org/) (comes with npm)
- Git

No Docker and no local SQL Server are needed.

---

## Run it locally

Use two terminals, both starting in the repository root.

**1. Backend** (http://localhost:5000)

```powershell
cd backend/src/TaskManager.Api
dotnet run
```

By default it uses SQLite: it creates `taskmanager.db` in that folder (git-ignored) on first start. Check it's up at http://localhost:5000/health. Swagger UI (Development only) is at http://localhost:5000/swagger.

**2. Frontend** (http://localhost:3000)

```powershell
cd frontend
npm install
copy .env.example .env.local     # first time only; it sets BACKEND_URL=http://localhost:5000
npm run dev
```

Open http://localhost:3000. `.env.local` is git-ignored. `BACKEND_URL` is required: `next dev` and `next build` stop with an error without it.

### Local database

**SQLite (default).** Nothing to set up. The file is throwaway.

> **After any data model change** (new or changed entity or column): stop the backend, delete `taskmanager.db`, `taskmanager.db-shm` and `taskmanager.db-wal` in `backend/src/TaskManager.Api`, and start it again. SQLite uses `EnsureCreated`, which never updates an existing file. An old file gives errors like "no such column".
>
> ```powershell
> Remove-Item backend/src/TaskManager.Api/taskmanager.db*
> ```

**Azure SQL (optional).** The backend can use the Azure SQL database instead, through .NET *user secrets*. They're stored outside the repo, in `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, and only loaded when `ASPNETCORE_ENVIRONMENT=Development`. Replace the `<...>` placeholders; never put the real values in any file in this repo:

```powershell
dotnet user-secrets set "Database:Provider" "SqlServer" --project backend/src/TaskManager.Api
dotnet user-secrets set "Database:Schema" "Migrate" --project backend/src/TaskManager.Api
dotnet user-secrets set "ConnectionStrings:Default" "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;User ID=<app-user>;Password=<password>;Encrypt=True;Connect Timeout=60;" --project backend/src/TaskManager.Api
dotnet user-secrets list --project backend/src/TaskManager.Api   # check (prints the values, so not on a shared screen)
```

> **Important:** there is **one** Azure SQL database, used by **both production and local runs** (a deliberate choice for now). With user secrets set, your local backend reads and writes **production data**. Empty the tables before real users sign up.

Back to SQLite (this deletes **all** user secrets of the project, including the connection string):

```powershell
dotnet user-secrets clear --project backend/src/TaskManager.Api
```

Tests never use these settings. They run in the `Testing` environment (integration tests) or with explicit environment variables (Playwright), so they always use SQLite.

### Adding a database migration

Migrations target **SQL Server only** (there are never SQLite migrations). `dotnet-ef` is a *local* tool pinned in `dotnet-tools.json`. From the repo root:

```powershell
dotnet tool restore     # once per clone
dotnet ef migrations add <MigrationName> --project backend/src/TaskManager.Api --startup-project backend/src/TaskManager.Api --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes --project backend/src/TaskManager.Api --startup-project backend/src/TaskManager.Api
```

This needs no database: the tools use `AppDbContextDesignTimeFactory`. Production applies new migrations automatically when the app starts (`Database__Schema=Migrate`). If you use SQLite locally, delete `taskmanager.db*` afterwards (see above).

---

## Tests

| What | Command (from the repo root unless noted) |
|---|---|
| All backend tests | `dotnet test backend/TaskManager.sln` |
| Backend unit tests | `dotnet test backend/tests/TaskManager.Api.UnitTests` |
| Backend integration tests (SQLite in-memory) | `dotnet test backend/tests/TaskManager.Api.IntegrationTests` |
| Frontend lint | `cd frontend; npm run lint` |
| Frontend typecheck | `cd frontend; npm run typecheck` (on a fresh clone, run `npx next typegen` or `npm run dev` once first) |
| Frontend unit tests (Vitest) | `cd frontend; npm test` |
| Frontend production build | `cd frontend; npm run build` |
| End-to-end (Playwright, Chromium) | `cd frontend; npx playwright install chromium` (once), then `npm run test:e2e` |

`npm run test:e2e` starts its own backend on :5100 (with a fresh throwaway SQLite file in your temp folder) and its own production build of the frontend on :3100. Your dev servers on :5000 / :3000 can keep running. After an E2E run, run `npm run build` again before `npm start`, because the E2E build points at :5100.

The integration tests can also run against a real SQL Server (`TEST_DATABASE_PROVIDER=SqlServer` plus `TEST_DATABASE_CONNECTION`). CI does this with a throwaway container. **Never point that at Azure**: it creates a new database per test class, and those are billed.

---

## CI (`.github/workflows/ci.yml`)

Runs on every push to `main`, every pull request into `main`, and manually (Actions tab, "Run workflow"). All jobs run on `ubuntu-latest`, in parallel:

| Job | What it does |
|---|---|
| `backend` | Restore, build (Release; warnings shown in the log), unit tests, integration tests on SQLite in-memory |
| `migrations-check` | `dotnet tool restore`, then `dotnet ef migrations has-pending-model-changes` (fails if the model changed without a migration) |
| `backend-sqlserver` | Starts a throwaway SQL Server 2022 container with a **random SA password generated for that run** (masked in the logs, never stored), waits until it accepts logins, then runs the integration suite against it with the real migrations applied (AC-DB-2) |
| `frontend` | `npm ci`, lint, generate Next.js route types, typecheck, Vitest, `next build` |
| `e2e` | .NET 10 + Node 24, `npm ci`, installs Chromium, runs Playwright (1 worker on CI). On failure, the HTML report and traces are uploaded as the `playwright-report` artifact |

## Deploy (`.github/workflows/deploy-api.yml`)

Deploys **the backend only**. Vercel deploys the frontend by itself on every push.

1. Runs automatically when the **CI workflow succeeds for a push to `main`**, and deploys exactly the commit CI tested. Pull requests never deploy. It can also be started manually from `main` (Actions tab, "Deploy API", "Run workflow"); that skips CI, so use it only to redeploy something that already passed.
2. On a Windows runner: `dotnet publish -c Release -p:UseAppHost=false`. This is framework-dependent and portable (AnyCPU), so it runs on the App Service's own .NET 10 runtime in the **32-bit** worker of the Free plan. `web.config` starts it with `dotnet .\TaskManager.Api.dll`. `appsettings.Development.json` is removed from the package.
3. Deploys with `azure/webapps-deploy` using the GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE`.
4. Polls `https://<app>/health` for up to about 10 minutes until it returns 200, and fails the run if it never does. The first start after a deploy can be slow: F1 cold start, the free database waking up from auto-pause, and migrations running.

**Fallback (not used today):** if the Windows plan ever stops offering the .NET 10 runtime, publish self-contained for the 32-bit worker instead: `dotnet publish backend/src/TaskManager.Api/TaskManager.Api.csproj -c Release -r win-x86 --self-contained true -o <folder>`.

---

## Azure setup (as it is today)

| Item | Value |
|---|---|
| App Service plan | **Windows**, **Free F1**, region **Italy North**. Free Windows plans run a **32-bit** worker only, have no Always On (cold starts) and have a daily CPU quota |
| Web app | Runtime stack **.NET 10**. URL: `https://youseff-task-manager-api-h3gdfedxckekc3ab.italynorth-01.azurewebsites.net` |
| HTTPS Only | On |
| App settings | `Database__Provider=SqlServer`, `Database__Schema=Migrate`, `Auth__CookieSecure=Always`, `RateLimiting__UseForwardedFor=true`, `ASPNETCORE_ENVIRONMENT=Production` |
| Connection string | `Default`, type `SQLAzure` (value only in Azure, never in this repo) |
| Database | Azure SQL Database, **free offer** (serverless, auto-pauses when idle; set to pause instead of bill when the free limit is reached). The app logs in as a contained database user with read/write/DDL rights, not the server admin |
| SQL firewall | "Allow Azure services and resources to access this server" is on |

The production schema is created and updated by the app itself at startup (`Database__Schema=Migrate`). GitHub runners never connect to the database.

## Remaining one-time steps

Do these once, after the code is on GitHub:

1. **Enable publish credentials.** Azure portal, open the web app, then **Settings > Configuration > General settings**. Turn **SCM Basic Auth Publishing Credentials** **On** and click **Save**.
2. **Download the publish profile.** Open the web app's **Overview** and click **Download publish profile**. The file (`*.PublishSettings`) contains a deploy password: never commit it. `.gitignore` blocks it, but keep it out of the repo folder anyway.
3. **Store it in GitHub.** Open the repository, then **Settings > Secrets and variables > Actions > New repository secret**. Name: `AZURE_WEBAPP_PUBLISH_PROFILE`. Value: the **entire contents** of the downloaded file (open it in Notepad, select all, copy).
4. **Delete the downloaded file** (and empty the Recycle Bin). If it ever leaks, use **Reset publish profile** on the web app's Overview, then repeat steps 2-4.
5. **Check the app name.** In `.github/workflows/deploy-api.yml`, `AZURE_WEBAPP_NAME` must be the web app's **name** as shown on its Overview page (expected: `youseff-task-manager-api`).
6. **First deploy.** Push to `main` (or run "Deploy API" manually). Wait for the health check step to pass, then open `https://<app>/health`. It should say `Healthy`.
7. **Vercel.**
   1. On vercel.com: **Add New > Project**, and import this GitHub repository.
   2. Set **Root Directory** to `frontend`. The framework preset should be detected as Next.js.
   3. Under **Environment Variables**, add `BACKEND_URL` = `https://youseff-task-manager-api-h3gdfedxckekc3ab.italynorth-01.azurewebsites.net` (no trailing slash), for **Production** and **Preview**.
   4. Click **Deploy**. `BACKEND_URL` is read at build time: if you ever change it, **redeploy**.
   5. Optional: under Settings > Build and Deployment, set Node.js to 24.x to match CI.
8. **Smoke test.** Open the Vercel URL, register, create a task, log out and log in again.

### Secrets checklist

| Secret | Where it lives | Never in |
|---|---|---|
| Azure SQL connection string (app user password) | App Service **Connection strings** (`Default`); locally only in .NET user secrets | the repo, `.env` files, this README |
| SQL server admin password | Your password manager | anywhere else |
| Publish profile | GitHub secret `AZURE_WEBAPP_PUBLISH_PROFILE` | the repo, your Downloads folder (after step 4) |
| `BACKEND_URL` | Vercel environment variable (not a secret, just config) | n/a |

## Good to know

- **Don't point an uptime pinger or keep-alive at `/health`.** It would keep the free database awake and use up its monthly allowance. Accept the slow first request instead.
- The **first request after the app has been idle can take a minute or two**: the F1 app has to start, and the database may have to resume. The frontend then shows "server is starting up, try again". That's expected.
- If F1's daily CPU quota runs out, the API stops until the next day.
- `/health` is on the backend only (not proxied through Vercel).
- Logs: Azure portal, web app, **Log stream** (or **Diagnose and solve problems**). A failing migration at startup shows up there.
