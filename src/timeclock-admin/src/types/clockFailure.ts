import type { PunchType } from "./timePunch";

export type ClockFailureReason = "UnknownCard" | "PunchRejected" | (string & {});

export interface ClockFailure {
  id: string;
  reason: ClockFailureReason;
  message: string;
  cardUid?: string | null;
  employeeId?: string | null;
  employeeFirstName?: string | null;
  employeeLastName?: string | null;
  punchType?: PunchType | null;
  /** Local wall-clock time of the scan. Null when the clock had not set its time. */
  occurredAt?: string | null;
  locationId?: string | null;
  locationName?: string | null;
  source?: string | null;
  deviceId?: string | null;
  receivedAt: string;
  resolvedAt?: string | null;
}
