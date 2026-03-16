# Railway Deployment Guide

## Prerequisites

- Railway account (railway.app)
- GitHub repo connected to Railway (optional, for auto-deploy)

## 1. Create a PostgreSQL Database on Railway

1. Create a new project on Railway
2. Click **Add Service** → **Database** → **PostgreSQL**
3. Railway creates a PostgreSQL instance and sets `DATABASE_URL` automatically when linked

## 2. Deploy the .NET API

1. Click **Add Service** → **GitHub Repo** (or deploy from CLI)
2. Select this repository
3. Set the **Root Directory** to the project root (where `railway.toml` is)
4. Railway auto-detects .NET and uses Nixpacks to build

## 3. Link the Database

1. In your API service, go to **Variables**
2. Click **Add Variable** → **Add Reference**
3. Select the PostgreSQL service's `DATABASE_URL`
4. Railway injects it at runtime (no manual copy needed)

## 4. Run the Database Schema

After the first deploy, run the PostgreSQL schema against your Railway database:

1. Get the `DATABASE_URL` from your PostgreSQL service (Variables tab)
2. Locally, run:
   ```bash
   psql "YOUR_DATABASE_URL" -f database/001_CreateSchema_PostgreSQL.sql
   ```
   Or use a GUI (pgAdmin, DBeaver) and execute `database/001_CreateSchema_PostgreSQL.sql`

## 5. Local Development

For local development with PostgreSQL:

1. Install PostgreSQL and create a database named `TimeClock`
2. Update `appsettings.Development.json`:
   ```json
   "DefaultConnection": "Host=localhost;Database=TimeClock;Username=postgres;Password=YOUR_PASSWORD"
   ```
3. Run the schema: `psql -U postgres -d TimeClock -f database/001_CreateSchema_PostgreSQL.sql`
4. Run the API: `dotnet run --project src/TimeClock.Api`

## Environment Variables

| Variable       | Required | Description                                      |
|----------------|----------|--------------------------------------------------|
| `DATABASE_URL` | Yes      | PostgreSQL connection string (Railway sets this) |
| `PORT`         | Auto     | Set by Railway for the web process               |
