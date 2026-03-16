export type PunchType = "In" | "Out";

export interface TimePunch {
  id: string;
  employeeId: string;
  punchType: PunchType;
  punchedAt: string; // ISO datetime
  source?: string;
}
