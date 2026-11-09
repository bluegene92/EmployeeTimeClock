# Render Deployment Guide

This project deploys the **.NET 9 API** and **PostgreSQL** to [Render](https://render.com). The Next.js admin app runs on your desktop and talks to the hosted API.

Render has no native .NET runtime, so the API is built from the repo `Dockerfile`. See [Render language support](https://render.com/docs/language-support) and [Docker on Render](https://render.com/docs/docker).

## Architecture

- **Web service** `timeclock-api` — ASP.NET Core API (Docker)
- **Postgres** `timeclock-db` — same region as the API
- **Admin UI** — `src/timeclock-admin` on `http://localhost:3000`

The API reads `DATABASE_URL` (internal Render connection string) and `CORS_ORIGINS` (`http://localhost:3000`). Schema in `database/001_CreateSchema_PostgreSQL.sql` is applied automatically on API startup.

## Prerequisites

- A [Render](https://dashboard.render.com) account
- This repo on GitHub (for example `bluegene92/EmployeeTimeClock`)
- Node.js 20+ on your desktop for the admin app

Render Postgres is a paid instance (the Blueprint uses the smallest plan, `0.1c-256mb`). Put the API and database in the **same region**.

## 1. Deploy with a Blueprint (recommended)

1. Push the latest code to GitHub.
2. Open [Render Dashboard](https://dashboard.render.com) → **New** → **Blueprint**.
3. Connect the GitHub repo that contains `render.yaml`.
4. Apply the Blueprint. Render creates:
   - Postgres: `timeclock-db`
   - Web service: `timeclock-api` (Docker), health check `/api/health`
5. Wait for the first deploy to finish. The API applies the schema on startup.
6. Open the web service and copy its public URL, for example `https://timeclock-api.onrender.com`.

Blueprint env vars (already in `render.yaml`):

| Variable | Value |
|----------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `DATABASE_URL` | Internal URL from `timeclock-db` |
| `CORS_ORIGINS` | `http://localhost:3000` |

## 2. Deploy by hand (alternative)

1. **New** → **Postgres**. Name it `timeclock-db`. Use the same region you will use for the API.
2. **New** → **Web Service** → connect this GitHub repo.
3. Set **Language** to **Docker**. Dockerfile path: `./Dockerfile`.
4. Health check path: `/api/health`.
5. Environment variables:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `DATABASE_URL` = the database **Internal Database URL**
   - `CORS_ORIGINS` = `http://localhost:3000`
6. Deploy. Schema is applied on first boot.

Do **not** use `dotnet run` as the start command. Render does not ship a .NET SDK in native runtimes.

## 3. Run the admin app locally

```powershell
cd src/timeclock-admin
copy .env.local.example .env.local
```

Edit `.env.local`:

```
NEXT_PUBLIC_API_URL=https://YOUR-SERVICE.onrender.com
```

No trailing slash. Then:

```powershell
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). Log in with `admin` / `admin123` (local placeholder auth). Create an employee, set a PIN and wage, then confirm punches on Time Clock.

If the API is on a free/hobby web plan, it spins down when idle. The first request after idle can take 30–60 seconds.

## 4. Optional: SQL from your desktop

Use the database **External Database URL** in the Render dashboard (not the internal URL):

```powershell
psql "YOUR_EXTERNAL_DATABASE_URL" -f database/001_CreateSchema_PostgreSQL.sql
```

The API already applies this script on startup (`CREATE TABLE IF NOT EXISTS`), so this is only needed for one-off queries.

## Local API development (optional)

1. Install PostgreSQL locally and create a database named `TimeClock`.
2. `src/TimeClock.Api/appsettings.Development.json` already has:
   ```json
     "DefaultConnection": "Host=localhost;Database=TimeClock;Username=postgres;Password=postgres"
   ```
3. Run:
   ```powershell
     dotnet run --project src/TimeClock.Api
   ```

     The API listens on `http://localhost:5229` and applies the schema on startup.
4. Point `.env.local` at `http://localhost:5229` instead of Render.

## Environment variables

| Variable | Required | Description |
|----------|----------|-------------|
| `DATABASE_URL` | Yes on Render | Postgres URL. URI form is converted for Npgsql; SSL is required off localhost. |
| `PORT` | Auto | Set by Render. The API binds `http://0.0.0.0:$PORT`. |
| `CORS_ORIGINS` | Yes for local admin | Comma-separated origins. Default in Blueprint: `http://localhost:3000`. |
| `NEXT_PUBLIC_API_URL` | Yes for admin | Public API URL used by the local Next.js app. |
