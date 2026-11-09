export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  employeeNumber: string;
  isActive: boolean;
  isDeleted?: boolean;
  pin?: string | null;
  hasPinSet?: boolean;
  locationId?: string | null;
  locationName?: string | null;
  wageRate?: WageRate;
  createdAt: string;
  updatedAt: string;
}

export type RateType = "Hourly" | "Salary";

export interface WageRate {
  id: string;
  employeeId: string;
  hourlyRate: number | null;
  salaryAmount: number | null;
  rateType: RateType;
  effectiveFrom: string;
  effectiveTo?: string | null;
}

export interface Location {
  id: string;
  name: string;
  isActive: boolean;
}

export interface ResolvedDayRate {
  employeeId: string;
  workDate: string;
  hourlyRate: number | null;
  source: "Current" | "Override" | "Frozen";
  isVerified: boolean;
  rateType: RateType;
}

export interface PayPeriodSettings {
  anchorDate: string;
  updatedAt: string;
}
