/** Bi-weekly periods are 14 days starting on the anchor date the admin chose. */

const MS_PER_DAY = 24 * 60 * 60 * 1000;

export interface DateRange {
  start: Date;
  end: Date;
  dates: Date[];
}

export function parseDateOnly(value: string): Date {
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  return new Date(year, (month ?? 1) - 1, day ?? 1);
}

export function startOfDay(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

export function toDateKey(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

export function toWallClockIso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

export function getPeriodIndex(d: Date, anchor: Date): number {
  const diffDays = Math.round((startOfDay(d).getTime() - startOfDay(anchor).getTime()) / MS_PER_DAY);
  return Math.floor(diffDays / 14);
}

export function getPeriodStart(periodIndex: number, anchor: Date): Date {
  const start = startOfDay(anchor);
  start.setDate(start.getDate() + periodIndex * 14);
  return start;
}

export function getBiWeeklyDateRange(periodIndex: number, anchor: Date): DateRange {
  const start = getPeriodStart(periodIndex, anchor);
  const end = new Date(start);
  end.setDate(end.getDate() + 13);
  end.setHours(23, 59, 59, 999);

  const dates: Date[] = [];
  for (let i = 0; i < 14; i++) {
    const d = new Date(start);
    d.setDate(d.getDate() + i);
    dates.push(d);
  }

  return { start, end, dates };
}

export function getCurrentPeriodIndex(anchor: Date): number {
  return getPeriodIndex(new Date(), anchor);
}

export function getPeriodOptions(
  aroundIndex: number,
  anchor: Date,
  count = 12
): { index: number; label: string }[] {
  const options: { index: number; label: string }[] = [];
  const half = Math.floor(count / 2);
  for (let i = aroundIndex - half; i <= aroundIndex + half; i++) {
    const range = getBiWeeklyDateRange(i, anchor);
    const startStr = range.start.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const endStr = range.end.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    options.push({ index: i, label: `${startStr} – ${endStr}` });
  }
  return options;
}
