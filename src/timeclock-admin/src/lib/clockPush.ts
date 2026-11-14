"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { ClockDevice, ClockDevicesStatus } from "@/types/clockDevice";
import { getClockStatus, pushEmployeeListToClocks } from "@/lib/api";

/** Clocks check in every 15 seconds; allow for a download and a confirming check-in. */
const CONFIRM_TIMEOUT_MS = 60_000;
const POLL_INTERVAL_MS = 3_000;

export type ClockPushState =
  | { phase: "idle" }
  | { phase: "sending"; devices: ClockDevice[] }
  | { phase: "done"; devices: ClockDevice[]; updated: ClockDevice[]; failed: ClockDevice[] }
  | { phase: "error"; message: string };

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Sends the employee list to every clock and waits for each one to confirm it.
 * The API cannot contact a clock directly; clocks see the new list version on
 * their next check-in, download it, and check in again with that version.
 */
export function useClockPush() {
  const [state, setState] = useState<ClockPushState>({ phase: "idle" });
  const run = useRef(0);

  useEffect(() => () => {
    run.current++;
  }, []);

  const track = useCallback(async (start: Promise<ClockDevicesStatus>) => {
    const runId = ++run.current;
    try {
      let status = await start;
      const deadline = Date.now() + CONFIRM_TIMEOUT_MS;
      setState({ phase: "sending", devices: status.devices });

      // Stop once every clock that is online has confirmed; offline clocks cannot
      // confirm, but keep polling in case one comes back before the deadline.
      while (
        status.devices.some((d) => d.online && !d.upToDate) &&
        Date.now() < deadline
      ) {
        await sleep(POLL_INTERVAL_MS);
        if (runId !== run.current) return;
        status = await getClockStatus();
        setState({ phase: "sending", devices: status.devices });
      }

      if (runId !== run.current) return;
      setState({
        phase: "done",
        devices: status.devices,
        updated: status.devices.filter((d) => d.upToDate),
        failed: status.devices.filter((d) => !d.upToDate),
      });
    } catch (err) {
      if (runId !== run.current) return;
      setState({
        phase: "error",
        message: err instanceof Error ? err.message : "Could not reach the time clock API.",
      });
    }
  }, []);

  /** Forces every clock to download the list again, even if nothing changed. */
  const push = useCallback(() => track(pushEmployeeListToClocks()), [track]);

  /** Waits for clocks to pick up a change the API already made, such as a new card. */
  const confirm = useCallback(() => track(getClockStatus()), [track]);

  const dismiss = useCallback(() => {
    run.current++;
    setState({ phase: "idle" });
  }, []);

  return { state, push, confirm, dismiss };
}

export function describeLastSeen(device: ClockDevice) {
  const s = device.secondsSinceSeen;
  if (s < 60) return "just now";
  if (s < 3600) return `${Math.round(s / 60)} min ago`;
  if (s < 86400) return `${Math.round(s / 3600)} h ago`;
  return `${Math.round(s / 86400)} days ago`;
}

/** 45 -> "45s", 3720 -> "1h 2m", 93600 -> "1d 2h" */
export function formatDuration(seconds: number) {
  const s = Math.max(0, Math.round(seconds));
  if (s < 60) return `${s}s`;
  const minutes = Math.floor(s / 60);
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return minutes % 60 ? `${hours}h ${minutes % 60}m` : `${hours}h`;
  const days = Math.floor(hours / 24);
  return hours % 24 ? `${days}d ${hours % 24}h` : `${days}d`;
}

export function clockLabel(device: ClockDevice) {
  return device.locationName ? `${device.locationName} (${device.deviceId})` : device.deviceId;
}
