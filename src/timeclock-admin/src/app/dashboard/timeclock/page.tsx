"use client";

import { useState, useMemo } from "react";
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
import { getEmployees } from "@/lib/employeeStore";
import {
  getTimePunchesByDateRange,
  updateTimePunch,
  addTimePunch,
  deleteTimePunch,
} from "@/lib/timePunchStore";
import {
  getBiWeeklyDateRange,
  toDateKey,
  getCurrentPeriodIndex,
  getPeriodOptions,
} from "@/lib/biWeeklyUtils";
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

/** Pair In/Out punches and sum hours */
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
  const [periodIndex, setPeriodIndex] = useState(getCurrentPeriodIndex);
  const [sortAsc, setSortAsc] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);
  const [editingCell, setEditingCell] = useState<{
    employeeId: string;
    employeeName: string;
    date: Date;
    dateKey: string;
  } | null>(null);

  const range = useMemo(
    () => getBiWeeklyDateRange(periodIndex),
    [periodIndex]
  );

  const periodOptions = useMemo(
    () => getPeriodOptions(periodIndex, 16),
    [periodIndex]
  );

  const punches = useMemo(
    () => getTimePunchesByDateRange(range.start, range.end),
    [range.start, range.end, refreshKey]
  );

  const employees = useMemo(() => {
    const list = getEmployees().filter((e) => e.isActive);
    return [...list].sort((a, b) => {
      const nameA = `${a.firstName} ${a.lastName}`.toLowerCase();
      const nameB = `${b.firstName} ${b.lastName}`.toLowerCase();
      return sortAsc ? nameA.localeCompare(nameB) : nameB.localeCompare(nameA);
    });
  }, [sortAsc]);

  // Group punches by employeeId and date
  const grid = useMemo(() => {
    const map = new Map<string, Map<string, TimePunch[]>>();
    for (const emp of employees) {
      map.set(emp.id, new Map());
    }
    for (const p of punches) {
      const dateKey = toDateKey(new Date(p.punchedAt));
      let empMap = map.get(p.employeeId);
      if (!empMap) {
        empMap = new Map();
        map.set(p.employeeId, empMap);
      }
      const list = empMap.get(dateKey) ?? [];
      list.push(p);
      empMap.set(dateKey, list);
    }
    for (const empMap of map.values()) {
      for (const [dateKey, list] of empMap) {
        list.sort((a, b) => new Date(a.punchedAt).getTime() - new Date(b.punchedAt).getTime());
      }
    }
    return map;
  }, [employees, punches]);

  const totalHoursByEmployee = useMemo(() => {
    const map = new Map<string, number>();
    for (const emp of employees) {
      const empMap = grid.get(emp.id);
      let total = 0;
      if (empMap) {
        for (const list of empMap.values()) {
          total += calculateHoursForPunches(list);
        }
      }
      map.set(emp.id, total);
    }
    return map;
  }, [employees, grid]);

  return (
    <div className="p-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Time Clock</h1>
          <p className="mt-1 text-muted-foreground">
            Bi-weekly overview: view and edit employee punch times
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <Select
            value={String(periodIndex)}
            onValueChange={(v) => setPeriodIndex(Number(v))}
          >
            <SelectTrigger className="w-[280px]">
              <SelectValue placeholder="Select bi-weekly period" />
            </SelectTrigger>
            <SelectContent>
              {periodOptions.map((opt) => (
                <SelectItem key={opt.index} value={String(opt.index)}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setSortAsc(!sortAsc)}
            title={sortAsc ? "Sort Z–A" : "Sort A–Z"}
          >
            Employee {sortAsc ? <ChevronUp className="ml-1 h-4 w-4" /> : <ChevronDown className="ml-1 h-4 w-4" />}
          </Button>
        </div>
      </div>

      <Card>
        <CardContent className="overflow-x-auto p-0">
          <div className="min-w-[800px]">
            <table className="w-full border-collapse text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/50">
                  <th className="sticky left-0 z-10 min-w-[180px] border-r border-border bg-muted/50 px-3 py-2 text-left font-medium">
                    Employee
                  </th>
                  {range.dates.map((d) => (
                    <th
                      key={d.toISOString()}
                      className="min-w-[125px] border-r border-border px-2 py-2 text-center font-medium last:border-r-0"
                    >
                      {formatDate(d)}
                    </th>
                  ))}
                  <th className="min-w-[80px] border-border bg-muted/30 px-2 py-2 text-center font-medium">
                    Total hrs
                  </th>
                  <th className="min-w-[100px] border-border bg-muted/30 px-2 py-2 text-center font-medium">
                    Total wages
                  </th>
                </tr>
              </thead>
              <tbody>
                {employees.map((emp) => (
                  <tr key={emp.id} className="group border-b border-border hover:bg-muted/20">
                    <td className="sticky left-0 z-10 border-r border-border bg-background px-3 py-2 font-medium">
                      <div>
                        {emp.firstName} {emp.lastName}
                        {emp.wageRate?.hourlyRate != null && (
                          <p className="text-xs font-normal text-muted-foreground">
                            ${emp.wageRate.hourlyRate.toFixed(2)}/hr
                          </p>
                        )}
                      </div>
                    </td>
                    {range.dates.map((d) => {
                      const dateKey = toDateKey(d);
                      const dayPunches = grid.get(emp.id)?.get(dateKey) ?? [];
                      const hours = calculateHoursForPunches(dayPunches);
                      return (
                        <td
                          key={dateKey}
                          className="cursor-pointer border-r border-border px-2 py-1 align-top last:border-r-0 transition-colors hover:bg-muted/30"
                          onClick={() =>
                            setEditingCell({
                              employeeId: emp.id,
                              employeeName: `${emp.firstName} ${emp.lastName}`,
                              date: d,
                              dateKey,
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
                            {hours > 0 && (
                              <span className="text-[10px] text-muted-foreground">
                                {hours.toFixed(1)}h
                              </span>
                            )}
                            <span
                              className="inline-flex h-6 w-6 shrink-0 items-center justify-center opacity-0 group-hover:opacity-100"
                              title="Edit times"
                            >
                              <Pencil className="h-3 w-3 text-muted-foreground" />
                            </span>
                          </div>
                        </td>
                      );
                    })}
                    <td className="border-border bg-muted/20 px-2 py-2 text-center font-medium">
                      {totalHoursByEmployee.get(emp.id)?.toFixed(1) ?? "0"}h
                    </td>
                    <td className="border-border bg-muted/20 px-2 py-2 text-center font-medium">
                      {(() => {
                        const hours = totalHoursByEmployee.get(emp.id) ?? 0;
                        const rate = emp.wageRate?.hourlyRate;
                        if (rate == null) return "—";
                        return `$${(hours * rate).toFixed(2)}`;
                      })()}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>

      {editingCell && (
        <EditCellDialog
          employeeId={editingCell.employeeId}
          employeeName={editingCell.employeeName}
          date={editingCell.date}
          dateKey={editingCell.dateKey}
          punches={
            grid.get(editingCell.employeeId)?.get(editingCell.dateKey) ?? []
          }
          onClose={() => setEditingCell(null)}
          onSaved={() => {
            setEditingCell(null);
            setRefreshKey((k) => k + 1);
          }}
        />
      )}
    </div>
  );
}

function PunchEntryRow({
  type,
  time,
  onTypeChange,
  onTimeChange,
  onRemove,
}: {
  type: "In" | "Out";
  time: string;
  onTypeChange: (v: "In" | "Out") => void;
  onTimeChange: (v: string) => void;
  onRemove: () => void;
}) {
  return (
    <div className="flex items-center gap-2">
      <Select value={type} onValueChange={(v) => onTypeChange(v as "In" | "Out")}>
        <SelectTrigger className="w-20">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="In">In</SelectItem>
          <SelectItem value="Out">Out</SelectItem>
        </SelectContent>
      </Select>
      <Input
        type="time"
        value={time}
        onChange={(ev) => onTimeChange(ev.target.value)}
        className="w-36 min-w-[140px]"
        aria-label={`Punch ${type} time`}
      />
      <Button
        variant="ghost"
        size="icon"
        className="h-8 w-8 text-destructive hover:bg-destructive/10"
        onClick={onRemove}
        title="Remove"
      >
        ×
      </Button>
    </div>
  );
}

function EditCellDialog({
  employeeId,
  employeeName,
  date,
  dateKey,
  punches,
  onClose,
  onSaved,
}: {
  employeeId: string;
  employeeName: string;
  date: Date;
  dateKey: string;
  punches: TimePunch[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [entries, setEntries] = useState<{ id: string; type: "In" | "Out"; time: string }[]>(() => {
    const sorted = [...punches].sort(
      (a, b) => new Date(a.punchedAt).getTime() - new Date(b.punchedAt).getTime()
    );
    return sorted.map((p) => ({
      id: p.id,
      type: p.punchType,
      time: new Date(p.punchedAt).toTimeString().slice(0, 5),
    }));
  });

  const addEntry = () => {
    const last = entries[entries.length - 1];
    const nextType = !last || last.type === "Out" ? "In" : "Out";
    const nextTime = !last && nextType === "In" ? "08:30" : (last?.time ?? "09:00");
    // Unique id prevents React from reusing DOM - fixes shared state bug when In/Out showed same value
    const uniqueId = `new-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`;
    setEntries([...entries, { id: uniqueId, type: nextType, time: nextTime }]);
  };

  const removeEntry = (id: string) => {
    setEntries((prev) => prev.filter((e) => e.id !== id));
  };

  const updateEntryByIndex = (index: number, updates: Partial<{ type: "In" | "Out"; time: string }>) => {
    setEntries((prev) => {
      const next = [...prev];
      if (index >= 0 && index < next.length) {
        next[index] = { ...next[index], ...updates };
      }
      return next;
    });
  };

  const handleSave = () => {
    const [year, month, day] = dateKey.split("-").map(Number);
    for (const e of entries) {
      const [hours, minutes] = e.time.split(":").map(Number);
      const localDate = new Date(year, month - 1, day, hours, minutes, 0, 0);
      const punchedAt = localDate.toISOString();
      if (e.id.startsWith("new-")) {
        addTimePunch({
          employeeId,
          punchType: e.type,
          punchedAt,
          source: "Desktop",
        });
      } else {
        updateTimePunch(e.id, {
          punchType: e.type,
          punchedAt,
        });
      }
    }
    const entryIds = new Set(entries.map((e) => e.id).filter((id) => !id.startsWith("new-")));
    for (const p of punches) {
      if (!entryIds.has(p.id)) deleteTimePunch(p.id);
    }
    onSaved();
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>
            Edit punch times — {employeeName}
          </DialogTitle>
          <p className="text-sm text-muted-foreground">
            {formatDate(date)}
          </p>
        </DialogHeader>
        <div className="space-y-3 py-2">
          {entries.map((e, i) => (
            <PunchEntryRow
              key={`entry-${i}-${e.id}`}
              type={e.type}
              time={e.time}
              onTypeChange={(v) => updateEntryByIndex(i, { type: v })}
              onTimeChange={(v) => updateEntryByIndex(i, { time: v })}
              onRemove={() => removeEntry(e.id)}
            />
          ))}
          <Button variant="outline" size="sm" onClick={addEntry}>
            + Add punch
          </Button>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={handleSave}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
