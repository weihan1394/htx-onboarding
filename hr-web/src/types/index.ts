export interface Employee {
  employeeId: string;
  employeeNumber: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  department: string | null;
  position: string | null;
  hireDate: string;
  status: "active" | "inactive";
  createdAt: string;
}

export interface CreateEmployeeRequest {
  firstName: string;
  lastName: string;
  email: string;
  department: string | null;
  position: string | null;
  hireDate: string;
}

export interface OnboardingStatus {
  onboardingId: string;
  employeeId: string;
  status: "pending" | "in_progress" | "completed" | "failed";
  startedAt: string | null;
  completedAt: string | null;
  createdAt: string;
  retryCount: number;
  accountTasks: AccountTask[];
  equipmentTasks: EquipmentTask[];
  retryHistory: RetryHistoryEntry[];
}

export interface RetryHistoryEntry {
  historyId: string;
  attempt: number;
  status: "completed" | "failed";
  attemptedAt: string;
  errorMessage: string | null;
}

export interface AccountTask {
  taskId: string;
  accountType: string;
  username: string | null;
  status: "pending" | "completed" | "failed";
  errorMessage: string | null;
  completedAt: string | null;
}

export interface EquipmentTask {
  taskId: string;
  itemType: string;
  itemDetails: string | null;
  status: "pending" | "issued" | "failed";
  errorMessage: string | null;
  issuedAt: string | null;
}
