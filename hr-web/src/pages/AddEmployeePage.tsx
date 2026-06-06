import { useState, useMemo } from "react";
import { useNavigate, Link } from "react-router-dom";
import { ArrowLeft, UserPlus, Zap } from "lucide-react";
import { createEmployee } from "../api";

// valid departments — single source of truth for the form dropdown
const DEPARTMENTS = [
  "Engineering",
  "Human Resources",
  "Operations",
  "Cybersecurity",
  "Finance",
  "Legal",
];

export default function AddEmployeePage() {
  const navigate = useNavigate();
  const today = useMemo(() => new Date().toISOString().split("T")[0], []);

  // --- page state ---
  const [submitting, setSubmitting] = useState(false); // true while API call is in-flight
  const [error, setError] = useState<string | null>(null); // API error to display

  // --- form state ---
  // emailPrefix = part before "@htx.gov.sg"; domain is appended on submit
  // hireDate defaults to today; computed inside useState so it's captured once at mount
  const [form, setForm] = useState(() => ({
    firstName: "",
    lastName: "",
    emailPrefix: "",
    department: "",
    position: "",
    hireDate: new Date().toISOString().split("T")[0],
  }));

  // generic change handler — works for both <input> and <select>
  // uses element's `name` attribute to update the matching key in form state
  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>,
  ) => {
    setForm((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  // ─── handleSubmit ────────────────────────────────────────────────────────────
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    try {
      // Step 1: create the employee record in hr-svc
      // full email assembled here — API expects a complete address
      const employee = await createEmployee({
        firstName: form.firstName,
        lastName: form.lastName,
        email: form.emailPrefix + "@htx.gov.sg",
        department: form.department || null,
        position: form.position || null,
        hireDate: form.hireDate,
      });

      // Step 2: navigate to onboarding detail page
      // Temporal workflow is triggered server-side on creation, already running by now
      navigate(`/onboarding/${employee.employeeId}`);
    } catch (err: unknown) {
      // prefer the API's error message; fall back to generic string
      const msg = (err as { response?: { data?: { message?: string } } })
        ?.response?.data?.message;
      setError(msg ?? "Failed to create employee. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  // ─── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="max-w-2xl mx-auto px-6 py-8">
      {/* ── Header ── */}
      <div className="flex items-center gap-3 mb-8">
        <Link to="/" className="btn-icon">
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div>
          <h1 className="text-xl font-bold text-slate-900">Add New Employee</h1>
          <p className="text-sm text-slate-500 mt-0.5">
            The onboarding workflow will trigger automatically on creation
          </p>
        </div>
      </div>

      {/* ── Error banner ── */}
      {error && (
        <div className="mb-5 p-4 bg-red-50 border border-red-200 rounded-xl text-red-700 text-sm">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="card overflow-hidden">
        {/* ── Section 1: Personal Details ── name + work email */}
        <div className="px-6 py-5 border-b border-slate-100">
          <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-4">
            Personal Details
          </h2>
          <div className="grid grid-cols-2 gap-4 mb-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                First Name <span className="text-red-400">*</span>
              </label>
              <input
                name="firstName"
                value={form.firstName}
                onChange={handleChange}
                required
                className="form-input"
                placeholder="Alice"
              />
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                Last Name <span className="text-red-400">*</span>
              </label>
              <input
                name="lastName"
                value={form.lastName}
                onChange={handleChange}
                required
                className="form-input"
                placeholder="Tan"
              />
            </div>
          </div>
          {/* split email input — user types prefix only, domain is fixed */}
          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Work Email <span className="text-red-400">*</span>
            </label>
            <div className="flex items-stretch border border-slate-300 rounded-lg focus-within:ring-2 focus-within:ring-indigo-500 focus-within:border-indigo-500 overflow-hidden transition">
              <input
                name="emailPrefix"
                type="text"
                value={form.emailPrefix}
                onChange={handleChange}
                required
                className="flex-1 px-3.5 py-2.5 text-sm text-slate-900 placeholder:text-slate-400 focus:outline-none"
                placeholder="alice.tan"
              />
              <span className="px-3.5 py-2.5 text-sm text-slate-500 bg-slate-50 border-l border-slate-300 select-none whitespace-nowrap">
                @htx.gov.sg
              </span>
            </div>
          </div>
        </div>

        {/* ── Section 2: Employment Details ── department, position, hire date */}
        <div className="px-6 py-5">
          <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wide mb-4">
            Employment Details
          </h2>
          <div className="grid grid-cols-2 gap-4 mb-4">
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                Department
              </label>
              <select
                name="department"
                value={form.department}
                onChange={handleChange}
                className="form-input"
              >
                <option value="">Select department</option>
                {DEPARTMENTS.map((d) => (
                  <option key={d} value={d}>
                    {d}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-slate-700 mb-1.5">
                Position
              </label>
              <input
                name="position"
                value={form.position}
                onChange={handleChange}
                className="form-input"
                placeholder="Software Engineer"
              />
            </div>
          </div>
          {/* min={today} prevents past dates */}
          <div>
            <label className="block text-xs font-medium text-slate-700 mb-1.5">
              Hire Date <span className="text-red-400">*</span>
            </label>
            <input
              name="hireDate"
              type="date"
              value={form.hireDate}
              onChange={handleChange}
              required
              min={today}
              className="form-input"
            />
          </div>
        </div>

        {/* ── Actions ── cancel + submit */}
        <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex items-center gap-3">
          <Link to="/" className="btn-outline flex-1 font-medium">
            Cancel
          </Link>
          <button
            type="submit"
            disabled={submitting}
            className="btn-primary flex-1"
          >
            {submitting ? (
              // spinner while API call is in-flight
              <>
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
                Creating...
              </>
            ) : (
              <>
                <UserPlus className="w-4 h-4" /> Create Employee
              </>
            )}
          </button>
        </div>
      </form>

      {/* account + equipment setup happens automatically */}
      <div className="mt-4 flex items-center gap-2 text-xs text-slate-400 justify-center">
        <Zap className="w-3.5 h-3.5 text-amber-400" />
        Account setup, equipment issuance, and meetings are scheduled
        automatically.
      </div>
    </div>
  );
}
