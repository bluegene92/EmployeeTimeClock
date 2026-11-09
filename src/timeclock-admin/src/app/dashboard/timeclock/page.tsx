"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  createTimePunch,
  deleteTimePunch,
  getPayPeriodSettings,
  listEmployees,
  listLocations,
  listResolvedDayRates,
  listTimePunches,
  setDayRate,
  unverifyPayPeriod,
  updateTimePunch,
  verifyPayPeriod,
} from "@/lib/api";
import {
  getBiWeeklyDateRange,
  toDateKey,
  toWallClockIso,
  getCurrentPeriodIndex,
  getPeriodOptions,
  parseDateOnly,
} from "@/lib/biWeeklyUtils";
import type { Employee, Location, ResolvedDayRate } from "@/types/employee";
import type { TimePunch } from "@/types/timePunch";
import { ChevronDown, ChevronUp, Pencil } from "lucide-react";
import { cn } from "@/lib/utils";

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString("en-US", {
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

function formatDate(d: Date): string {
  return d.toLocaleDateString("en-US", { weekday: "short", month: "short", day: "numeric" });
}

function calculateHoursForPunches(punches: TimePunch[]): number {
  const sorted = [...punches].sort(
    (a, b) => new Date(a.punchedAt).getTime() - new Date(b.punchedAt).getTime()
  );
  let totalMs = 0;
  let lastIn: Date | null = null;
  for (const p of sorted) {
    const dt = new Date(p.punchedAt);
    if (p.punchType === "In") {
      lastIn = dt;
    } else if (p.punchType === "Out" && lastIn) {
      totalMs += dt.getTime() - lastIn.getTime();
      lastIn = null;
    }
  }
  return totalMs / (1000 * 60 * 60);
}

export default function TimeClockPage() {
  const [anchor, setAnchor] = useState<Date | null>(null);
  const [periodIndex, setPeriodIndex] = useState(0);
  const [sortAsc, setSortAsc] = useState(true);
  const [locationFilter, setLocationFilter] = useState("all");
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [punches, setPunches] = useState<TimePunch[]>([]);
  const [dayRates, setDayRates] = useState<ResolvedDayRate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingCell, setEditingCell] = useState<{
    employeeId: string;
    employeeName: string;
    homeLocationId: string | null;
    date: Date;
    dateKey: string;
    isSalary: boolean;
    verified: boolean;
    dayRate: number | null;
  } | null>(null);

  const range = useMemo(
    () => (anchor ? getBiWeeklyDateRange(periodIndex, anchor) : null),
    [anchor, periodIndex]
  );

  const periodOptions = useMemo(
    () => (anchor ? getPeriodOptions(periodIndex, anchor, 16) : []),
    [anchor, periodIndex]
  );

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const settings = await getPayPeriodSettings();
        if (cancelled) return;
        const nextAnchor = parseDateOnly(settings.anchorDate);
        setAnchor(nextAnchor);
        setPeriodIndex(getCurrentPeriodIndex(nextAnchor));
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load the pay period.");
        setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const loadData = async (currentRange: NonNullable<typeof range>) => {
    const endExclusive = new Date(currentRange.start);
    endExclusive.setDate(endExclusive.getDate() + 14);
    const [empList, punchList, locationList, rates] = await Promise.all([
      listEmployees(),
      listTimePunches(currentRange.start, endExclusive),
      listLocations(),
      listResolvedDayRates(toDateKey(currentRange.start), toDateKey(currentRange.dates[13])),
    ]);
    setEmployees(empList);
    setPunches(punchList);
    setLocations(locationList);
    setDayRates(rates);
  };

  useEffect(() => {
    if (!range) return;
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        await loadData(range);
        if (!cancelled) setError(null);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load time clock data.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [range?.start.getTime()]);

  const rateByKey = useMemo(() => {
    const map = new Map<string, ResolvedDayRate>();
    for (const rate of dayRates) map.set(`${rate.employeeId}|${rate.workDate}`, rate);
    return map;
  }, [dayRates]);

  const rows = useMemo(() => {
    const byId = new Map<string, Employee>();
    for (const employee of employees) byId.set(employee.id, employee);
    for (const punch of punches) {
      if (byId.has(punch.employeeId)) continue;
      byId.set(punch.employeeId, {
        id: punch.employeeId,
        firstName: punch.employeeFirstName ?? "Former",
        lastName: punch.employeeLastName ?? "employee",
        email: "",
        employeeNumber: "",
        isActive: false,
        isDeleted: true,
        createdAt: "",
        updatedAt: "",
      });
    }
    let list = [...byId.values()];
    if (locationFilter !== "all") list = list.filter((employee) => employee.locationId === locationFilter);
    list.sort((a, b) => {
      const nameA = `${a.firstName} ${a.lastName}`.toLowerCase();
      const nameB = `${b.firstName} ${b.lastName}`.toLowerCase();
      return sortAsc ? nameA.localeCompare(nameB) : nameB.localeCompare(nameA);
    });
    return list;
  }, [employees, punches, locationFilter, sortAsc]);

  const grid = useMemo(() => {
    const map = new Map<string, Map<string, TimePunch[]>>();
    for (const punch of punches) {
      const dateKey = toDateKey(new Date(punch.punchedAt));
      let empMap = map.get(punch.employeeId);
      if (!empMap) {
        empMap = new Map();
        map.set(punch.employeeId, empMap);
      }
      const list = empMap.get(dateKey) ?? [];
      list.push(punch);
      empMap.set(dateKey, list);
    }
    for (const empMap of map.values()) {
      for (const list of empMap.values()) {
        list.sort((a, b) => new Date(a.punchedAt).getTime() - new Date(b.punchedAt).getTime());
      }
    }
    return map;
  }, [punches]);

  const reload = async () => {
    if (!range) return;
    await loadData(range);
  };

  const toggleVerify = async (employeeId: string, verified: boolean) => {
    if (!range) return;
    try {
      const periodStart = toDateKey(range.start);
      if (verified) await unverifyPayPeriod(employeeId, periodStart);
      else await verifyPayPeriod(employeeId, periodStart);
      await reload();
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update verification.");
    }
  };

  return (
    <div className="p-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Time Clock</h1>
          <p className="mt-1 text-muted-foreground">
            Punch times are local. Pay uses each day’s rate. Employees can punch at any location.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <Select value={locationFilter} onValueChange={setLocationFilter}>
            <SelectTrigger className="w-[200px]">
              <SelectValue placeholder="All locations" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All home locations</SelectItem>
              {locations.map((location) => (
                <SelectItem key={location.id} value={location.id}>{location.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={String(periodIndex)} onValueChange={(v) => setPeriodIndex(Number(v))}>
            <SelectTrigger className="w-[280px]">
              <SelectValue placeholder="Select bi-weekly period" />
            </SelectTrigger>
            <SelectContent>
              {periodOptions.map((opt) => (
                <SelectItem key={opt.index} value={String(opt.index)}>{opt.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button variant="outline" size="sm" onClick={() => setSortAsc(!sortAsc)}>
            Employee {sortAsc ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}
          </Button>
        </div>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <Card>
        <CardContent className="overflow-x-auto p-0">
          {loading || !range ? (
            <div className="px-8 py-12 text-center text-muted-foreground">Loading time clock...</div>
          ) : (
            <div className="min-w-[800px]">
              <table className="w-full border-collapse text-sm">
                <thead>
                  <tr className="border-b border-border bg-muted/50">
                    <th className="sticky left-0 z-10 min-w-[200px] border-r border-border bg-muted/50 px-3 py-2 text-left font-medium">
                      Employee
                    </th>
                    {range.dates.map((d) => (
                      <th key={d.toISOString()} className="min-w-[125px] border-r border-border px-2 py-2 text-center font-medium last:border-r-0">
                        {formatDate(d)}
                      </th>
                    ))}
                    <th className="min-w-[80px] px-2 py-2 text-center font-medium">Total hrs</th>
                    <th className="min-w-[100px] px-2 py-2 text-center font-medium">Total wages</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((emp) => {
                    const periodKey = `${emp.id}|${toDateKey(range.start)}`;
                    const verified = rateByKey.get(periodKey)?.isVerified ?? false;
                    const isSalary = emp.wageRate?.rateType === "Salary" || rateByKey.get(periodKey)?.rateType === "Salary";
                    let totalHours = 0;
                    return (
                      <tr key={emp.id} className="group border-b border-border hover:bg-muted/20">
                        <td className="sticky left-0 z-10 border-r border-border bg-background px-3 py-2 font-medium">
                          <div>
                            {emp.firstName} {emp.lastName}
                            {emp.isDeleted && <span className="ml-1 text-xs font-normal text-muted-foreground">(hidden)</span>}
                            {!emp.isActive && !emp.isDeleted && (
                              <span className="ml-1 text-xs font-normal text-muted-foreground">(inactive)</span>
                            )}
                            <p className="text-xs font-normal text-muted-foreground">
                              {emp.locationName ?? "No home location"}
                              {isSalary
                                ? " · Salary"
                                : emp.wageRate?.hourlyRate != null
                                  ? ` · $${emp.wageRate.hourlyRate.toFixed(2)}/hr`
                                  : ""}
                            </p>
                            {!emp.isDeleted && (
                              <Button
                                variant="ghost"
                                size="sm"
                                className="mt-1 h-7 px-2 text-xs"
                                onClick={() => toggleVerify(emp.id, verified)}
                              >
                                {verified ? "Unverify rates" : "Verify rates"}
                              </Button>
                            )}
                          </div>
                        </td>
                        {range.dates.map((d) => {
                          const dateKey = toDateKey(d);
                          const dayPunches = grid.get(emp.id)?.get(dateKey) ?? [];
                          const hours = calculateHoursForPunches(dayPunches);
                          totalHours += hours;
                          const dayRate = rateByKey.get(`${emp.id}|${dateKey}`);
                          return (
                            <td
                              key={dateKey}
                              className="cursor-pointer border-r border-border px-2 py-1 align-top transition-colors last:border-r-0 hover:bg-muted/30"
                              onClick={() =>
                                setEditingCell({
                                  employeeId: emp.id,
                                  employeeName: `${emp.firstName} ${emp.lastName}`,
                                  homeLocationId: emp.locationId ?? null,
                                  date: d,
                                  dateKey,
                                  isSalary,
                                  verified,
                                  dayRate: dayRate?.hourlyRate ?? null,
                                })
                              }
                            >
                              <div className="flex min-h-[60px] flex-col items-center justify-between gap-1">
                                <div className="flex flex-wrap justify-center gap-x-2 gap-y-0.5 text-xs">
                                  {dayPunches.length === 0 ? (
                                    <span className="text-muted-foreground">—</span>
                                  ) : (
                                    dayPunches.map((p) => (
                                      <span
                                        key={p.id}
                                        className={cn(
                                          "font-medium",
                                          p.punchType === "In"
                                            ? "text-emerald-600 dark:text-emerald-400"
                                            : "text-amber-600 dark:text-amber-400"
                                        )}
                                      >
                                        {formatTime(p.punchedAt)} {p.punchType}
                                      </span>
                                    ))
                                  )}
                                </div>
                                {hours > 0 && <span className="text-[10px] text-muted-foreground">{hours.toFixed(1)}h</span>}
                                {!isSalary && dayRate?.hourlyRate != null && (
                                  <span className="text-[10px] text-muted-foreground">${dayRate.hourlyRate.toFixed(2)}</span>
                                )}
                                <span className="inline-flex h-6 w-6 shrink-0 items-center justify-center opacity-0 group-hover:opacity-100">
                                  <Pencil className="h-3 w-3 text-muted-foreground" />
                                </span>
                              </div>
                            </td>
                          );
                        })}
                        <td className="bg-muted/20 px-2 py-2 text-center font-medium">{totalHours.toFixed(1)}h</td>
                        <td className="bg-muted/20 px-2 py-2 text-center font-medium">
                          {isSalary
                            ? "Salary"
                            : (() => {
                                let pay = 0;
                                let rated = false;
                                for (const d of range.dates) {
                                  const key = toDateKey(d);
                                  const hours = calculateHoursForPunches(grid.get(emp.id)?.get(key) ?? []);
                                  const rate = rateByKey.get(`${emp.id}|${key}`)?.hourlyRate;
                                  if (rate == null) continue;
                                  rated = true;
                                  pay += hours * rate;
                                }
                                return rated ? `$${pay.toFixed(2)}` : "—";
                              })()}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>

      {editingCell && (
        <EditCellDialog
          employeeId={editingCell.employeeId}
          employeeName={editingCell.employeeName}
          homeLocationId={editingCell.homeLocationId}
          date={editingCell.date}
          dateKey={editingCell.dateKey}
          isSalary={editingCell.isSalary}
          verified={editingCell.verified}
          dayRate={editingCell.dayRate}
          locations={locations}
          punches={grid.get(editingCell.employeeId)?.get(editingCell.dateKey) ?? []}
          onClose={() => setEditingCell(null)}
          onSaved={async () => {
            setEditingCell(null);
            try {
              await reload();
              setError(null);
            } catch (err) {
              setError(err instanceof Error ? err.message : "Failed to reload punches.");
            }
          }}
        />
      )}
    </div>
  );
}

function EditCellDialog({
  employeeId,
  employeeName,
  homeLocationId,
  date,
  dateKey,
  isSalary,
  verified,
  dayRate,
  locations,
  punches,
  onClose,
  onSaved,
}: {
  employeeId: string;
  employeeName: string;
  homeLocationId: string | null;
  date: Date;
  dateKey: string;
  isSalary: boolean;
  verified: boolean;
  dayRate: number | null;
  locations: Location[];
  punches: TimePunch[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rateInput, setRateInput] = useState(dayRate != null ? String(dayRate) : "");
  const [entries, setEntries] = useState(() =>
    [...punches]
      .sort((a, b) => new Date(a.punchedAt).getTime() - new Date(b.punchedAt).getTime())
      .map((p) => ({
        id: p.id,
        type: p.punchType,
        time: new Date(p.punchedAt).toTimeString().slice(0, 5),
        locationId: p.locationId ?? homeLocationId ?? "",
        source: p.source ?? "",
      }))
  );

  const addEntry = () => {
    const last = entries[entries.length - 1];
    const nextType = !last || last.type === "Out" ? "In" : "Out";
    setEntries([
      ...entries,
      {
        id: `new-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
        type: nextType as "In" | "Out",
        time: last?.time ?? "08:30",
        locationId: last?.locationId || homeLocationId || "",
        source: "Web",
      },
    ]);
  };

  const handleSave = async () => {
    const [year, month, day] = dateKey.split("-").map(Number);
    try {
      setBusy(true);
      setError(null);
      if (!isSalary && !verified && rateInput.trim()) {
        const rate = parseFloat(rateInput);
        if (Number.isNaN(rate) || rate < 0) {
          setError("Enter a valid daily rate.");
          return;
        }
        await setDayRate({ employeeId, workDate: dateKey, hourlyRate: rate });
      }
      for (const entry of entries) {
        const [hours, minutes] = entry.time.split(":").map(Number);
        const punchedAt = toWallClockIso(new Date(year, month - 1, day, hours, minutes, 0, 0));
        const locationId = entry.locationId || null;
        if (entry.id.startsWith("new-")) {
          await createTimePunch({
            employeeId,
            punchType: entry.type,
            punchedAt,
            locationId,
            source: "Web",
          });
        } else {
          await updateTimePunch(entry.id, {
            punchType: entry.type,
            punchedAt,
            locationId,
          });
        }
      }
      const kept = new Set(entries.map((entry) => entry.id).filter((id) => !id.startsWith("new-")));
      for (const punch of punches) {
        if (!kept.has(punch.id)) await deleteTimePunch(punch.id);
      }
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save punches.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Edit punch times — {employeeName}</DialogTitle>
          <p className="text-sm text-muted-foreground">{formatDate(date)}</p>
        </DialogHeader>
        {error && <p className="text-sm text-destructive">{error}</p>}
        {!isSalary && (
          <div className="space-y-1">
            <Label>Rate for this day ($/hr)</Label>
            <Input
              type="number"
              step="0.01"
              min="0"
              value={rateInput}
              disabled={verified}
              onChange={(e) => setRateInput(e.target.value)}
            />
            <p className="text-xs text-muted-foreground">
              {verified
                ? "Rates for this pay period are verified. Unverify to change them. Clock times can still be edited."
                : "Leave this equal to the employee rate to keep following future rate changes."}
            </p>
          </div>
        )}
        {isSalary && (
          <p className="text-sm text-muted-foreground">
            Salary pay is set on the employee. This day still records hours.
          </p>
        )}
        <div className="space-y-3 py-2">
          {entries.map((entry, index) => (
            <div key={`${entry.id}-${index}`} className="space-y-1">
              <div className="flex items-center gap-2">
                <Select
                  value={entry.type}
                  onValueChange={(value) =>
                    setEntries((prev) => prev.map((item, i) => (i === index ? { ...item, type: value as "In" | "Out" } : item)))
                  }
                >
                  <SelectTrigger className="w-20"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="In">In</SelectItem>
                    <SelectItem value="Out">Out</SelectItem>
                  </SelectContent>
                </Select>
                <Input
                  type="time"
                  value={entry.time}
                  className="w-36"
                  onChange={(event) =>
                    setEntries((prev) => prev.map((item, i) => (i === index ? { ...item, time: event.target.value } : item)))
                  }
                />
                <Button
                  variant="ghost"
                  size="icon"
                  className="h-8 w-8 text-destructive"
                  onClick={() => setEntries((prev) => prev.filter((_, i) => i !== index))}
                >
                  ×
                </Button>
              </div>
              <Select
                value={entry.locationId || "none"}
                onValueChange={(value) =>
                  setEntries((prev) => prev.map((item, i) => (i === index ? { ...item, locationId: value === "none" ? "" : value } : item)))
                }
              >
                <SelectTrigger><SelectValue placeholder="Location punched" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">No location</SelectItem>
                  {locations.map((location) => (
                    <SelectItem key={location.id} value={location.id}>{location.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {entry.source && !entry.id.startsWith("new-") && (
                <p className="text-xs text-muted-foreground">Source: {entry.source}</p>
              )}
            </div>
          ))}
          <Button variant="outline" size="sm" onClick={addEntry}>+ Add punch</Button>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button onClick={handleSave} disabled={busy}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
