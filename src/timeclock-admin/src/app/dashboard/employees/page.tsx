"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  Plus,
  Search,
  Pencil,
  Trash2,
  UserCircle,
} from "lucide-react";
import type { Employee, Location, RateType } from "@/types/employee";
import {
  checkEmployeeNumber,
  createEmployee,
  deleteEmployee,
  getNextEmployeeNumber,
  listEmployees,
  listLocations,
  updateEmployee,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Card, CardContent } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export default function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showBulkEditModal, setShowBulkEditModal] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState<string | "bulk" | null>(null);
  const [busy, setBusy] = useState(false);

  const refreshEmployees = async () => {
    const data = await listEmployees();
    setEmployees(data);
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        const [data, locationData] = await Promise.all([
          listEmployees(),
          listLocations(),
        ]);
        if (!cancelled) {
          setEmployees(data);
          setLocations(locationData);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Failed to load employees.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const filteredEmployees = useMemo(() => {
    let result = employees;
    if (search) {
      const q = search.toLowerCase();
      result = result.filter(
        (e) =>
          e.firstName.toLowerCase().includes(q) ||
          e.lastName.toLowerCase().includes(q) ||
          e.email.toLowerCase().includes(q) ||
          e.employeeNumber.toLowerCase().includes(q) ||
          (e.pin ?? "").toLowerCase().includes(q)
      );
    }
    if (statusFilter === "active") result = result.filter((e) => e.isActive);
    if (statusFilter === "inactive") result = result.filter((e) => !e.isActive);
    return result;
  }, [employees, search, statusFilter]);

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleSelectAll = () => {
    if (selectedIds.size === filteredEmployees.length) setSelectedIds(new Set());
    else setSelectedIds(new Set(filteredEmployees.map((e) => e.id)));
  };

  const handleDelete = async (id: string) => {
    try {
      setBusy(true);
      await deleteEmployee(id);
      await refreshEmployees();
      setSelectedIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      setShowDeleteConfirm(null);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete employee.");
    } finally {
      setBusy(false);
    }
  };

  const handleBulkDelete = async () => {
    try {
      setBusy(true);
      await Promise.all([...selectedIds].map((id) => deleteEmployee(id)));
      await refreshEmployees();
      setSelectedIds(new Set());
      setShowDeleteConfirm(null);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to delete employees.");
    } finally {
      setBusy(false);
    }
  };

  const handleCreate = async (emp: {
    firstName: string;
    lastName: string;
    email: string;
    employeeNumber: string;
    isActive: boolean;
    locationId?: string | null;
    rateType?: RateType;
    hourlyRate?: number;
    salaryAmount?: number;
    generateEmployeeNumber?: boolean;
  }) => {
    try {
      setBusy(true);
      await createEmployee(emp);
      await refreshEmployees();
      setShowCreateModal(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to create employee.");
    } finally {
      setBusy(false);
    }
  };

  const handleBulkStatus = async (isActive: boolean) => {
    try {
      setBusy(true);
      const selected = employees.filter((e) => selectedIds.has(e.id));
      await Promise.all(
        selected.map((e) =>
          updateEmployee(e.id, {
            firstName: e.firstName,
            lastName: e.lastName,
            email: e.email,
            employeeNumber: e.employeeNumber,
            isActive,
            locationId: e.locationId ?? null,
          })
        )
      );
      await refreshEmployees();
      setShowBulkEditModal(false);
      setSelectedIds(new Set());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update employees.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Employees</h1>
          <p className="mt-1 text-muted-foreground">
            Manage employees, home locations, and 4-digit clock numbers
          </p>
        </div>
        <Button onClick={() => setShowCreateModal(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Employee
        </Button>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name, email, employee number, or clock number..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-10"
          />
        </div>
        <Select
          value={statusFilter}
          onValueChange={(v) => setStatusFilter(v as typeof statusFilter)}
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All status</SelectItem>
            <SelectItem value="active">Active only</SelectItem>
            <SelectItem value="inactive">Inactive only</SelectItem>
          </SelectContent>
        </Select>
        {selectedIds.size > 0 && (
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">
              {selectedIds.size} selected
            </span>
            <Button variant="outline" size="sm" onClick={() => setShowBulkEditModal(true)}>
              <Pencil className="mr-1.5 h-4 w-4" />
              Bulk Edit
            </Button>
            <Button
              variant="outline"
              size="sm"
              className="border-destructive/50 text-destructive hover:bg-destructive/10 hover:text-destructive"
              onClick={() => setShowDeleteConfirm("bulk")}
            >
              <Trash2 className="mr-1.5 h-4 w-4" />
              Delete
            </Button>
          </div>
        )}
      </div>

      <Card>
        <CardContent className="p-0">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead className="w-12">
                  <Checkbox
                    checked={
                      filteredEmployees.length > 0 &&
                      selectedIds.size === filteredEmployees.length
                    }
                    onCheckedChange={toggleSelectAll}
                  />
                </TableHead>
                <TableHead>Employee</TableHead>
                <TableHead>Employee #</TableHead>
                <TableHead>Location</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Clock #</TableHead>
                <TableHead>Hourly Rate</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-20" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {filteredEmployees.map((emp) => (
                <TableRow key={emp.id} className="group">
                  <TableCell onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={selectedIds.has(emp.id)}
                      onCheckedChange={() => toggleSelect(emp.id)}
                    />
                  </TableCell>
                  <TableCell>
                    <Link
                      href={`/dashboard/employees/${emp.id}`}
                      className="flex items-center gap-3"
                    >
                      <div className="flex h-9 w-9 items-center justify-center rounded-full bg-muted text-muted-foreground">
                        <UserCircle className="h-5 w-5" />
                      </div>
                      <span className="font-medium">
                        {emp.firstName} {emp.lastName}
                      </span>
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {emp.employeeNumber}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {emp.locationName ?? "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{emp.email}</TableCell>
                  <TableCell className="font-mono text-muted-foreground">
                    {emp.pin || "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {emp.wageRate?.rateType === "Salary"
                      ? "Salary"
                      : emp.wageRate?.hourlyRate != null
                        ? `$${emp.wageRate.hourlyRate.toFixed(2)}`
                        : "—"}
                  </TableCell>
                  <TableCell>
                    <span
                      className={cn(
                        "inline-flex rounded-md px-2.5 py-0.5 text-xs font-medium",
                        emp.isActive
                          ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                          : "bg-muted text-muted-foreground"
                      )}
                    >
                      {emp.isActive ? "Active" : "Inactive"}
                    </span>
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center gap-1 opacity-0 transition-opacity group-hover:opacity-100">
                      <Button variant="ghost" size="icon" className="h-8 w-8" asChild>
                        <Link href={`/dashboard/employees/${emp.id}`} title="Edit">
                          <Pencil className="h-4 w-4" />
                        </Link>
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                        onClick={() => setShowDeleteConfirm(emp.id)}
                        title="Delete"
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
          {loading && (
            <div className="px-8 py-12 text-center text-muted-foreground">
              Loading employees...
            </div>
          )}
          {!loading && filteredEmployees.length === 0 && (
            <div className="px-8 py-12 text-center text-muted-foreground">
              No employees found.
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog open={!!showDeleteConfirm} onOpenChange={() => setShowDeleteConfirm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              Delete employee{showDeleteConfirm === "bulk" ? "s" : ""}?
            </DialogTitle>
            <DialogDescription>
              {showDeleteConfirm === "bulk"
                ? `This hides ${selectedIds.size} employee(s). Their punches and pay records stay in the database.`
                : "This hides the employee. Their punches and pay records stay in the database."}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDeleteConfirm(null)} disabled={busy}>
              Cancel
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() =>
                showDeleteConfirm === "bulk"
                  ? handleBulkDelete()
                  : showDeleteConfirm && handleDelete(showDeleteConfirm)
              }
            >
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {showCreateModal && (
        <CreateEmployeeModal
          busy={busy}
          locations={locations}
          onClose={() => setShowCreateModal(false)}
          onCreate={handleCreate}
        />
      )}

      {showBulkEditModal && (
        <BulkEditModal
          busy={busy}
          count={selectedIds.size}
          onClose={() => setShowBulkEditModal(false)}
          onSetActive={handleBulkStatus}
        />
      )}
    </div>
  );
}

function CreateEmployeeModal({
  busy,
  locations,
  onClose,
  onCreate,
}: {
  busy: boolean;
  locations: Location[];
  onClose: () => void;
  onCreate: (emp: {
    firstName: string;
    lastName: string;
    email: string;
    employeeNumber: string;
    isActive: boolean;
    locationId?: string | null;
    rateType?: RateType;
    hourlyRate?: number;
    salaryAmount?: number;
    generateEmployeeNumber?: boolean;
  }) => void;
}) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [employeeNumber, setEmployeeNumber] = useState("");
  const [generateEmployeeNumber, setGenerateEmployeeNumber] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [generateError, setGenerateError] = useState<string | null>(null);
  const [numberError, setNumberError] = useState<string | null>(null);

  const handleCheckNumber = async () => {
    const value = employeeNumber.trim();
    if (!value || generateEmployeeNumber) {
      setNumberError(null);
      return;
    }
    try {
      const result = await checkEmployeeNumber(value);
      setNumberError(result.available ? null : result.message);
    } catch (err) {
      setNumberError(err instanceof Error ? err.message : "Could not check the employee number.");
    }
  };
  const [locationId, setLocationId] = useState("");
  const [rateType, setRateType] = useState<RateType>("Hourly");
  const [payAmount, setPayAmount] = useState("");
  const [isActive, setIsActive] = useState(true);

  const handleGenerate = async () => {
    try {
      setGenerating(true);
      setGenerateError(null);
      const next = await getNextEmployeeNumber();
      setEmployeeNumber(next.employeeNumber);
      setGenerateEmployeeNumber(true);
      setNumberError(null);
    } catch (err) {
      setGenerateError(err instanceof Error ? err.message : "Could not generate a number.");
    } finally {
      setGenerating(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = payAmount ? parseFloat(payAmount) : undefined;
    const amount = parsed !== undefined && !Number.isNaN(parsed) ? parsed : undefined;
    onCreate({
      firstName,
      lastName,
      email,
      employeeNumber,
      isActive,
      locationId: locationId || null,
      rateType,
      hourlyRate: rateType === "Hourly" ? amount : undefined,
      salaryAmount: rateType === "Salary" ? amount : undefined,
      generateEmployeeNumber,
    });
  };

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add Employee</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>First name</Label>
              <Input
                value={firstName}
                onChange={(e) => setFirstName(e.target.value)}
                required
              />
            </div>
            <div className="space-y-2">
              <Label>Last name</Label>
              <Input
                value={lastName}
                onChange={(e) => setLastName(e.target.value)}
                required
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Email</Label>
            <Input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label>Employee number</Label>
            <div className="flex gap-2">
              <Input
                value={employeeNumber}
                onChange={(e) => {
                  setEmployeeNumber(e.target.value);
                  setGenerateEmployeeNumber(false);
                  setNumberError(null);
                }}
                onBlur={handleCheckNumber}
                required={!generateEmployeeNumber}
                placeholder={generateEmployeeNumber ? "1000" : "Type or generate"}
              />
              <Button
                type="button"
                variant="outline"
                onClick={handleGenerate}
                disabled={busy || generating}
              >
                {generating ? "Generating..." : "Generate"}
              </Button>
            </div>
            <p className="text-xs text-muted-foreground">
              Generate assigns the next 4-digit number, starting at 1000. The employee
              can enter it to clock in or out without an NFC badge.
            </p>
            {generateError && (
              <p className="text-xs text-destructive">{generateError}</p>
            )}
            {numberError && (
              <p className="text-xs text-destructive">{numberError}</p>
            )}
          </div>
          <div className="space-y-2">
            <Label>Home location</Label>
            <Select value={locationId || "none"} onValueChange={(v) => setLocationId(v === "none" ? "" : v)}>
              <SelectTrigger>
                <SelectValue placeholder="Unassigned" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="none">Unassigned</SelectItem>
                {locations.filter((l) => l.isActive).map((location) => (
                  <SelectItem key={location.id} value={location.id}>
                    {location.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label>Pay type</Label>
              <Select value={rateType} onValueChange={(v) => setRateType(v as RateType)}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Hourly">Hourly</SelectItem>
                  <SelectItem value="Salary">Salary</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>{rateType === "Salary" ? "Salary amount ($)" : "Hourly rate ($)"}</Label>
              <Input
                type="number"
                step="0.01"
                min="0"
                value={payAmount}
                onChange={(e) => setPayAmount(e.target.value)}
                placeholder="Optional"
              />
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Checkbox
              id="active"
              checked={isActive}
              onCheckedChange={(c) => setIsActive(!!c)}
            />
            <Label htmlFor="active">Active</Label>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={busy}>
              Cancel
            </Button>
            <Button type="submit" disabled={busy || !!numberError}>Create</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function BulkEditModal({
  busy,
  count,
  onClose,
  onSetActive,
}: {
  busy: boolean;
  count: number;
  onClose: () => void;
  onSetActive: (isActive: boolean) => void;
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Bulk Edit ({count} employees)</DialogTitle>
          <DialogDescription>
            Set status for the selected employees.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Close
          </Button>
          <Button variant="secondary" onClick={() => onSetActive(false)} disabled={busy}>
            Mark inactive
          </Button>
          <Button onClick={() => onSetActive(true)} disabled={busy}>
            Mark active
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
