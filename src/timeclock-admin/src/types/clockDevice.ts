export interface ClockDevice {
  deviceId: string;
  locationId?: string | null;
  locationName?: string | null;
  employeeListVersion?: string | null;
  upToDate: boolean;
  online: boolean;
  lastSeenAt: string;
  secondsSinceSeen: number;
  pendingPunches: number;
  pendingFailures: number;
  firmwareVersion?: string | null;
  wifiRssi?: number | null;
  bootedAt?: string | null;
  firstSeenAt: string;
  /** Includes an outage still in progress. */
  downtime24hSeconds: number;
  downtime7dSeconds: number;
  outages7d: number;
}

export interface ClockDevicesStatus {
  employeeListVersion: string;
  devices: ClockDevice[];
}

export interface ClockOutage {
  startedAt: string;
  /** Null while the clock is still offline. */
  endedAt?: string | null;
  durationSeconds: number;
  restarted: boolean;
}
