"use client";

import { useState, useEffect, useRef } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ChevronLeft,
  ChevronRight,
  UserCircle,
  Camera,
  Clock,
  DollarSign,
} from "lucide-react";
import { getEmployeeById, updateEmployee } from "@/lib/employeeStore";
import type { Employee } from "@/types/employee";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export default function EmployeeDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const [employee, setEmployee] = useState<Employee | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [hourlyRate, setHourlyRate] = useState("");
  const [clockId, setClockId] = useState("");
  const [isEditingClockId, setIsEditingClockId] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const emp = getEmployeeById(id);
    setEmployee(emp ?? null);
    if (emp?.wageRate) setHourlyRate(String(emp.wageRate.hourlyRate));
    if (emp?.clockId) setClockId(emp.clockId);
  }, [id]);

  const handlePhotoUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !employee) return;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = reader.result as string;
      updateEmployee(employee.id, { photoUrl: dataUrl });
      setEmployee({ ...employee, photoUrl: dataUrl });
    };
    reader.readAsDataURL(file);
  };

  const handleSaveWage = () => {
    if (!employee) return;
    const rate = parseFloat(hourlyRate);
    if (!isNaN(rate) && rate >= 0) {
      const existing = employee.wageRate;
      const wageRate = {
        id: existing?.id ?? "w",
        employeeId: employee.id,
        hourlyRate: rate,
        rateType: "Hourly" as const,
        effectiveFrom:
          existing?.effectiveFrom ?? new Date().toISOString().slice(0, 10),
        effectiveTo: existing?.effectiveTo,
      };
      updateEmployee(employee.id, { wageRate });
      setEmployee({ ...employee, wageRate });
    }
    setIsEditing(false);
  };

  const isValidClockId = (val: string) => /^\d{4}$/.test(val);

  const handleSaveClockId = () => {
    if (!employee) return;
    const trimmed = clockId.trim();
    if (trimmed === "") {
      updateEmployee(employee.id, { clockId: undefined });
      setEmployee({ ...employee, clockId: undefined });
    } else if (isValidClockId(trimmed)) {
      updateEmployee(employee.id, { clockId: trimmed });
      setEmployee({ ...employee, clockId: trimmed });
    }
    setIsEditingClockId(false);
  };

  if (!employee) {
    return (
      <div className="flex min-h-[400px] items-center justify-center p-8">
        <p className="text-muted-foreground">Employee not found.</p>
        <Button variant="link" asChild>
          <Link href="/dashboard/employees" className="ml-4">
            Back to Employees
          </Link>
        </Button>
      </div>
    );
  }

  const displayName = `${employee.firstName} ${employee.lastName}`;

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center gap-2">
        <Button variant="ghost" size="icon" asChild>
          <Link
            href="/dashboard/employees"
            title="Back to Employees"
          >
            <ChevronLeft className="h-5 w-5" />
          </Link>
        </Button>
        <nav className="flex items-center gap-2 text-sm">
          <Link
            href="/dashboard/employees"
            className="text-muted-foreground hover:text-foreground"
          >
            Employees
          </Link>
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium text-foreground">{displayName}</span>
        </nav>
      </div>

      <div className="flex flex-col gap-8 lg:flex-row lg:items-start">
        <div className="shrink-0">
          <div className="group relative">
            <Button
              variant="outline"
              className="h-auto border-dashed p-2"
              onClick={() => fileInputRef.current?.click()}
            >
              {employee.photoUrl ? (
                <img
                  src={employee.photoUrl}
                  alt={displayName}
                  className="h-32 w-32 rounded-md object-cover"
                />
              ) : (
                <div className="flex h-32 w-32 flex-col items-center justify-center rounded-md bg-muted text-muted-foreground">
                  <UserCircle className="h-12 w-12" />
                  <span className="mt-1 text-xs">Upload photo</span>
                </div>
              )}
            </Button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/*"
              onChange={handlePhotoUpload}
              className="hidden"
            />
            <div className="absolute bottom-2 right-2 rounded-md bg-primary p-1.5 text-primary-foreground opacity-0 shadow-lg transition-opacity group-hover:opacity-100">
              <Camera className="h-4 w-4" />
            </div>
          </div>
          <div className="mt-4">
            <h2 className="text-xl font-semibold">{displayName}</h2>
            <p className="text-sm text-muted-foreground">{employee.employeeNumber}</p>
            <span
              className={cn(
                "mt-2 inline-block rounded-md px-2.5 py-0.5 text-xs font-medium",
                employee.isActive
                  ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400"
                  : "bg-muted text-muted-foreground"
              )}
            >
              {employee.isActive ? "Active" : "Inactive"}
            </span>
          </div>
        </div>

        <div className="flex-1 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Contact</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div>
                <p className="text-muted-foreground">Email</p>
                <p className="font-medium">{employee.email}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Employee number</p>
                <p className="font-medium">{employee.employeeNumber}</p>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Clock className="h-5 w-5 text-muted-foreground" />
                Clock ID
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="mb-3 text-sm text-muted-foreground">
                Unique 4-digit ID the employee uses to clock in and out.
              </p>
              {isEditingClockId ? (
                <div className="flex items-center gap-2">
                  <Input
                    value={clockId}
                    onChange={(e) => {
                      const v = e.target.value.replace(/\D/g, "").slice(0, 4);
                      setClockId(v);
                    }}
                    placeholder="0000"
                    maxLength={4}
                    className="w-24 font-mono"
                  />
                  <Button size="sm" onClick={handleSaveClockId}>
                    Save
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setIsEditingClockId(false);
                      setClockId(employee.clockId ?? "");
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              ) : (
                <div className="flex items-center justify-between">
                  <p className="font-mono text-xl font-semibold">
                    {employee.clockId ?? "—"}
                  </p>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setIsEditingClockId(true)}
                  >
                    {employee.clockId ? "Edit" : "Assign"}
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <DollarSign className="h-5 w-5 text-muted-foreground" />
                Hourly Wage
              </CardTitle>
            </CardHeader>
            <CardContent>
              {isEditing ? (
                <div className="flex items-center gap-2">
                  <span className="text-muted-foreground">$</span>
                  <Input
                    type="number"
                    step="0.01"
                    min="0"
                    value={hourlyRate}
                    onChange={(e) => setHourlyRate(e.target.value)}
                    className="w-32"
                  />
                  <Button size="sm" onClick={handleSaveWage}>
                    Save
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                      setIsEditing(false);
                      setHourlyRate(
                        employee.wageRate
                          ? String(employee.wageRate.hourlyRate)
                          : ""
                      );
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              ) : (
                <div className="flex items-center justify-between">
                  <p className="text-2xl font-semibold">
                    ${employee.wageRate?.hourlyRate?.toFixed(2) ?? "—"}
                  </p>
                  <Button variant="ghost" size="sm" onClick={() => setIsEditing(true)}>
                    Edit
                  </Button>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
