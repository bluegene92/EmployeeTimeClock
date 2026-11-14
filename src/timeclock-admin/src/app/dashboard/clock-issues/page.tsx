"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { Check, CreditCard, RotateCcw, Trash2 } from "lucide-react";
import type { ClockFailure } from "@/types/clockFailure";
import type { Employee } from "@/types/employee";
import {
  deleteClockFailure,
  listClockFailures,
  listEmployees,
  reopenClockFailure,
  resolveClockFailure,
  setEmployeeCard,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";
import { useClockPush } from "@/lib/clockPush";
import { ClockPushStatus } from "@/components/ClockPushStatus";

const REASON_LABELS: Record<string, string> = {
  UnknownCard: "Unknown card",
  PunchRejected: "Punch rejected",
};

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

function employeeName(failure: ClockFailure) {
  const name = `${failure.employeeFirstName ?? ""} ${failure.employeeLastName ?? ""}`.trim();
  return name || null;
}

export default function ClockIssuesPage() {
  const [failures, setFailures] = useState<ClockFailure[]>([]);
  const [showResolved, setShowResolved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [assigning, setAssigning] = useState<ClockFailure | null>(null);
  const clockPush = useClockPush();

  const load = useCallback(async (includeResolved: boolean) => {
    try {
      setFailures(await listClockFailures(includeResolved));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load clock issues.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load(showResolved);
  }, [load, showResolved]);

  const run = async (failure: ClockFailure, action: (id: string) => Promise<void>, fallback: string) => {
    try {
      setBusyId(failure.id);
      await action(failure.id);
      await load(showResolved);
    } catch (err) {
      setError(err instanceof Error ? err.message : fallback);
    } finally {
      setBusyId(null);
    }
  };

  const openCount = failures.filter((f) => !f.resolvedAt).length;

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Clock Issues</h1>
          <p className="mt-1 text-muted-foreground">
            Scans at the time clock that did not record a punch. Fix the cause, add the missing punch on the
            Time Clock page if needed, then mark the issue resolved.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Checkbox
            id="show-resolved"
            checked={showResolved}
            onCheckedChange={(c) => setShowResolved(!!c)}
          />
          <Label htmlFor="show-resolved">Show resolved</Label>
        </div>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <ClockPushStatus state={clockPush.state} onDismiss={clockPush.dismiss} className="mb-4" />

      {!loading && (
        <p className="mb-3 text-sm text-muted-foreground">
          {openCount === 0 ? "No open issues." : `${openCount} open ${openCount === 1 ? "issue" : "issues"}.`}
        </p>
      )}

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Scanned</TableHead>
                <TableHead>Problem</TableHead>
                <TableHead>Card</TableHead>
                <TableHead>Employee</TableHead>
                <TableHead>Clock</TableHead>
                <TableHead className="w-40" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {failures.map((failure) => {
                const name = employeeName(failure);
                const busy = busyId === failure.id;
                const canAssign = failure.reason === "UnknownCard" && !failure.employeeId && !!failure.cardUid;
                return (
                  <TableRow key={failure.id} className={cn(failure.resolvedAt && "opacity-60")}>
                    <TableCell className="whitespace-nowrap">
                      {failure.occurredAt ? formatDateTime(failure.occurredAt) : "Clock time not set"}
                      <div className="text-xs text-muted-foreground">
                        Received {formatDateTime(failure.receivedAt)}
                      </div>
                    </TableCell>
                    <TableCell className="max-w-md">
                      <span
                        className={cn(
                          "inline-flex rounded-md px-2.5 py-0.5 text-xs font-medium",
                          failure.resolvedAt
                            ? "bg-muted text-muted-foreground"
                            : "bg-destructive/10 text-destructive"
                        )}
                      >
                        {REASON_LABELS[failure.reason] ?? failure.reason}
                        {failure.resolvedAt && " · Resolved"}
                      </span>
                      <div className="mt-1 text-sm">{failure.message}</div>
                      {failure.punchType && (
                        <div className="text-xs text-muted-foreground">Attempted: clock {failure.punchType.toLowerCase()}</div>
                      )}
                    </TableCell>
                    <TableCell className="font-mono text-xs">{failure.cardUid ?? "—"}</TableCell>
                    <TableCell>
                      {failure.employeeId && name ? (
                        <Link href={`/dashboard/employees/${failure.employeeId}`} className="hover:underline">
                          {name}
                        </Link>
                      ) : failure.employeeId ? (
                        <span className="text-muted-foreground">Unknown employee</span>
                      ) : (
                        "—"
                      )}
                    </TableCell>
                    <TableCell className="text-sm">
                      {failure.locationName && <div>{failure.locationName}</div>}
                      <div className="text-xs text-muted-foreground">{failure.deviceId ?? failure.source ?? "—"}</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex justify-end gap-1">
                        {canAssign && !failure.resolvedAt && (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8"
                            title="Assign this card to an employee"
                            disabled={busy}
                            onClick={() => setAssigning(failure)}
                          >
                            <CreditCard className="h-4 w-4" />
                          </Button>
                        )}
                        {failure.resolvedAt ? (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8"
                            title="Reopen"
                            disabled={busy}
                            onClick={() => run(failure, reopenClockFailure, "Failed to reopen issue.")}
                          >
                            <RotateCcw className="h-4 w-4" />
                          </Button>
                        ) : (
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8"
                            title="Mark resolved"
                            disabled={busy}
                            onClick={() => run(failure, resolveClockFailure, "Failed to resolve issue.")}
                          >
                            <Check className="h-4 w-4" />
                          </Button>
                        )}
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                          title="Delete"
                          disabled={busy}
                          onClick={() => run(failure, deleteClockFailure, "Failed to delete issue.")}
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
          {loading && <div className="px-8 py-12 text-center text-muted-foreground">Loading clock issues...</div>}
          {!loading && failures.length === 0 && (
            <div className="px-8 py-12 text-center text-muted-foreground">
              {showResolved ? "No clock issues recorded." : "No open clock issues."}
            </div>
          )}
        </CardContent>
      </Card>

      {assigning && (
        <AssignCardDialog
          failure={assigning}
          onClose={() => setAssigning(null)}
          onAssigned={async () => {
            setAssigning(null);
            clockPush.confirm();
            await load(showResolved);
          }}
        />
      )}
    </div>
  );
}

function AssignCardDialog({
  failure,
  onClose,
  onAssigned,
}: {
  failure: ClockFailure;
  onClose: () => void;
  onAssigned: () => void;
}) {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [employeeId, setEmployeeId] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listEmployees(true)
      .then((list) =>
        setEmployees([...list].sort((a, b) => `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`)))
      )
      .catch((err) => setError(err instanceof Error ? err.message : "Failed to load employees."));
  }, []);

  const handleAssign = async () => {
    if (!employeeId || !failure.cardUid) return;
    try {
      setBusy(true);
      await setEmployeeCard(employeeId, failure.cardUid);
      onAssigned();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to assign card.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Assign card {failure.cardUid}</DialogTitle>
          <DialogDescription>
            Replaces the employee&apos;s current card. Every open unknown-card issue for this card is marked resolved,
            and the change is sent to every clock.
          </DialogDescription>
        </DialogHeader>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <div className="space-y-2">
          <Label>Employee</Label>
          <Select value={employeeId} onValueChange={setEmployeeId}>
            <SelectTrigger className="w-full">
              <SelectValue placeholder="Choose an employee" />
            </SelectTrigger>
            <SelectContent>
              {employees.map((employee) => (
                <SelectItem key={employee.id} value={employee.id}>
                  {employee.firstName} {employee.lastName}
                  {employee.cardUid ? ` (has card ${employee.cardUid})` : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={onClose} disabled={busy}>Cancel</Button>
          <Button onClick={handleAssign} disabled={busy || !employeeId}>Assign card</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
