# Employee Time Clock Management System
## App Plan & Design System

---

## 1. Executive Summary

An employee time clock management system consisting of:
- **Next.js Web App** — Admin portal for employees, PIN clock IDs, wages, and time punches (runs locally)
- **.NET API Server** — Central backend on Render
- **PostgreSQL** — Managed database on Render

A Windows desktop clock-in app is owned by another team and is out of this repository. This API keeps PIN verify and employee-sync endpoints for that team.

---

## 2. System Architecture

```
┌─────────────────────────────────────── CLIENT LAYER ───────────────────────────────────────┐
│                                                                                             │
│  ┌──────────────────────────────────┐                                                       │
│  │  Next.js Admin Web App (local)   │                                                       │
│  │  • Employee CRUD                 │                                                       │
│  │  • 4-digit PIN association       │                                                       │
│  │  • Time reports & wages          │                                                       │
│  │  • Settings & configuration      │                                                       │
│  └─────────────────┬────────────────┘                                                       │
│                    │ HTTPS (CORS: localhost)                                                │
└────────────────────┼────────────────────────────────────────────────────────────────────────┘
                     ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                    API LAYER (Render)                                       │
│  ┌──────────────────────────────────────────────────────────────────────────────────────┐   │
│  │  .NET 9 Web API (ASP.NET Core, Docker)                                                │   │
│  │  • RESTful endpoints                                                                  │   │
│  │  • PIN verify / employee sync for the desktop clock team                              │   │
│  └─────────────────────────────────────────────┬────────────────────────────────────────┘   │
└────────────────────────────────────────────────┼────────────────────────────────────────────┘
                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                    DATA LAYER (Render)                                      │
│  ┌──────────────────────────────────────────────────────────────────────────────────────┐   │
│  │  PostgreSQL                                                                           │   │
│  │  • Employees (including PIN hash)                                                     │   │
│  │  • Time punches                                                                       │   │
│  │  • Wage rates                                                                         │   │
│  └──────────────────────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Application Plan

### 3.1 .NET API Server (Render)

| Endpoint / Area | Method | Description |
|-----------------|--------|-------------|
| **Auth** | | |
| `/api/auth/verify-pin` | POST | Verify 4-digit PIN (desktop clock team) |
| **Employees** | | |
| `/api/employees` | GET, POST | List, create employees |
| `/api/employees/{id}` | GET, PUT, DELETE | Get, update, delete |
| `/api/employees/{id}/pin` | PUT, DELETE | Set or clear 4-digit PIN |
| `/api/employees/{id}/wages` | GET, PUT | Current wage rate |
| **Time Punches** | | |
| `/api/timepunches` | GET, POST | List (date/employee filters), create |
| `/api/timepunches/{id}` | PUT, DELETE | Update, delete |
| `/api/timepunches/sync` | POST | Batch receive punches (desktop team) |
| **Desktop Sync** | | |
| `/api/sync/employees` | GET | Active employees + HasPinSet for desktop cache |
| **Health** | | |
| `/api/health` | GET | Health check |

### 3.2 Next.js Admin Web Application (local)

| Module | Features |
|--------|----------|
| **Employees** | List, add, edit, deactivate; search, filters |
| **PIN** | Set or clear a 4-digit clock PIN (hash stored; PIN is not shown after save) |
| **Time Punches** | View, filter, edit, delete; date range, employee |
| **Wages** | Set hourly rates |
| **Reports** | Hours worked, overtime, export for payroll (placeholder) |
| **Settings** | Company info, punch rules (placeholder) |

### 3.3 Data Model (Core Entities)

```
Employees
├── Id (GUID)
├── FirstName
├── LastName
├── Email
├── EmployeeNumber (unique)
├── IsActive
├── PinHash (bcrypt; never returned to clients)
├── CreatedAt, UpdatedAt
└── WageRates[] (one-to-many)

TimePunches
├── Id (GUID)
├── EmployeeId (FK)
├── PunchType (In / Out)
├── PunchedAt (DateTime, UTC)
├── Source (Web / Desktop)
└── DeviceId (desktop identifier)

