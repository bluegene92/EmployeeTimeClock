import type { TimePunch } from "@/types/timePunch";
import { createMockTimePunches } from "./mockTimePunches";

let punches: TimePunch[] = createMockTimePunches();

export function getTimePunches(): TimePunch[] {
  return punches;
}

export function getTimePunchesByDateRange(from: Date, to: Date): TimePunch[] {
  const fromTime = from.getTime();
  const toTime = to.getTime();
  return punches.filter((p) => {
    const t = new Date(p.punchedAt).getTime();
    return t >= fromTime && t < toTime;
  });
}

export function getTimePunchesForEmployeeAndDateRange(
  employeeId: string,
  from: Date,
  to: Date
): TimePunch[] {
  return getTimePunchesByDateRange(from, to).filter((p) => p.employeeId === employeeId);
}

export function updateTimePunch(id: string, updates: Partial<Pick<TimePunch, "punchedAt" | "punchType">>): TimePunch | undefined {
  const idx = punches.findIndex((p) => p.id === id);
  if (idx < 0) return undefined;
  const updated = { ...punches[idx], ...updates };
  punches = [
    ...punches.slice(0, idx),
    updated,
    ...punches.slice(idx + 1),
  ];
  return updated;
}

export function addTimePunch(punch: Omit<TimePunch, "id">): TimePunch {
  const id = `p${Date.now()}`;
  const newPunch: TimePunch = { ...punch, id };
  punches = [...punches, newPunch];
  return newPunch;
}

export function deleteTimePunch(id: string): boolean {
  const idx = punches.findIndex((p) => p.id === id);
  if (idx < 0) return false;
  punches = [...punches.slice(0, idx), ...punches.slice(idx + 1)];
  return true;
}
