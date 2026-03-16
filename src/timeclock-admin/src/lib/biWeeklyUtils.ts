/** Bi-weekly periods run Monday to Sunday, 14 days (2 full weeks). */

const MS_PER_DAY = 24 * 60 * 60 * 1000;
/** Epoch: Jan 6, 2020 (Monday) - common pay period start */
const EPOCH = new Date(2020, 0, 6);

export interface DateRange {
  start: Date;
  end: Date;
  dates: Date[];
}

/** Get the Monday at start of day for a given date (in local timezone) */
function getMonday(d: Date): Date {
  const copy = new Date(d);
  const day = copy.getDay();
  const diff = day === 0 ? -6 : 1 - day; // Sunday = 0, so -6 to get prev Monday
  copy.setDate(copy.getDate() + diff);
  copy.setHours(0, 0, 0, 0);
  return copy;
}

/** Get the period index (0-based) for a given date. Each period is 14 days starting Monday. */
function getPeriodIndex(d: Date): number {
  const monday = getMonday(d);
  const diffMs = monday.getTime() - EPOCH.getTime();
  const diffDays = Math.round(diffMs / MS_PER_DAY);
  return Math.floor(diffDays / 14);
}

/** Get the start Monday for a period index */
function getPeriodStartMonday(periodIndex: number): Date {
  const d = new Date(EPOCH);
  d.setDate(d.getDate() + periodIndex * 14);
  return d;
}

export function getBiWeeklyDateRange(periodIndex: number): DateRange {
  const start = getPeriodStartMonday(periodIndex);
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

/** Get the current period index (containing today) */
export function getCurrentPeriodIndex(): number {
  return getPeriodIndex(new Date());
}

/** Get period index for a given date */
export function getPeriodIndexForDate(d: Date): number {
  return getPeriodIndex(d);
}

/** Generate period options for the selector (e.g. -6 to +2 from current) */
export function getPeriodOptions(aroundIndex: number, count = 12): { index: number; label: string }[] {
  const options: { index: number; label: string }[] = [];
  const half = Math.floor(count / 2);
  for (let i = aroundIndex - half; i <= aroundIndex + half; i++) {
    const range = getBiWeeklyDateRange(i);
    const startStr = range.start.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    const endStr = range.end.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
    options.push({ index: i, label: `${startStr} – ${endStr}` });
  }
  return options;
}

export function toDateKey(d: Date): string {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}
