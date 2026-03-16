# Employee Time Clock Management System
## App Plan & Design System

---

## 1. Executive Summary

An end-to-end employee time clock management system consisting of:
- **Windows Desktop App** — NFC-based clock in/out for employees
- **Next.js Web App** — Admin portal for employee & NFC management
- **.NET API Server** — Central backend with Microsoft SQL database
- **Offline-first desktop** with sync when network is available

---

## 2. System Architecture

```
┌─────────────────────────────────────── CLIENT LAYER ───────────────────────────────────────┐
│                                                                                             │
│  ┌──────────────────────────────┐                    ┌──────────────────────────────────┐   │
│  │  Windows Desktop App         │                    │  Next.js Admin Web App           │   │
│  │  (WPF / WinUI / Electron)    │                    │  • Employee CRUD                 │   │
│  │  • NFC card reader           │                    │  • NFC card association          │   │
│  │  • Local SQLite storage      │                    │  • Time reports & wages          │   │
│  │  • Offline queue & sync      │                    │  • Settings & configuration     │   │
│  └──────────────┬───────────────┘                    └──────────────┬───────────────────┘   │
│                 │                                                   │                        │
│                 │ HTTPS (when online)                               │ HTTPS                  │
│                 └───────────────────────┬───────────────────────────┘                        │
└─────────────────────────────────────────┼───────────────────────────────────────────────────┘
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                    API LAYER                                                │
│  ┌──────────────────────────────────────────────────────────────────────────────────────┐   │
│  │  .NET 8 Web API (ASP.NET Core)                                                        │   │
│  │  • RESTful endpoints                                                                  │   │
│  │  • JWT / API Key auth                                                                 │   │
│  │  • Serves both desktop app and admin web app                                          │   │
│  └─────────────────────────────────────────────┬────────────────────────────────────────┘   │
└────────────────────────────────────────────────┼────────────────────────────────────────────┘
                                                 │
                                                 ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                    DATA LAYER                                               │
│  ┌────────────────────────────┐     ┌────────────────────────────────────────────────────┐  │
│  │  Microsoft SQL Server      │     │  Desktop (local): SQLite                            │  │
│  │  • Employees               │     │  • Pending punch records (offline queue)            │  │
│  │  • NFC card mappings       │     │  • Cached employee/card data for offline lookup     │  │
│  │  • Time punches            │     │  • Sync queue                                       │  │
│  │  • Wage rates              │     │                                                    │  │
│  └────────────────────────────┘     └────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Application Plan

### 3.1 Windows Desktop Application (Employee Clock)

| Feature | Description | Priority |
|---------|-------------|----------|
| NFC card read | Detect card tap, read UID, look up employee | P0 |
| Clock in | Record punch-in with timestamp, employee ID | P0 |
| Clock out | Record punch-out with timestamp | P0 |
| Local storage | SQLite for punches & cached employee/card data | P0 |
| Offline mode | Queue punches when offline, sync when online | P0 |
| Sync service | Push pending punches to API, pull updates | P0 |
| Visual feedback | Success/error animations, sound cues | P1 |
| Settings | API URL, sync interval, device name | P1 |
| Auto-start / kiosk mode | Run on Windows startup, optional full-screen | P2 |

**Technology options:** WPF (.NET 8), WinUI 3, or Electron + Node NFC bindings

**NFC libraries:**  
- Windows: `nfc-sharp`, `pcsc-sharp` (PC/SC)  
- Or device-specific SDK (e.g. ACR122U SDK)

---

### 3.2 .NET API Server

| Endpoint / Area | Method | Description |
|-----------------|--------|-------------|
| **Auth** | | |
| `/api/auth/login` | POST | Admin login (JWT) |
| `/api/auth/desktop-token` | POST | Desktop app API key validation |
| **Employees** | | |
| `/api/employees` | GET, POST | List, create employees |
| `/api/employees/{id}` | GET, PUT, DELETE | Get, update, delete |
| `/api/employees/{id}/nfc-cards` | GET, POST, DELETE | Manage NFC card association |
| **Time Punches** | | |
| `/api/punches` | GET | List punches (filters: date, employee) |
| `/api/punches/sync` | POST | Batch receive punches from desktop |
| `/api/punches/export` | GET | Export for payroll (CSV, etc.) |
| **Wages** | | |
| `/api/employees/{id}/wages` | GET, POST | Wage rates per employee |
| **Desktop Sync** | | |
| `/api/sync/employees` | GET | Full employee + card list for desktop cache |
| `/api/sync/punches` | POST | Accept batch of punches from desktop |
| **Health** | | |
| `/health` | GET | Health check |

---

### 3.3 Next.js Admin Web Application

| Module | Features |
|--------|----------|
| **Dashboard** | Today’s punches, active employees, quick stats |
| **Employees** | List, add, edit, deactivate; search, filters |
| **NFC Cards** | Associate/unlink cards to employees; scan/test |
| **Time Punches** | View, filter, edit, delete; date range, employee |
| **Wages** | Set hourly/salary rates; effective dates |
| **Reports** | Hours worked, overtime, export for payroll |
| **Settings** | Company info, punch rules (rounding, grace period) |
| **Users** | Admin users, roles (future) |

---

### 3.4 Data Model (Core Entities)

```
Employees
├── Id (GUID)
├── FirstName
├── LastName
├── Email
├── EmployeeNumber (unique)
├── IsActive
├── CreatedAt, UpdatedAt
└── WageRates[] (one-to-many)

