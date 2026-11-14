"use client";

import { CheckCircle2, Loader2, TriangleAlert, X } from "lucide-react";
import type { ClockPushState } from "@/lib/clockPush";
import { clockLabel, describeLastSeen } from "@/lib/clockPush";
import { cn } from "@/lib/utils";

export function ClockPushStatus({
  state,
  onDismiss,
  className,
}: {
  state: ClockPushState;
  onDismiss: () => void;
  className?: string;
}) {
  if (state.phase === "idle") return null;

  let tone: "info" | "ok" | "warn" | "bad" = "info";
  let title = "";
  let lines: string[] = [];

  if (state.phase === "error") {
    tone = "bad";
    title = "Could not send the employee list to the clocks.";
    lines = [state.message];
  } else if (state.devices.length === 0) {
    tone = "warn";
    title = "No clocks have connected yet.";
    lines = ["Power on a clock with the current firmware. It appears here after its first check-in."];
  } else if (state.phase === "sending") {
    const confirmed = state.devices.filter((d) => d.upToDate).length;
    title = `Sending the employee list to the clocks... ${confirmed} of ${state.devices.length} confirmed.`;
    lines = state.devices
      .filter((d) => !d.online)
      .map((d) => `${clockLabel(d)} is offline (last seen ${describeLastSeen(d)}). It will update when it reconnects.`);
  } else if (state.failed.length === 0) {
    tone = "ok";
    title =
      state.devices.length === 1
        ? "The clock has the latest employee list."
        : `All ${state.devices.length} clocks have the latest employee list.`;
  } else {
    tone = "bad";
    title = `${state.updated.length} of ${state.devices.length} clocks updated.`;
    lines = state.failed.map((d) =>
      d.online
        ? `${clockLabel(d)} is online but did not confirm within a minute.`
        : `${clockLabel(d)} could not be reached. It is offline (last seen ${describeLastSeen(d)}) and will update when it reconnects.`
    );
  }

  const Icon = tone === "ok" ? CheckCircle2 : tone === "info" ? Loader2 : TriangleAlert;

  return (
    <div
      role="status"
      className={cn(
        "flex items-start gap-3 rounded-md border px-3 py-2 text-sm",
        tone === "ok" && "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
        tone === "info" && "border-border bg-muted text-foreground",
        tone === "warn" && "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
        tone === "bad" && "border-destructive/30 bg-destructive/10 text-destructive",
        className
      )}
    >
      <Icon className={cn("mt-0.5 h-4 w-4 shrink-0", tone === "info" && "animate-spin")} />
      <div className="flex-1">
        <p className="font-medium">{title}</p>
        {lines.map((line) => (
          <p key={line}>{line}</p>
        ))}
      </div>
      {state.phase !== "sending" && (
        <button type="button" onClick={onDismiss} className="opacity-70 hover:opacity-100" aria-label="Dismiss">
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}