WageRates
├── Id (GUID)
├── EmployeeId (FK)
├── HourlyRate / SalaryAmount
├── EffectiveFrom, EffectiveTo
└── RateType (Hourly, Salary)
```

---

## 4. Design System

### 4.1 Brand & Tone

- **Professional** — Trustworthy for HR and payroll
- **Clear** — Minimal jargon, easy to understand
- **Efficient** — Admin tasks should be fast and predictable

---

### 4.2 Color Palette

| Role | Hex | Usage |
|------|-----|--------|
| **Primary** | `#2563EB` | CTAs, links, active states |
| **Primary Hover** | `#1D4ED8` | Button hover |
| **Success** | `#059669` | Clock in/out success, confirmations |
| **Error** | `#DC2626` | Errors, invalid PIN, warnings |
| **Warning** | `#D97706` | Attention needed |
| **Neutral 50** | `#F9FAFB` | Page backgrounds |
| **Neutral 100** | `#F3F4F6` | Card backgrounds |
| **Neutral 700** | `#374151` | Body text |
| **Neutral 900** | `#111827` | Headings |

---

### 4.3 Typography

| Element | Font | Weight | Size |
|---------|------|--------|------|
| **Headings (H1)** | Inter, system-ui | 700 | 24px |
| **Headings (H2)** | Inter, system-ui | 600 | 20px |
| **Headings (H3)** | Inter, system-ui | 600 | 16px |
| **Body** | Inter, system-ui | 400 | 14px |
| **Small / Caption** | Inter, system-ui | 400 | 12px |
| **Monospace** | JetBrains Mono, monospace | 400 | 14px |

---

### 4.4 Spacing Scale

```
4px   — xs
8px   — sm
12px  — md
16px  — lg
24px  — xl
32px  — 2xl
48px  — 3xl
64px  — 4xl
```

---

### 4.5 Component Patterns

#### Buttons
- **Primary:** Solid background `#2563EB`, white text, 8px border radius
- **Secondary:** Light gray background, dark text
- **Danger:** Red for delete actions
- Height: 40px; padding: 0 16px

#### Cards
- Background: white or `#F9FAFB`
- Border: 1px solid `#E5E7EB`
- Border radius: 8px
- Shadow: `0 1px 3px rgba(0,0,0,0.06)`

#### Forms
- Label: 12px, weight 500, color `#374151`
- Input height: 40px
- Error text: 12px, `#DC2626`

#### Tables (Admin)
- Header: 12px, weight 600, background `#F9FAFB`
- Row hover: `#F3F4F6`
- Alternating rows optional for long lists

---

### 4.6 Icons

- **Library:** Lucide React (Next.js)
- **Primary icons:** Clock, User, Check, X, AlertCircle

---

## 5. User Flows

### 5.1 Admin: Associate PIN

1. Admin opens Employee detail in Next.js
2. Clicks “Set PIN”
3. Enters a 4-digit PIN; API stores only the hash
4. Admin sees “PIN set” (the PIN is never shown again)

### 5.2 Admin: View & Edit Time

1. Admin opens Time Clock
2. Filters by bi-weekly period
3. Clicks a day cell to add, edit, or delete punches
4. Hours and wages update from punch pairs and hourly rate

---

## 6. Security Considerations

| Area | Approach |
|------|----------|
| Admin login | Local session placeholder (JWT planned) |
| PIN | Bcrypt hash only; never returned by the API |
| SQL injection | Parameterized queries / Dapper |
| HTTPS | Render terminates TLS at the edge |
| CORS | Allow the local admin origin (`http://localhost:3000`) |

---

## 7. Technology Stack Summary

| Layer | Technology |
|-------|------------|
| API | ASP.NET Core 9, Dapper, Npgsql, Docker on Render |
| Admin Web | Next.js 16, React, Tailwind CSS (runs locally) |
| Database | PostgreSQL on Render |
| Deploy | Render Blueprint (`render.yaml`) |

---

## 8. Project Structure

```
EmployeeTimeClock/
├── src/
│   ├── TimeClock.Api/              # .NET Web API
│   └── timeclock-admin/            # Next.js admin (local)
├── database/
│   └── 001_CreateSchema_PostgreSQL.sql
├── Dockerfile
├── render.yaml
├── DEPLOYMENT.md
└── APP_PLAN_AND_DESIGN_SYSTEM.md
```

---

## 9. Phased Rollout

| Phase | Scope |
|-------|-------|
| **Phase 1** | API + Postgres schema, employee CRUD, PIN, wages, punch CRUD |
| **Phase 2** | Admin web wired to hosted API |
| **Phase 3** | Reports, export, punch rules |
| **Phase 4** | Admin JWT auth |

---

*Document version: 1.1 — Employee Time Clock Management System (Render)*
