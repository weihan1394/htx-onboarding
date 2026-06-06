import { useEffect, useRef, useState } from "react";
import { useParams, Link } from "react-router-dom";
import {
  ArrowLeft,
  RefreshCw,
  Monitor,
  Key,
  CheckCircle,
  XCircle,
  Clock,
  AlertTriangle,
} from "lucide-react";
import {
  getEmployee,
  getOnboardingStatus,
  retryWorkflow,
  subscribeToOnboarding,
} from "../api";
import type { Employee, OnboardingStatus, RetryHistoryEntry } from "../types";
import StatusBadge from "../components/StatusBadge";

export default function OnboardingDetailPage() {
  // employeeId comes from the route: /onboarding/:employeeId
  const { employeeId } = useParams<{ employeeId: string }>();

  // --- page state ---
  const [employee, setEmployee] = useState<Employee | null>(null); // employee profile from hr-svc
  const [onboarding, setOnboarding] = useState<OnboardingStatus | null>(null); // live workflow status from onboarding-svc
  const [loading, setLoading] = useState(true); // true on first load only
  const [error, setError] = useState<string | null>(null); // service-level error message
  const [retrying, setRetrying] = useState(false); // true while retry API call is in-flight
  const retryTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (retryTimeoutRef.current !== null) {
        clearTimeout(retryTimeoutRef.current);
      }
    };
  }, []);

  // ─── handleRetry ────────────────────────────────────────────────────────────
  // sends a signal to onboarding-svc to re-trigger the Temporal workflow
  const handleRetry = async () => {
    if (!employeeId) {
      return;
    }
    try {
      setRetrying(true);
      setError(null);
      await retryWorkflow(employeeId);
      // SSE may have been disconnected (e.g. after a postgres reset) and could miss the
      // completion event while it reconnects. Poll once after 3 s as a fallback.
      retryTimeoutRef.current = setTimeout(() => fetchData(), 3000);
    } catch {
      setError("Workflow Service is unavailable — cannot retry workflow.");
    } finally {
      setRetrying(false);
    }
  };

  // ─── fetchData ───────────────────────────────────────────────────────────────
  // full page load or manual refresh — fetches employee + onboarding in sequence
  const fetchData = async (signal?: AbortSignal) => {
    if (!employeeId) {
      return;
    }
    setLoading(true);
    setError(null);

    let emp = null;
    let ob = null;

    // Step 1: fetch employee profile from hr-svc
    // bail early if hr-svc is down
    try {
      emp = await getEmployee(employeeId);
    } catch (err: unknown) {
      if (signal?.aborted) {
        return;
      }
      const status = (err as { response?: { status?: number } })?.response?.status;
      setError(
        status === 404
          ? "Employee not found."
          : "HR Service is unavailable — cannot load employee details.",
      );
      setLoading(false);
      return;
    }

    // Step 2: fetch onboarding status from onboarding-svc
    // retry on 404 up to 5x — race between navigation and workflow writing its first record
    for (let attempt = 0; attempt < 5; attempt++) {
      if (signal?.aborted) {
        return;
      }
      try {
        ob = await getOnboardingStatus(employeeId);
        break; // got a record — exit the retry loop
      } catch (err: unknown) {
        const status = (err as { response?: { status?: number } })?.response
          ?.status;

        // 404 → record doesn't exist yet, wait and retry
        if (status === 404 && attempt < 4) {
          await new Promise((r) => setTimeout(r, 5000));
          continue;
        }

        // any other error → service unavailable
        if (status !== 404) {
          setError(
            "Onboarding Service is unavailable — cannot load onboarding status.",
          );
        }
        break;
      }
    }

    if (!signal?.aborted) {
      setEmployee(emp);
      setOnboarding(ob);
      setLoading(false);
    }
  };

  // ─── Initial load ────────────────────────────────────────────────────────────
  // runs once on mount — fetches employee + onboarding, then sets loading=false
  useEffect(() => {
    const controller = new AbortController();
    fetchData(controller.signal);
    return () => controller.abort();
  }, [employeeId]);

  // ─── Live updates via SSE ─────────────────────────────────────────────────-──
  useEffect(() => {
    if (!employeeId) {
      return;
    }

    const es = subscribeToOnboarding(
      employeeId,
      async (status) => {
        if (status === "not_started") {
          return;
        }
        try {
          const ob = await getOnboardingStatus(employeeId);
          setOnboarding(ob);
        } catch {
          setError("Onboarding Service is unavailable — live updates stopped.");
        }
      },
      () => setError("Live updates disconnected — refresh to reconnect."),
    );

    return () => es.close();
  }, [employeeId]);

  // ─── Loading screen ──────────────────────────────────────────────────────────
  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center py-24 gap-3">
        <div className="animate-spin w-8 h-8 border-[3px] border-indigo-600 border-t-transparent rounded-full" />
        <p className="text-sm text-slate-400">Loading onboarding details...</p>
      </div>
    );
  }

  // show Retry when failed, in_progress (could be stuck), pending (never started), or no record yet
  const showRetry =
    !onboarding ||
    onboarding.status === "failed" ||
    onboarding.status === "in_progress" ||
    onboarding.status === "pending";

  // ─── Render ──────────────────────────────────────────────────────────────────
  return (
    <div className="max-w-6xl mx-auto px-6 py-8">
      {/* ── Header ── back button, employee name + ID, status badge, retry/refresh */}
      <div className="flex items-start gap-4 mb-8">
        <Link to="/" className="mt-1 btn-icon flex-shrink-0">
          <ArrowLeft className="w-5 h-5" />
        </Link>
        <div className="flex-1 min-w-0">
          <h1 className="text-2xl font-bold text-slate-900 truncate">
            {employee
              ? `${employee.fullName}'s Onboarding`
              : "Onboarding Details"}
          </h1>
          {employee && (
            <p className="text-sm text-slate-400 mt-1 flex items-center gap-2">
              <span className="font-mono bg-slate-100 px-1.5 py-0.5 rounded text-xs text-slate-600">
                {employee.employeeNumber}
              </span>
              <span>{employee.email}</span>
            </p>
          )}
        </div>
        <div className="flex items-center gap-2 flex-shrink-0">
          {onboarding && <StatusBadge status={onboarding.status} />}
          {showRetry && (
            <button
              onClick={handleRetry}
              disabled={retrying}
              className="btn-primary"
            >
              {retrying
                ? onboarding?.status === "pending"
                  ? "Starting..."
                  : "Retrying..."
                : onboarding?.status === "pending"
                  ? "Start Workflow"
                  : "Retry Workflow"}
            </button>
          )}
          <button onClick={() => fetchData()} className="btn-icon">
            <RefreshCw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* ── Error banner ── */}
      {error && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-xl text-red-700 text-sm flex items-start gap-2">
          <AlertTriangle className="w-4 h-4 flex-shrink-0 mt-0.5" />
          {error}
        </div>
      )}

      {/* ── Not started ── onboarding record doesn't exist yet */}
      {!onboarding && !error && (
        <div className="p-6 bg-amber-50 border border-amber-200 rounded-xl text-amber-800 text-sm">
          Onboarding has not started yet — the workflow service may have been
          unavailable when this employee was created. Click{" "}
          <strong>Retry Workflow</strong> to begin onboarding.
        </div>
      )}

      {/* ── Main content ── only rendered once onboarding record exists */}
      {onboarding && (
        <div className="space-y-5">
          {/* two-column layout: task cards left (2/3), sidebar right (1/3) */}
          <div className="grid grid-cols-3 gap-5 items-start">
            {/* ── Left column: task sections ── */}
            <div className="col-span-2 space-y-5">
              {/*
               * both task groups share the same card layout
               * defined as data and rendered with a single .map()
               */}
              {[
                {
                  icon: <Key className="w-4 h-4 text-indigo-600" />,
                  title: "Account Creation",
                  sub: "Email, VPN, and HR portal accounts",
                  empty: "No account tasks yet.",
                  items: (onboarding.accountTasks ?? []).map((task) => ({
                    key: task.taskId,
                    statusIcon:
                      task.status === "completed" ? (
                        <CheckCircle className="w-4 h-4 text-emerald-500 flex-shrink-0" />
                      ) : task.status === "failed" ? (
                        <XCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                      ) : (
                        <Clock className="w-4 h-4 text-amber-400 flex-shrink-0" />
                      ),
                    primary: task.accountType.replace(/_/g, " "), // "hr_portal" → "hr portal"
                    secondary: task.username,
                    error: task.errorMessage,
                    status: task.status,
                  })),
                },
                {
                  icon: <Monitor className="w-4 h-4 text-indigo-600" />,
                  title: "Equipment Issuance",
                  sub: "Laptop, staff pass, and welcome kit",
                  empty: "No equipment tasks yet.",
                  items: (onboarding.equipmentTasks ?? []).map((task) => ({
                    key: task.taskId,
                    // equipment uses "issued" not "completed" as the success status
                    statusIcon:
                      task.status === "issued" ? (
                        <CheckCircle className="w-4 h-4 text-emerald-500 flex-shrink-0" />
                      ) : task.status === "failed" ? (
                        <XCircle className="w-4 h-4 text-red-500 flex-shrink-0" />
                      ) : (
                        <Clock className="w-4 h-4 text-amber-400 flex-shrink-0" />
                      ),
                    primary: task.itemType.replace(/_/g, " "), // "staff_pass" → "staff pass"
                    secondary: null,
                    error: task.errorMessage,
                    status: task.status,
                  })),
                },
              ].map(({ icon, title, sub, empty, items }) => (
                <div key={title} className="card overflow-hidden">
                  {/* card header */}
                  <div className="flex items-center gap-3 px-5 py-4 border-b border-slate-100">
                    <div className="w-8 h-8 bg-indigo-50 rounded-lg flex items-center justify-center flex-shrink-0">
                      {icon}
                    </div>
                    <div>
                      <h2 className="text-base font-semibold text-slate-900">
                        {title}
                      </h2>
                      <p className="text-xs text-slate-400 mt-0.5">{sub}</p>
                    </div>
                  </div>
                  {/* task rows */}
                  <div className="divide-y divide-slate-50">
                    {items.length === 0 ? (
                      <p className="px-5 py-5 text-sm text-slate-400">
                        {empty}
                      </p>
                    ) : (
                      items.map((item) => (
                        <div
                          key={item.key}
                          className="flex items-center justify-between px-5 py-3.5"
                        >
                          <div className="flex items-center gap-3">
                            {item.statusIcon}
                            <div>
                              <p className="text-sm font-medium text-slate-700 capitalize">
                                {item.primary}
                              </p>
                              {item.secondary && (
                                <p className="text-xs text-slate-400 mt-0.5">
                                  {item.secondary}
                                </p>
                              )}
                              {item.error && (
                                <p className="text-xs text-red-500 mt-0.5">
                                  {item.error}
                                </p>
                              )}
                            </div>
                          </div>
                          <StatusBadge status={item.status} />
                        </div>
                      ))
                    )}
                  </div>
                </div>
              ))}
            </div>

            {/* ── Right column: sidebar (sticky) ── */}
            <div className="col-span-1 space-y-4 sticky top-20">
              {/* Timeline: when the workflow started and finished */}
              <div className="card p-5">
                <p className="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-4">
                  Timeline
                </p>
                <div className="space-y-3">
                  <div>
                    <p className="text-xs text-slate-400 mb-0.5">Started</p>
                    <p className="text-sm font-medium text-slate-700">
                      {onboarding.startedAt
                        ? new Date(onboarding.startedAt).toLocaleString()
                        : "—"}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-slate-400 mb-0.5">Completed</p>
                    <p className="text-sm font-medium text-slate-700">
                      {onboarding.completedAt
                        ? new Date(onboarding.completedAt).toLocaleString()
                        : "—"}
                    </p>
                  </div>
                  {/* only shown when there have been retries */}
                  {onboarding.retryCount > 0 && (
                    <div>
                      <p className="text-xs text-slate-400 mb-0.5">Retries</p>
                      <p className="text-sm font-medium text-amber-600">
                        {onboarding.retryCount} attempt
                        {onboarding.retryCount !== 1 ? "s" : ""}
                      </p>
                    </div>
                  )}
                </div>
              </div>

              {/* Attempt History: timeline of every workflow run */}
              <div className="card p-5">
                <p className="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-4">
                  Attempt History
                </p>
                {(onboarding.retryHistory ?? []).length === 0 ? (
                  <p className="text-sm text-slate-400">No attempts yet.</p>
                ) : (
                  <div className="relative pl-5">
                    {/* vertical line connecting attempt dots */}
                    <div className="absolute left-[7px] top-2 bottom-2 w-px bg-slate-200" />

                    <div className="space-y-4">
                      {(onboarding.retryHistory ?? []).map(
                        (entry: RetryHistoryEntry) => (
                          <div key={entry.historyId} className="relative">
                            {/* coloured dot: green = completed, red = failed */}
                            <div
                              className={`absolute -left-5 top-[3px] w-3 h-3 rounded-full border-2 border-white z-10 ${
                                entry.status === "completed"
                                  ? "bg-emerald-500"
                                  : "bg-red-400"
                              }`}
                            />

                            {/* attempt number + status badge + timestamp */}
                            <div className="flex items-center gap-1.5 flex-wrap">
                              <span className="text-xs font-semibold text-slate-700">
                                Attempt {entry.attempt}
                              </span>
                              <span
                                className={`text-[10px] font-bold uppercase tracking-wide px-1.5 py-0.5 rounded-full ${
                                  entry.status === "completed"
                                    ? "text-emerald-700 bg-emerald-100"
                                    : "text-red-700 bg-red-100"
                                }`}
                              >
                                {entry.status}
                              </span>
                              <span className="text-[11px] text-slate-400 ml-auto shrink-0">
                                {new Date(entry.attemptedAt).toLocaleString(
                                  "en-SG",
                                  {
                                    day: "numeric",
                                    month: "short",
                                    hour: "2-digit",
                                    minute: "2-digit",
                                  },
                                )}
                              </span>
                            </div>

                            {/* error detail — only on failed attempts */}
                            {entry.errorMessage && (
                              <p className="text-[11px] text-slate-500 mt-1 leading-relaxed break-all">
                                {entry.errorMessage}
                              </p>
                            )}
                          </div>
                        ),
                      )}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
