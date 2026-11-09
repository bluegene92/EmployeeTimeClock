import type {
  Employee,
  Location,
  PayPeriodSettings,
  RateType,
  ResolvedDayRate,
  WageRate,
} from "@/types/employee";
import type { PunchType, TimePunch } from "@/types/timePunch";
import { toWallClockIso } from "@/lib/biWeeklyUtils";
import { getAppEnvironment } from "@/lib/environment";

function readError(text: string, status: number, statusText: string) {
  const trimmed = text.trim();
  if (!trimmed) return `${status} ${statusText}`;
  try {
    const body = JSON.parse(trimmed) as { message?: string; title?: string } | string;
    if (typeof body === "string") return body;
    if (typeof body.message === "string") return body.message;
    if (typeof body.title === "string") return body.title;
  } catch {
    return trimmed;
  }
  return trimmed;
}

function toEnvironmentPath(path: string) {
  if (getAppEnvironment() !== "test" || !path.startsWith("/api/") || path.startsWith("/api/test/") || path.startsWith("/api/test?")) {
    return path;
  }
  return `/api/test${path.slice(4)}`;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api/backend${toEnvironmentPath(path)}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!res.ok) {
    const text = await res.text();
    throw new Error(readError(text, res.status, res.statusText));
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

export function getApiBaseUrl() {
  return "/api/backend";
}

export function listEmployees(isActive?: boolean, locationId?: string) {
  const params = new URLSearchParams();
  if (isActive !== undefined) params.set("isActive", String(isActive));
  if (locationId) params.set("locationId", locationId);
  const query = params.size ? `?${params.toString()}` : "";
  return request<Employee[]>(`/api/employees${query}`);
}

export function getEmployee(id: string) {
  return request<Employee>(`/api/employees/${id}`);
}

export function createEmployee(body: {
  firstName: string;
  lastName: string;
  email: string;
  employeeNumber: string;
  isActive: boolean;
  locationId?: string | null;
  rateType?: RateType;
  hourlyRate?: number;
  salaryAmount?: number;
}) {
  return request<Employee>("/api/employees", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateEmployee(
  id: string,
  body: {
    firstName: string;
    lastName: string;
    email: string;
    employeeNumber: string;
    isActive: boolean;
    locationId?: string | null;
  }
) {
  return request<void>(`/api/employees/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteEmployee(id: string) {
  return request<void>(`/api/employees/${id}`, { method: "DELETE" });
}

export function setEmployeePin(id: string, pin: string) {
  return request<void>(`/api/employees/${id}/pin`, {
    method: "PUT",
    body: JSON.stringify({ pin }),
  });
}

export function clearEmployeePin(id: string) {
  return request<void>(`/api/employees/${id}/pin`, { method: "DELETE" });
}

export function setEmployeeWage(
  id: string,
  body: { rateType: RateType; hourlyRate?: number; salaryAmount?: number }
) {
  return request<WageRate>(`/api/employees/${id}/wages`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function listWageHistory(id: string) {
  return request<WageRate[]>(`/api/employees/${id}/wages/history`);
}

export function listLocations(isActive?: boolean) {
  const query = isActive === undefined ? "" : `?isActive=${isActive}`;
  return request<Location[]>(`/api/locations${query}`);
}

export function createLocation(body: { name: string; isActive: boolean }) {
  return request<Location>("/api/locations", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateLocation(id: string, body: { name: string; isActive: boolean }) {
  return request<void>(`/api/locations/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteLocation(id: string) {
  return request<void>(`/api/locations/${id}`, { method: "DELETE" });
}

export function listTimePunches(from: Date, toExclusive: Date, employeeId?: string) {
  const params = new URLSearchParams({
    from: toWallClockIso(from),
    to: toWallClockIso(toExclusive),
  });
  if (employeeId) params.set("employeeId", employeeId);
  return request<TimePunch[]>(`/api/timepunches?${params.toString()}`);
}

export function createTimePunch(body: {
  employeeId: string;
  punchType: PunchType;
  punchedAt: string;
  locationId?: string | null;
  source?: string;
}) {
  return request<TimePunch>("/api/timepunches", {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateTimePunch(
  id: string,
  body: { punchType: PunchType; punchedAt: string; locationId?: string | null }
) {
  return request<void>(`/api/timepunches/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteTimePunch(id: string) {
  return request<void>(`/api/timepunches/${id}`, { method: "DELETE" });
}

export function listResolvedDayRates(from: string, to: string) {
  const params = new URLSearchParams({ from, to });
  return request<ResolvedDayRate[]>(`/api/day-rates?${params.toString()}`);
}

export function setDayRate(body: { employeeId: string; workDate: string; hourlyRate: number }) {
  return request<void>("/api/day-rates", {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function getPayPeriodSettings() {
  return request<PayPeriodSettings>("/api/pay-periods/settings");
}

export function updatePayPeriodSettings(anchorDate: string) {
  return request<PayPeriodSettings>("/api/pay-periods/settings", {
    method: "PUT",
    body: JSON.stringify({ anchorDate }),
  });
}

export function verifyPayPeriod(employeeId: string, periodStart: string) {
  return request<void>("/api/pay-periods/verify", {
    method: "POST",
    body: JSON.stringify({ employeeId, periodStart }),
  });
}

export function unverifyPayPeriod(employeeId: string, periodStart: string) {
  return request<void>("/api/pay-periods/unverify", {
    method: "POST",
    body: JSON.stringify({ employeeId, periodStart }),
  });
}