NfcCards
├── Id (GUID)
├── CardUid (string, unique) — from NFC reader
├── EmployeeId (FK)
├── IsActive
├── RegisteredAt
└── LastUsedAt

TimePunches
├── Id (GUID)
├── EmployeeId (FK)
├── PunchType (In / Out)
├── PunchedAt (DateTime, UTC)
├── Source (Desktop / Web)
├── DeviceId (desktop identifier)
├── SyncedAt (null if from API directly)
└── RawCardUid (for reconciliation)

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
| **Error** | `#DC2626` | Errors, invalid card, warnings |
| **Warning** | `#D97706` | Pending sync, attention needed |
| **Neutral 50** | `#F9FAFB` | Page backgrounds |
| **Neutral 100** | `#F3F4F6` | Card backgrounds |
| **Neutral 700** | `#374151` | Body text |
| **Neutral 900** | `#111827` | Headings |

**Desktop app accent:**  
- Success: `#059669` (clock in/out OK)  
- Error: `#DC2626` (unknown card, sync failed)

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

### 4.6 Desktop App UI

- **Full-screen / kiosk:** Large “Tap card to clock in/out” area
- **States:**
  - Idle: Neutral gray, icon + “Tap your NFC card”
  - Success: Green flash, checkmark, employee name + time
  - Error: Red flash, “Card not recognized”
  - Offline: Yellow banner “Working offline — will sync when connected”
- **Font:** Large, readable from a distance (e.g. 32px+ for status)

---

### 4.7 Icons

- **Library:** Lucide React (Next.js), Lucide or similar for desktop
- **Primary icons:** Clock, User, CreditCard (NFC), Check, X, AlertCircle, Wifi, WifiOff

---

## 5. User Flows

### 5.1 Employee: Clock In

1. Employee taps NFC card on reader
2. Desktop reads card UID
3. Desktop looks up UID in local cache
4. If found: create punch (type: In), show success + name + time
5. If not found: show “Card not recognized”
6. If offline: store punch in local queue; sync when online

### 5.2 Employee: Clock Out

Same as clock in, but punch type = Out. Optional: validate that last punch was In.

### 5.3 Admin: Associate NFC Card

1. Admin opens Employee detail in Next.js
2. Clicks “Add NFC card”
3. Desktop app or USB reader in admin PC: tap card
4. Card UID sent to API; API associates with employee
5. Admin sees card listed; desktop pulls updated cache on next sync

### 5.4 Admin: View & Export Time

1. Admin opens Time Punches or Reports
2. Filters by date range, employee
3. Views table; edits if needed (e.g. manual correction)
4. Exports CSV for payroll

---

## 6. Security Considerations

| Area | Approach |
|------|----------|
| API auth | JWT for admin; API key for desktop app |
| Desktop app | Store API key in secure storage (e.g. Windows Credential Manager) |
| NFC spoofing | Log raw UID; consider additional validation if high security |
| SQL injection | Parameterized queries / EF Core |
| HTTPS | Enforce everywhere |
| CORS | Restrict admin/API origins |

---

## 7. Technology Stack Summary

| Layer | Technology |
|-------|------------|
| Desktop | WPF / WinUI 3 / Electron, .NET 8 or Node, SQLite, PC/SC for NFC |
| API | ASP.NET Core 8, Entity Framework Core, SQL Server |
| Admin Web | Next.js 14+, React, Tailwind CSS |
| Database | Microsoft SQL Server |
| Auth | JWT, API keys |

---

## 8. Project Structure (Suggested)

```
EmployeeTimeClock/
├── src/
│   ├── TimeClock.Desktop/          # Windows app
│   ├── TimeClock.Api/              # .NET Web API
│   ├── TimeClock.Admin/            # Next.js admin
│   └── TimeClock.Shared/           # Shared DTOs, constants
├── docs/
│   └── APP_PLAN_AND_DESIGN_SYSTEM.md
└── README.md
```

---

## 9. Phased Rollout

| Phase | Scope |
|-------|-------|
| **Phase 1** | API + SQL schema, basic employee CRUD, NFC card association |
| **Phase 2** | Desktop app: NFC read, local storage, clock in/out, sync |
| **Phase 3** | Admin web: dashboard, punches, reports, export |
| **Phase 4** | Wages, rounding rules, overtime, polish |

---

*Document version: 1.0 — Employee Time Clock Management System*
