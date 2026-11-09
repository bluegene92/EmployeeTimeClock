export type PunchType = "In" | "Out";

export interface TimePunch {
  id: string;
  employeeId: string;
  employeeFirstName?: string;
  employeeLastName?: string;
  employeeIsDeleted?: boolean;
  locationId?: string | null;
  locationName?: string | null;
  punchType: PunchType;
  punchedAt: string;
  source?: string;
  deviceId?: string | null;
}
