"use client";

import { useEffect, useMemo, useState } from "react";
import { getPayPeriodSettings, updatePayPeriodSettings } from "@/lib/api";
import { getAppEnvironment, setAppEnvironment, type AppEnvironment } from "@/lib/environment";
import { getBiWeeklyDateRange, getCurrentPeriodIndex, parseDateOnly, toDateKey } from "@/lib/biWeeklyUtils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

function monthDate(day: number) {
  const now = new Date();
  return toDateKey(new Date(now.getFullYear(), now.getMonth(), day));
}

export default function SettingsPage() {
  const [anchorDate, setAnchorDate] = useState("");
  const [saved, setSaved] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [environment, setEnvironment] = useState<AppEnvironment>("production");

  useEffect(() => {
    setEnvironment(getAppEnvironment());
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const settings = await getPayPeriodSettings();
        if (!cancelled) {
          setAnchorDate(settings.anchorDate.slice(0, 10));
          setSaved(settings.anchorDate.slice(0, 10));
          setError(null);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load settings.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const preview = useMemo(() => {
    if (!anchorDate) return [];
    const anchor = parseDateOnly(anchorDate);
    const current = getCurrentPeriodIndex(anchor);
    return [-1, 0, 1].map((offset) => {
      const range = getBiWeeklyDateRange(current + offset, anchor);
      const start = range.start.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
      const end = range.end.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
      return { key: current + offset, label: `${start} – ${end}`, current: offset === 0 };
    });
  }, [anchorDate]);

  const handleSave = async () => {
    try {
      setBusy(true);
      const settings = await updatePayPeriodSettings(anchorDate);
      setSaved(settings.anchorDate.slice(0, 10));
      setAnchorDate(settings.anchorDate.slice(0, 10));
      setMessage("Pay period start saved.");
      setError(null);
    } catch (err) {
      setMessage(null);
      setError(err instanceof Error ? err.message : "Failed to save the pay period start.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="p-8">
      <div className="mb-8">
        <h1 className="text-2xl font-semibold text-foreground">Settings</h1>
        <p className="mt-1 text-muted-foreground">
          Bi-weekly pay periods are 14 days, counted from the start date you pick.
        </p>
      </div>

      <Card className="mb-6 max-w-xl">
        <CardHeader>
          <CardTitle>Environment</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm text-muted-foreground">
            Production uses the live database. Test sends every request through the test API, which stores data in the test database.
          </p>
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant={environment === "production" ? "default" : "outline"}
              onClick={() => environment !== "production" && setAppEnvironment("production")}
            >
              Production
            </Button>
            <Button
              type="button"
              variant={environment === "test" ? "default" : "outline"}
              className={environment === "test" ? "bg-amber-400 text-amber-950 hover:bg-amber-300" : ""}
              onClick={() => environment !== "test" && setAppEnvironment("test")}
            >
              Test
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card className="max-w-xl">
        <CardHeader>
          <CardTitle>Pay period start</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? (
            <p className="text-sm text-muted-foreground">Loading...</p>
          ) : (
            <>
              <p className="text-sm text-muted-foreground">
                The 1st or the 15th are typical starting points. Every period after that is the next 14 days, including dates before today.
              </p>
              <div className="flex flex-wrap gap-2">
                <Button type="button" variant="outline" size="sm" onClick={() => setAnchorDate(monthDate(1))}>
                  1st of this month
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={() => setAnchorDate(monthDate(15))}>
                  15th of this month
                </Button>
              </div>
              <div className="space-y-2">
                <Label htmlFor="anchor">Start date</Label>
                <Input
                  id="anchor"
                  type="date"
                  value={anchorDate}
                  onChange={(e) => setAnchorDate(e.target.value)}
                />
              </div>
              <div className="space-y-1 text-sm">
                {preview.map((period) => (
                  <p key={period.key} className={period.current ? "font-medium" : "text-muted-foreground"}>
                    {period.current ? "Current: " : ""}
                    {period.label}
                  </p>
                ))}
              </div>
              {error && <p className="text-sm text-destructive">{error}</p>}
              {message && <p className="text-sm text-emerald-600">{message}</p>}
              <Button onClick={handleSave} disabled={busy || !anchorDate || anchorDate === saved}>
                Save
              </Button>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
