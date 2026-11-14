"use client";

import { useCallback, useEffect, useState } from "react";
import { History, Send, Trash2 } from "lucide-react";
import type { ClockDevice, ClockOutage } from "@/types/clockDevice";
import { deleteClockDevice, getClockOutages, getClockStatus } from "@/lib/api";
import { clockLabel, formatDuration, useClockPush } from "@/lib/clockPush";
import { ClockPushStatus } from "@/components/ClockPushStatus";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

/** Clocks send a heartbeat every 15 seconds. */
const REFRESH_MS = 5_000;

function signalLabel(rssi: number) {
  if (rssi >= -60) return "strong";
  if (rssi >= -70) return "good";
  if (rssi >= -80) return "weak";
  return "very weak";
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export default function ClocksPage() {
  const [devices, setDevices] = useState<ClockDevice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [historyFor, setHistoryFor] = useState<ClockDevice | null>(null);
  const clockPush = useClockPush();

  const load = useCallback(async () => {
    try {
      const status = await getClockStatus();
      setDevices(status.devices);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load clocks.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    const timer = setInterval(load, REFRESH_MS);
    return () => clearInterval(timer);
  }, [load]);

  // While a push is in progress its polling is fresher than the regular refresh.
  const shown = clockPush.state.phase === "sending" ? clockPush.state.devices : devices;
  const onlineCount = shown.filter((d) => d.online).length;

  const handleRemove = async (device: ClockDevice) => {
    try {
      setBusyId(device.deviceId);
      await deleteClockDevice(device.deviceId);
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to remove clock.");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Clocks</h1>
          <p className="mt-1 text-muted-foreground">
            Each clock sends a heartbeat every 15 seconds. A clock with no heartbeat for a minute is offline, and the
            gap is recorded as downtime. Push to make every clock download the employee list now and confirm it.
          </p>
        </div>
        <Button onClick={clockPush.push} disabled={clockPush.state.phase === "sending"}>
          <Send className="mr-2 h-4 w-4" />
          Push employee list
        </Button>
      </div>

      <ClockPushStatus state={clockPush.state} onDismiss={clockPush.dismiss} className="mb-4" />

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      {shown.length > 0 && (
        <p className="mb-3 text-sm text-muted-foreground">
          <span
            className={cn(
              "font-medium",
              onlineCount === shown.length ? "text-emerald-600 dark:text-emerald-400" : "text-destructive"
            )}
          >
            {onlineCount} of {shown.length}
          </span>{" "}
          {shown.length === 1 ? "clock" : "clocks"} online
        </p>
      )}

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Clock</TableHead>
                <TableHead>Heartbeat</TableHead>
                <TableHead>Downtime</TableHead>
                <TableHead>Employee list</TableHead>
                <TableHead>Waiting to upload</TableHead>
                <TableHead>Firmware</TableHead>
                <TableHead className="w-24" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {shown.map((device) => (
                <TableRow key={device.deviceId}>
                  <TableCell>
                    <div className="font-medium">{device.locationName ?? "No location set"}</div>
                    <div className="font-mono text-xs text-muted-foreground">{device.deviceId}</div>
                  </TableCell>
                  <TableCell>
                    <span
                      className={cn(
                        "inline-flex items-center gap-1.5 rounded-md px-2.5 py-0.5 text-xs font-medium",
                        device.online
                          ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                          : "bg-destructive/10 text-destructive"
                      )}
                    >
                      <span
                        className={cn(
                          "h-1.5 w-1.5 rounded-full",
                          device.online ? "animate-pulse bg-emerald-500" : "bg-destructive"
                        )}
                      />
                      {device.online ? "Online" : "Offline"}
                    </span>
                    <div className="mt-0.5 text-xs text-muted-foreground">
                      {device.online
                        ? `Last heartbeat ${formatDuration(device.secondsSinceSeen)} ago`
                        : `Offline for ${formatDuration(device.secondsSinceSeen)}, since ${formatDateTime(device.lastSeenAt)}`}
                    </div>
                    {device.online && device.wifiRssi != null && (
                      <div className="text-xs text-muted-foreground">
                        Wi-Fi {signalLabel(device.wifiRssi)} ({device.wifiRssi} dBm)
                      </div>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    <div>
                      24 h:{" "}
                      <span className={cn(device.downtime24hSeconds > 0 && "font-medium text-destructive")}>
                        {device.downtime24hSeconds > 0 ? formatDuration(device.downtime24hSeconds) : "none"}
                      </span>
                    </div>
                    <div className="text-xs text-muted-foreground">
                      7 days: {device.downtime7dSeconds > 0 ? formatDuration(device.downtime7dSeconds) : "none"}
                      {device.outages7d > 0 && ` in ${device.outages7d} ${device.outages7d === 1 ? "outage" : "outages"}`}
                    </div>
                  </TableCell>
                  <TableCell>
                    {device.upToDate ? (
                      <span className="text-emerald-600 dark:text-emerald-400">Up to date</span>
                    ) : (
                      <span className="text-amber-600 dark:text-amber-400">Out of date</span>
                    )}
                  </TableCell>
                  <TableCell className="text-sm">
                    {device.pendingPunches === 0 && device.pendingFailures === 0
                      ? "Nothing"
                      : [
                          device.pendingPunches > 0 &&
                            `${device.pendingPunches} ${device.pendingPunches === 1 ? "punch" : "punches"}`,
                          device.pendingFailures > 0 &&
                            `${device.pendingFailures} ${device.pendingFailures === 1 ? "issue" : "issues"}`,
                        ]
                          .filter(Boolean)
                          .join(", ")}
                  </TableCell>
                  <TableCell>
                    <div className="font-mono text-xs">{device.firmwareVersion ?? "—"}</div>
                    {device.online && device.bootedAt && (
                      <div className="text-xs text-muted-foreground" title={`Started ${formatDateTime(device.bootedAt)}`}>
                        Running {formatDuration((Date.now() - new Date(device.bootedAt).getTime()) / 1000)}
                      </div>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        title="Downtime history"
                        onClick={() => setHistoryFor(device)}
                      >
                        <History className="h-4 w-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                        title="Remove a retired clock. A clock that is still running reappears on its next heartbeat."
                        disabled={busyId === device.deviceId}
                        onClick={() => handleRemove(device)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {loading && <div className="px-8 py-12 text-center text-muted-foreground">Loading clocks...</div>}
          {!loading && shown.length === 0 && (
            <div className="px-8 py-12 text-center text-muted-foreground">
              No clocks have connected yet. A clock appears here after its first heartbeat.
            </div>
          )}
        </CardContent>
      </Card>

      {historyFor && <OutageHistoryDialog device={historyFor} onClose={() => setHistoryFor(null)} />}
    </div>
  );
}

function OutageHistoryDialog({ device, onClose }: { device: ClockDevice; onClose: () => void }) {
  const [days, setDays] = useState(7);
  const [outages, setOutages] = useState<ClockOutage[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getClockOutages(device.deviceId, days)
      .then((list) => {
        if (!cancelled) {
          setOutages(list);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : "Failed to load downtime.");
      });
    return () => {
      cancelled = true;
    };
  }, [device.deviceId, days]);

  const total = outages?.reduce((sum, o) => sum + o.durationSeconds, 0) ?? 0;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>Downtime history</DialogTitle>
          <DialogDescription>
            {clockLabel(device)}. Each row is a gap of more than a minute between heartbeats, or a restart.
          </DialogDescription>
        </DialogHeader>

        <div className="flex items-center justify-between gap-2 text-sm">
          <div className="flex gap-1">
            {[1, 7, 30, 90].map((d) => (
              <Button
                key={d}
                size="sm"
                variant={d === days ? "default" : "outline"}
                onClick={() => {
                  setOutages(null);
                  setDays(d);
                }}
              >
                {d === 1 ? "24 hours" : `${d} days`}
              </Button>
            ))}
          </div>
          {outages && (
            <span className="text-muted-foreground">
              {outages.length === 0 ? "No downtime" : `${formatDuration(total)} total`}
            </span>
          )}
        </div>

        {error && <p className="text-sm text-destructive">{error}</p>}
        {!outages && !error && <p className="py-6 text-center text-sm text-muted-foreground">Loading...</p>}
        {outages && outages.length === 0 && (
          <p className="py-6 text-center text-sm text-muted-foreground">
            No downtime recorded in this period.
          </p>
        )}
        {outages && outages.length > 0 && (
          <div className="max-h-96 overflow-y-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Went offline</TableHead>
                  <TableHead>Back online</TableHead>
                  <TableHead>Duration</TableHead>
                  <TableHead>Cause</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {outages.map((o) => (
                  <TableRow key={o.startedAt}>
                    <TableCell>{formatDateTime(o.startedAt)}</TableCell>
                    <TableCell>
                      {o.endedAt ? (
                        formatDateTime(o.endedAt)
                      ) : (
                        <span className="font-medium text-destructive">Still offline</span>
                      )}
                    </TableCell>
                    <TableCell>{formatDuration(o.durationSeconds)}</TableCell>
                    <TableCell className="text-muted-foreground">
                      {o.restarted ? "Clock restarted" : "No heartbeat (Wi-Fi, power, or internet)"}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
