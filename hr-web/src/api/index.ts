import axios from "axios";
import type {
  Employee,
  CreateEmployeeRequest,
  OnboardingStatus,
} from "../types";

const hrApi = axios.create({
  baseURL: "/api/hr",
  headers: { "Content-Type": "application/json" },
});

// ── Employees ─────────────────────────────────────────────────────────────────
export const getEmployees = async (): Promise<Employee[]> => {
  const { data } = await hrApi.get<Employee[]>("/employees");
  return data;
};

export const getEmployee = async (id: string): Promise<Employee> => {
  const { data } = await hrApi.get<Employee>(`/employees/${id}`);
  return data;
};

export const createEmployee = async (
  payload: CreateEmployeeRequest,
): Promise<Employee> => {
  const { data } = await hrApi.post<Employee>("/employees", payload);
  return data;
};

// ── Onboarding (via hr-svc BFF) ───────────────────────────────────────────────
export const getOnboardingStatus = async (
  employeeId: string,
): Promise<OnboardingStatus> => {
  const { data } = await hrApi.get<OnboardingStatus>(
    `/employees/${employeeId}/onboarding`,
  );
  return data;
};

// Opens an SSE connection to the streaming endpoint.
// Calls onUpdate with the new status string whenever the server pushes an event.
// Returns the EventSource so the caller can call .close() on cleanup.
export const subscribeToOnboarding = (
  employeeId: string,
  onUpdate: (status: string) => void,
  onError?: (err: Event) => void,
): EventSource => {
  const es = new EventSource(
    `/api/hr/employees/${employeeId}/onboarding/stream`,
  );
  es.onmessage = (e) => {
    try {
      const { status } = JSON.parse(e.data) as { status: string };
      onUpdate(status);
    } catch {
      // malformed SSE frame — skip
    }
  };
  if (onError) {
    es.onerror = onError;
  }
  return es; // caller must call es.close() to clean up
};

export const retryWorkflow = async (employeeId: string): Promise<void> => {
  await hrApi.post(`/employees/${employeeId}/onboarding/retry`);
};
