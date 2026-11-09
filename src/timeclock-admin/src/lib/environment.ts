export const ENVIRONMENT_KEY = "timeclock-environment";

export type AppEnvironment = "production" | "test";

export function getAppEnvironment(): AppEnvironment {
  if (typeof window === "undefined") return "production";
  return window.localStorage.getItem(ENVIRONMENT_KEY) === "test" ? "test" : "production";
}

export function setAppEnvironment(environment: AppEnvironment) {
  window.localStorage.setItem(ENVIRONMENT_KEY, environment);
  window.location.reload();
}
