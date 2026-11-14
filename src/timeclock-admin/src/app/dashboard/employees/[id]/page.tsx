"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { ChevronLeft, ChevronRight, Clock, CreditCard, DollarSign } from "lucide-react";
import type { Employee, Location, RateType, WageRate } from "@/types/employee";
import {
  checkEmployeeNumber,
  clearEmployeeCard,
  clearEmployeePin,
  generateEmployeeNumber,
  getEmployee,
  listLocations,
  listWageHistory,
  setEmployeeCard,
  setEmployeePin,
  setEmployeeWage,
  updateEmployee,
} from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

export default function EmployeeDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const [employee, setEmployee] = useState<Employee | null>(null);
  const [locations, setLocations] = useState<Location[]>([]);
  const [history, setHistory] = useState<WageRate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [editingProfile, setEditingProfile] = useState(false);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [employeeNumber, setEmployeeNumber] = useState("");
  const [numberError, setNumberError] = useState<string | null>(null);
  const [locationId, setLocationId] = useState("");
  const [isActive, setIsActive] = useState(true);

  const [editingPay, setEditingPay] = useState(false);
  const [rateType, setRateType] = useState<RateType>("Hourly");
  const [payAmount, setPayAmount] = useState("");

  const [editingPin, setEditingPin] = useState(false);
  const [pin, setPin] = useState("");

  const [editingCard, setEditingCard] = useState(false);
  const [cardUid, setCardUid] = useState("");

  const load = async () => {
    const [emp, locationData, wageHistory] = await Promise.all([
      getEmployee(id),
      listLocations(),
      listWageHistory(id),
    ]);
    setEmployee(emp);
    setLocations(locationData);
    setHistory(wageHistory);
    setFirstName(emp.firstName);
    setLastName(emp.lastName);
    setEmail(emp.email);
    setEmployeeNumber(emp.employeeNumber);
    setLocationId(emp.locationId ?? "");
    setIsActive(emp.isActive);
    const type = emp.wageRate?.rateType ?? "Hourly";
    setRateType(type);
    setPayAmount(
      type === "Salary"
        ? emp.wageRate?.salaryAmount != null
          ? String(emp.wageRate.salaryAmount)
          : ""
        : emp.wageRate?.hourlyRate != null
          ? String(emp.wageRate.hourlyRate)
          : ""
    );
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        await load();
        if (!cancelled) setError(null);
      } catch {
        if (!cancelled) {
          setEmployee(null);
          setError("Employee not found.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const handleCheckNumber = async () => {
    if (!employee) return;
    const value = employeeNumber.trim();
    if (!value || value === employee.employeeNumber) {
      setNumberError(null);
      return;
    }
    try {
      const result = await checkEmployeeNumber(value, employee.id);
      setNumberError(result.available ? null : result.message);
    } catch (err) {
      setNumberError(err instanceof Error ? err.message : "Could not check the employee number.");
    }
  };

  const handleSaveProfile = async () => {
    if (!employee) return;
    try {
      setBusy(true);
      await updateEmployee(employee.id, {
        firstName,
        lastName,
        email,
        employeeNumber,
        isActive,
        locationId: locationId || null,
      });
      await load();
      setEditingProfile(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save employee.");
    } finally {
      setBusy(false);
    }
  };

  const handleSavePay = async () => {
    if (!employee) return;
    const amount = parseFloat(payAmount);
    if (Number.isNaN(amount) || amount < 0) {
      setError(rateType === "Salary" ? "Enter a salary amount." : "Enter an hourly rate.");
      return;
    }
    try {
      setBusy(true);
      await setEmployeeWage(employee.id, {
        rateType,
        hourlyRate: rateType === "Hourly" ? amount : undefined,
        salaryAmount: rateType === "Salary" ? amount : undefined,
      });
      await load();
      setEditingPay(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save pay.");
    } finally {
      setBusy(false);
    }
  };

  const handleSavePin = async () => {
    if (!employee) return;
    if (!/^[1-9]\d{3}$/.test(pin)) {
      setError("PIN must be 4 digits and cannot start with 0.");
      return;
    }
    try {
      setBusy(true);
      await setEmployeePin(employee.id, pin);
      await load();
      setPin("");
      setEditingPin(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to update PIN.");
    } finally {
      setBusy(false);
    }
  };

  const handleGenerateNumber = async () => {
    if (!employee) return;
    const confirmed = window.confirm(
      `Replace employee number ${employee.employeeNumber} with the next number starting at 1000? The employee will use that number to clock in or out without an NFC badge.`
    );
    if (!confirmed) return;
    try {
      setBusy(true);
      await generateEmployeeNumber(employee.id);
      await load();
      setEditingProfile(false);
      setPin("");
      setEditingPin(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate an employee number.");
    } finally {
      setBusy(false);
    }
  };

  const handleSaveCard = async () => {
    if (!employee) return;
    try {
      setBusy(true);
      await setEmployeeCard(employee.id, cardUid.trim());
      await load();
      setCardUid("");
      setEditingCard(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to save card.");
    } finally {
      setBusy(false);
    }
  };

  const handleClearCard = async () => {
    if (!employee) return;
    try {
      setBusy(true);
      await clearEmployeeCard(employee.id);
      await load();
      setEditingCard(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to clear card.");
    } finally {
      setBusy(false);
    }
  };

  const handleClearPin = async () => {
    if (!employee) return;
    try {
      setBusy(true);
      await clearEmployeePin(employee.id);
      await load();
      setPin("");
      setEditingPin(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to clear PIN.");
    } finally {
      setBusy(false);
    }
  };

  if (loading) {
    return (
      <div className="flex min-h-[400px] items-center justify-center p-8">
        <p className="text-muted-foreground">Loading employee...</p>
      </div>
    );
  }

  if (!employee) {
    return (
      <div className="flex min-h-[400px] items-center justify-center p-8">
        <p className="text-muted-foreground">{error ?? "Employee not found."}</p>
        <Button variant="link" asChild>
          <Link href="/dashboard/employees" className="ml-4">
            Back to Employees
          </Link>
        </Button>
      </div>
    );
  }

  const displayName = `${employee.firstName} ${employee.lastName}`;
  const payLabel =
    employee.wageRate?.rateType === "Salary"
      ? employee.wageRate.salaryAmount != null
        ? `Salary $${employee.wageRate.salaryAmount.toFixed(2)}`
        : "Salary"
      : employee.wageRate?.hourlyRate != null
        ? `$${employee.wageRate.hourlyRate.toFixed(2)}/hr`
        : "No rate";

  return (
    <div className="p-8">
      <div className="mb-6 flex items-center gap-2">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/dashboard/employees" title="Back to Employees">
            <ChevronLeft className="h-5 w-5" />
          </Link>
        </Button>
        <nav className="flex items-center gap-2 text-sm">
          <Link href="/dashboard/employees" className="text-muted-foreground hover:text-foreground">
            Employees
          </Link>
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium text-foreground">{displayName}</span>
        </nav>
      </div>

      {error && (
        <p className="mb-4 rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {error}
        </p>
      )}

      <div className="mb-6">
        <h2 className="text-xl font-semibold">{displayName}</h2>
        <p className="text-sm text-muted-foreground">
          {employee.employeeNumber}
          {employee.locationName ? ` · ${employee.locationName}` : ""}
        </p>
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

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle>Profile</CardTitle>
            {!editingProfile && (
              <Button variant="ghost" size="sm" onClick={() => setEditingProfile(true)}>
                Edit
              </Button>
            )}
          </CardHeader>
          <CardContent className="space-y-3 text-sm">
            {editingProfile ? (
              <div className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label>First name</Label>
                    <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label>Last name</Label>
                    <Input value={lastName} onChange={(e) => setLastName(e.target.value)} />
                  </div>
                </div>
                <div className="space-y-1">
                  <Label>Email</Label>
                  <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label>Employee number</Label>
                  <Input
                    value={employeeNumber}
                    onChange={(e) => {
                      setEmployeeNumber(e.target.value);
                      setNumberError(null);
                    }}
                    onBlur={handleCheckNumber}
                  />
                  {numberError && <p className="text-xs text-destructive">{numberError}</p>}
                </div>
                <div className="space-y-1">
                  <Label>Home location</Label>
                  <Select value={locationId || "none"} onValueChange={(v) => setLocationId(v === "none" ? "" : v)}>
                    <SelectTrigger>
                      <SelectValue placeholder="Unassigned" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Unassigned</SelectItem>
                      {locations.map((location) => (
                        <SelectItem key={location.id} value={location.id}>
                          {location.name}
                          {location.isActive ? "" : " (inactive)"}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex items-center gap-2">
                  <Checkbox id="active" checked={isActive} onCheckedChange={(c) => setIsActive(!!c)} />
                  <Label htmlFor="active">Active</Label>
                </div>
                <div className="flex gap-2">
                  <Button size="sm" onClick={handleSaveProfile} disabled={busy || !!numberError}>Save</Button>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={busy}
                    onClick={() => {
                      setEditingProfile(false);
                      setEmployeeNumber(employee.employeeNumber);
                      setNumberError(null);
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            ) : (
              <>
                <div>
                  <p className="text-muted-foreground">Email</p>
                  <p className="font-medium">{employee.email}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Employee number</p>
                  <div className="mt-1 flex items-center gap-3">
                    <p className="font-medium font-mono">{employee.employeeNumber}</p>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={handleGenerateNumber}
                      disabled={busy}
                    >
                      Generate
                    </Button>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    Generate saves the next 4-digit number, starting at 1000, and uses it as the clock PIN.
                    The employee enters that number to clock in or out without an NFC badge.
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Home location</p>
                  <p className="font-medium">{employee.locationName ?? "Unassigned"}</p>
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Clock className="h-5 w-5 text-muted-foreground" />
              Clock PIN
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="mb-3 text-sm text-muted-foreground">
              4-digit code for clocking in or out without an NFC badge. It cannot start with 0.
              Generating an employee number sets this PIN to that number. The clock also accepts the employee number itself.
            </p>
            {editingPin ? (
              <div className="flex flex-wrap items-center gap-2">
                <Input
                  value={pin}
                  onChange={(e) => setPin(e.target.value.replace(/\D/g, "").slice(0, 4))}
                  placeholder="1234"
                  maxLength={4}
                  className="w-24 font-mono"
                />
                <Button size="sm" onClick={handleSavePin} disabled={busy}>Save</Button>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => { setEditingPin(false); setPin(""); }}>
                  Cancel
                </Button>
              </div>
            ) : (
              <div className="flex items-center justify-between">
                <p className="font-semibold font-mono">{employee.pin || "No PIN"}</p>
                <div className="flex gap-2">
                  {employee.hasPinSet && (
                    <Button variant="ghost" size="sm" onClick={handleClearPin} disabled={busy}>Clear</Button>
                  )}
                  <Button variant="ghost" size="sm" onClick={() => setEditingPin(true)} disabled={busy}>
                    {employee.hasPinSet ? "Change" : "Set PIN"}
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CreditCard className="h-5 w-5 text-muted-foreground" />
              NFC Card
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="mb-3 text-sm text-muted-foreground">
              The card ID the time clock reads. Scan an unassigned card at the clock and it shows the ID to enter here.
            </p>
            {editingCard ? (
              <div className="flex flex-wrap items-center gap-2">
                <Input
                  value={cardUid}
                  onChange={(e) => setCardUid(e.target.value.toUpperCase())}
                  placeholder="04A1B2C3"
                  maxLength={32}
                  className="w-56 font-mono"
                />
                <Button size="sm" onClick={handleSaveCard} disabled={busy || !cardUid.trim()}>Save</Button>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => { setEditingCard(false); setCardUid(""); }}>
                  Cancel
                </Button>
              </div>
            ) : (
              <div className="flex items-center justify-between">
                <p className="font-semibold font-mono">{employee.cardUid || "No card"}</p>
                <div className="flex gap-2">
                  {employee.cardUid && (
                    <Button variant="ghost" size="sm" onClick={handleClearCard} disabled={busy}>Clear</Button>
                  )}
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => { setCardUid(employee.cardUid ?? ""); setEditingCard(true); }}
                    disabled={busy}
                  >
                    {employee.cardUid ? "Re-assign" : "Assign card"}
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between">
            <CardTitle className="flex items-center gap-2">
              <DollarSign className="h-5 w-5 text-muted-foreground" />
              Pay
            </CardTitle>
            {!editingPay && (
              <Button variant="ghost" size="sm" onClick={() => setEditingPay(true)}>Edit</Button>
            )}
          </CardHeader>
          <CardContent className="space-y-4">
            {editingPay ? (
              <div className="flex flex-wrap items-end gap-2">
                <div className="space-y-1">
                  <Label>Type</Label>
                  <Select value={rateType} onValueChange={(v) => setRateType(v as RateType)}>
                    <SelectTrigger className="w-32">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Hourly">Hourly</SelectItem>
                      <SelectItem value="Salary">Salary</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1">
                  <Label>{rateType === "Salary" ? "Salary amount" : "Hourly rate"}</Label>
                  <Input
                    type="number"
                    step="0.01"
                    min="0"
                    value={payAmount}
                    onChange={(e) => setPayAmount(e.target.value)}
                    className="w-36"
                  />
                </div>
                <Button size="sm" onClick={handleSavePay} disabled={busy}>Save</Button>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => setEditingPay(false)}>
                  Cancel
                </Button>
              </div>
            ) : (
              <p className="text-2xl font-semibold">{payLabel}</p>
            )}
            <p className="text-sm text-muted-foreground">
              {employee.wageRate?.rateType === "Salary"
                ? "Salary employees still punch. Hours show on the time grid. Pay is this salary, not hours times a rate."
                : "Changing this rate updates unverified days that were not given their own rate. Verified periods keep the rate they were locked with."}
            </p>
            {history.length > 0 && (
              <div className="space-y-1 text-sm">
                <p className="font-medium">Rate history</p>
                {history.map((row) => (
                  <p key={row.id} className="text-muted-foreground">
                    {new Date(row.effectiveFrom).toLocaleDateString()} –{" "}
                    {row.effectiveTo ? new Date(row.effectiveTo).toLocaleDateString() : "current"}
                    {": "}
                    {row.rateType === "Salary"
                      ? `Salary $${row.salaryAmount?.toFixed(2) ?? "—"}`
                      : `$${row.hourlyRate?.toFixed(2) ?? "—"}/hr`}
                  </p>
                ))}
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
