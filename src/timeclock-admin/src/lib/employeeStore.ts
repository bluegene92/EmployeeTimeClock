import type { Employee } from "@/types/employee";
import { MOCK_EMPLOYEES } from "./mockEmployees";

// Simple in-memory store so list and detail pages share data
let employees: Employee[] = [...MOCK_EMPLOYEES];

export function getEmployees(): Employee[] {
  return employees;
}

export function getEmployeeById(id: string): Employee | undefined {
  return employees.find((e) => e.id === id);
}

export function setEmployees(update: Employee[] | ((prev: Employee[]) => Employee[])) {
  employees = typeof update === "function" ? update(employees) : update;
  return employees;
}

export function addEmployee(emp: Omit<Employee, "id" | "createdAt" | "updatedAt">) {
  const now = new Date().toISOString();
  const newId = String(employees.length + 1);
  const newEmp: Employee = {
    ...emp,
    id: newId,
    clockId: emp.clockId,
    wageRate: emp.wageRate
      ? { ...emp.wageRate, id: emp.wageRate.id || `w-${newId}`, employeeId: newId }
      : undefined,
    createdAt: now,
    updatedAt: now,
  };
  employees = [...employees, newEmp];
  return newEmp;
}

export function updateEmployee(id: string, updates: Partial<Employee>) {
  const idx = employees.findIndex((e) => e.id === id);
  if (idx < 0) return undefined;
  const updated = { ...employees[idx], ...updates, updatedAt: new Date().toISOString() };
  employees = [...employees.slice(0, idx), updated, ...employees.slice(idx + 1)];
  return updated;
}

export function deleteEmployee(id: string) {
  employees = employees.filter((e) => e.id !== id);
}
