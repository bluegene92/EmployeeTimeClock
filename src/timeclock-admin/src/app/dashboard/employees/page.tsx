"use client";

import { useState, useMemo } from "react";
import Link from "next/link";
import {
  Plus,
  Search,
  Pencil,
  Trash2,
  UserCircle,
} from "lucide-react";
import type { Employee } from "@/types/employee";
import {
  getEmployees,
  addEmployee,
  deleteEmployee as deleteEmployeeFromStore,
} from "@/lib/employeeStore";
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
  const [employees, setEmployeesState] = useState<Employee[]>(getEmployees);
  const refreshEmployees = () => setEmployeesState(getEmployees());
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "active" | "inactive">("all");
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showBulkEditModal, setShowBulkEditModal] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState<string | "bulk" | null>(null);

  const filteredEmployees = useMemo(() => {
    let result = employees;
    if (search) {
      const q = search.toLowerCase();
      result = result.filter(
        (e) =>
          e.firstName.toLowerCase().includes(q) ||
          e.lastName.toLowerCase().includes(q) ||
          e.email.toLowerCase().includes(q) ||
          e.employeeNumber.toLowerCase().includes(q)
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

  const handleDelete = (id: string) => {
    deleteEmployeeFromStore(id);
    refreshEmployees();
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
    setShowDeleteConfirm(null);
  };

  const handleBulkDelete = () => {
    selectedIds.forEach((id) => deleteEmployeeFromStore(id));
    refreshEmployees();
    setSelectedIds(new Set());
    setShowDeleteConfirm(null);
  };

  const handleCreate = (emp: Omit<Employee, "id" | "createdAt" | "updatedAt">) => {
    addEmployee(emp);
    refreshEmployees();
    setShowCreateModal(false);
  };

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Employees</h1>
          <p className="mt-1 text-muted-foreground">
            Manage employee information and clock IDs
          </p>
        </div>
        <Button onClick={() => setShowCreateModal(true)}>
          <Plus className="mr-2 h-4 w-4" />
          Add Employee
        </Button>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <div className="relative flex-1 min-w-[200px]">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name, email, or employee number..."
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
                <TableHead>Email</TableHead>
                <TableHead>Clock ID</TableHead>
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
                      {emp.photoUrl ? (
                        <img
                          src={emp.photoUrl}
                          alt=""
                          className="h-9 w-9 rounded-full object-cover"
                        />
                      ) : (
                        <div className="flex h-9 w-9 items-center justify-center rounded-full bg-muted text-muted-foreground">
                          <UserCircle className="h-5 w-5" />
                        </div>
                      )}
                      <span className="font-medium">
                        {emp.firstName} {emp.lastName}
                      </span>
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {emp.employeeNumber}
                  </TableCell>
                  <TableCell className="text-muted-foreground">{emp.email}</TableCell>
                  <TableCell className="font-mono text-muted-foreground">
                    {emp.clockId ?? "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    ${emp.wageRate?.hourlyRate?.toFixed(2) ?? "—"}
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
          {filteredEmployees.length === 0 && (
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
                ? `This will permanently delete ${selectedIds.size} employee(s).`
                : "This action cannot be undone."}
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDeleteConfirm(null)}>
              Cancel
            </Button>
            <Button
              variant="destructive"
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
          onClose={() => setShowCreateModal(false)}
          onCreate={handleCreate}
        />
      )}

      {showBulkEditModal && (
        <BulkEditModal
          count={selectedIds.size}
          onClose={() => setShowBulkEditModal(false)}
          onSave={() => setShowBulkEditModal(false)}
        />
      )}
    </div>
  );
}

function CreateEmployeeModal({
  onClose,
  onCreate,
}: {
  onClose: () => void;
  onCreate: (emp: Omit<Employee, "id" | "createdAt" | "updatedAt">) => void;
}) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [employeeNumber, setEmployeeNumber] = useState("");
  const [hourlyRate, setHourlyRate] = useState("");
  const [isActive, setIsActive] = useState(true);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const wage = hourlyRate
      ? {
          id: "w-new",
          employeeId: "",
          hourlyRate: parseFloat(hourlyRate),
          rateType: "Hourly" as const,
          effectiveFrom: new Date().toISOString().slice(0, 10),
        }
      : undefined;
    onCreate({
      firstName,
      lastName,
      email,
      employeeNumber,
      isActive,
      clockId: undefined,
      wageRate: wage,
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
            <Input
              value={employeeNumber}
              onChange={(e) => setEmployeeNumber(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label>Hourly rate ($)</Label>
            <Input
              type="number"
              step="0.01"
              min="0"
              value={hourlyRate}
              onChange={(e) => setHourlyRate(e.target.value)}
              placeholder="Optional"
            />
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
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit">Create</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function BulkEditModal({
  count,
  onClose,
  onSave,
}: {
  count: number;
  onClose: () => void;
  onSave: () => void;
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Bulk Edit ({count} employees)</DialogTitle>
          <DialogDescription>
            Bulk edit options (e.g. status, wage) will be available when connected
            to API.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
          <Button onClick={onSave}>Save</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
