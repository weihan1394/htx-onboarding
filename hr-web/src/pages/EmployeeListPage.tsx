import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Users,
  Plus,
  RefreshCw,
  ArrowRight,
  UserCheck,
  UserX,
} from "lucide-react";
import { getEmployees } from "../api";
import type { Employee } from "../types";
import StatusBadge from "../components/StatusBadge";

// avatar colours cycled per employee
const AVATAR_COLORS = [
  "bg-indigo-100 text-indigo-700",
  "bg-violet-100 text-violet-700",
  "bg-rose-100 text-rose-700",
  "bg-amber-100 text-amber-700",
  "bg-teal-100 text-teal-700",
  "bg-sky-100 text-sky-700",
];

// deterministic colour per employee — same id always maps to the same colour
function avatarColor(id: string) {
  const sum = id.split("").reduce((a, c) => a + c.charCodeAt(0), 0);
  return AVATAR_COLORS[sum % AVATAR_COLORS.length];
}

export default function EmployeeListPage() {
  // --- page state ---
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // also called manually via the Refresh button
  const fetchEmployees = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await getEmployees();
      setEmployees(data);
    } catch {
      setError("Failed to load employees. Make sure hr-svc is running.");
    } finally {
      setLoading(false);
    }
  };

  // fetch on mount
  useEffect(() => {
    fetchEmployees();
  }, []);

  // derived counts — no extra API call needed
  const active = employees.filter((e) => e.status === "active").length;
  const inactive = employees.length - active;

  // ─── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="max-w-6xl mx-auto px-6 py-8">
      {/* ── Header ── */}
      <div className="flex items-start justify-between mb-8">
        <div>
          <h1 className="text-2xl font-bold text-slate-900">Employees</h1>
          <p className="text-sm text-slate-500 mt-0.5">
            Manage your workforce and onboarding
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={fetchEmployees} className="btn-outline">
            <RefreshCw className="w-3.5 h-3.5" />
            Refresh
          </button>
          <Link to="/employees/new" className="btn-primary">
            <Plus className="w-4 h-4" />
            Add Employee
          </Link>
        </div>
      </div>

      {/* ── Stats cards ── hidden during loading to avoid showing 0s */}
      {!loading && !error && (
        <div className="grid grid-cols-3 gap-4 mb-6">
          {[
            {
              label: "Total Employees",
              value: employees.length,
              icon: <Users className="w-5 h-5 text-slate-400" />,
              color: "text-slate-900",
            },
            {
              label: "Active",
              value: active,
              icon: <UserCheck className="w-5 h-5 text-emerald-500" />,
              color: "text-emerald-700",
            },
            {
              label: "Inactive",
              value: inactive,
              icon: <UserX className="w-5 h-5 text-slate-400" />,
              color: "text-slate-500",
            },
          ].map(({ label, value, icon, color }) => (
            <div key={label} className="card p-4 flex items-center gap-4">
              <div className="w-10 h-10 bg-slate-50 rounded-lg flex items-center justify-center flex-shrink-0">
                {icon}
              </div>
              <div>
                <p className={`text-2xl font-bold ${color}`}>{value}</p>
                <p className="text-xs text-slate-500 mt-0.5">{label}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Error banner ── */}
      {error && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-xl text-red-700 text-sm">
          {error}
        </div>
      )}

      {/* ── Table / loading ── */}
      {loading ? (
        <div className="flex flex-col items-center justify-center py-24 gap-3">
          <div className="animate-spin w-8 h-8 border-[3px] border-indigo-600 border-t-transparent rounded-full" />
          <p className="text-sm text-slate-400">Loading employees...</p>
        </div>
      ) : (
        <div className="card overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200 bg-slate-50/80">
                <th className="text-left px-6 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Employee
                </th>
                <th className="text-left px-4 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Emp No.
                </th>
                <th className="text-left px-4 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Department
                </th>
                <th className="text-left px-4 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Position
                </th>
                <th className="text-left px-4 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Hire Date
                </th>
                <th className="text-left px-4 py-3.5 text-xs font-semibold text-slate-500 uppercase tracking-wide">
                  Status
                </th>
                <th className="px-4 py-3.5" />
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {/* ── Empty state ── */}
              {employees.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-6 py-16 text-center">
                    <div className="flex flex-col items-center gap-3">
                      <div className="w-12 h-12 bg-slate-100 rounded-full flex items-center justify-center">
                        <Users className="w-6 h-6 text-slate-400" />
                      </div>
                      <div>
                        <p className="font-medium text-slate-600">
                          No employees yet
                        </p>
                        <p className="text-sm text-slate-400 mt-0.5">
                          Add your first employee to get started
                        </p>
                      </div>
                      <Link to="/employees/new" className="btn-primary mt-1">
                        <Plus className="w-3.5 h-3.5" /> Add Employee
                      </Link>
                    </div>
                  </td>
                </tr>
              ) : (
                employees.map((emp) => (
                  <tr
                    key={emp.employeeId}
                    className="hover:bg-slate-50/60 transition-colors"
                  >
                    {/* initials avatar + name + email */}
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div
                          className={`w-9 h-9 rounded-xl flex items-center justify-center font-semibold text-xs flex-shrink-0 ${avatarColor(emp.employeeId)}`}
                        >
                          {emp.firstName[0]}
                          {emp.lastName[0]}
                        </div>
                        <div>
                          <p className="font-medium text-slate-900">
                            {emp.fullName}
                          </p>
                          <p className="text-slate-400 text-xs mt-0.5">
                            {emp.email}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-4">
                      <span className="font-mono text-xs bg-slate-100 px-2 py-1 rounded text-slate-600">
                        {emp.employeeNumber}
                      </span>
                    </td>
                    {/* null → em-dash instead of blank */}
                    <td className="px-4 py-4 text-slate-600 text-sm">
                      {emp.department ?? (
                        <span className="text-slate-300">—</span>
                      )}
                    </td>
                    <td className="px-4 py-4 text-slate-600 text-sm">
                      {emp.position ?? (
                        <span className="text-slate-300">—</span>
                      )}
                    </td>
                    <td className="px-4 py-4 text-slate-500 text-sm">
                      {emp.hireDate}
                    </td>
                    <td className="px-4 py-4">
                      <StatusBadge status={emp.status} />
                    </td>
                    {/* link to live workflow status page */}
                    <td className="px-4 py-4 text-right">
                      <Link
                        to={`/onboarding/${emp.employeeId}`}
                        className="inline-flex items-center gap-1 px-3 py-1.5 text-xs font-medium text-indigo-600 bg-indigo-50 rounded-lg hover:bg-indigo-100 transition"
                      >
                        Onboarding <ArrowRight className="w-3 h-3" />
                      </Link>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
