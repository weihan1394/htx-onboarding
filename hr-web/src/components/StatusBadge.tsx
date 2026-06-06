type Status = string;

interface StatusBadgeProps {
  status: Status;
}

// maps every known status to its visual style + display label
// "issued", "scheduled", "submitted" are task-level terminal states — same green appearance as "completed"
const config: Record<string, { bg: string; text: string; dot: string; label: string }> = {
  active:      { bg: "bg-emerald-50",  text: "text-emerald-700", dot: "bg-emerald-500", label: "Active" },
  inactive:    { bg: "bg-slate-100",   text: "text-slate-500",   dot: "bg-slate-400",   label: "Inactive" },
  completed:   { bg: "bg-emerald-50",  text: "text-emerald-700", dot: "bg-emerald-500", label: "Completed" },
  in_progress: { bg: "bg-blue-50",     text: "text-blue-700",    dot: "bg-blue-500",    label: "In Progress" },
  pending:     { bg: "bg-amber-50",    text: "text-amber-700",   dot: "bg-amber-400",   label: "Pending" },
  failed:      { bg: "bg-red-50",      text: "text-red-700",     dot: "bg-red-500",     label: "Failed" },
  issued:      { bg: "bg-emerald-50",  text: "text-emerald-700", dot: "bg-emerald-500", label: "Completed" },
  scheduled:   { bg: "bg-emerald-50",  text: "text-emerald-700", dot: "bg-emerald-500", label: "Completed" },
  cancelled:   { bg: "bg-slate-100",   text: "text-slate-500",   dot: "bg-slate-400",   label: "Cancelled" },
  submitted:   { bg: "bg-emerald-50",  text: "text-emerald-700", dot: "bg-emerald-500", label: "Completed" },
};

// pill badge with coloured dot — falls back to grey + raw string for unknown statuses
export default function StatusBadge({ status }: StatusBadgeProps) {
  const c = config[status] ?? { bg: "bg-slate-100", text: "text-slate-500", dot: "bg-slate-400", label: status };
  return (
    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium ${c.bg} ${c.text}`}>
      <span className={`w-1.5 h-1.5 rounded-full flex-shrink-0 ${c.dot}`} />
      {c.label}
    </span>
  );
}
