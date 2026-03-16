export interface Employee {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  employeeNumber: string;
  isActive: boolean;
  photoUrl?: string;
  /** Unique 4-digit ID for clock in/out */
  clockId?: string;
  wageRate?: WageRate;
  createdAt: string;
  updatedAt: string;
}

export interface WageRate {
  id: string;
  employeeId: string;
  hourlyRate: number;
  rateType: "Hourly" | "Salary";
  effectiveFrom: string;
  effectiveTo?: string;
}
