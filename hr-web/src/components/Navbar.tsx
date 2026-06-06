import { Link, useLocation } from "react-router-dom";
import { Shield } from "lucide-react";

// sticky top nav bar
export default function Navbar() {
  // current URL path — used to highlight the active link
  const { pathname } = useLocation();

  return (
    <nav className="bg-slate-900 border-b border-slate-800 sticky top-0 z-20">
      <div className="max-w-6xl mx-auto px-6 h-16 flex items-center justify-between">

        {/* brand logo — always navigates home */}
        <Link to="/" className="flex items-center gap-3">
          <div className="w-9 h-9 bg-indigo-600 rounded-xl flex items-center justify-center ring-1 ring-indigo-500 shadow-lg shadow-indigo-900/40">
            <Shield className="w-4 h-4 text-white" />
          </div>
          <div className="flex items-baseline gap-2">
            <span className="font-bold text-white text-sm tracking-wider">HTX</span>
            <span className="text-slate-500 text-xs">HR System</span>
          </div>
        </Link>

        {/* nav links — data-driven so adding a tab is one line */}
        <div className="flex items-center gap-1">
          {[
            { to: "/", label: "Employees" },
            { to: "/employees/new", label: "Add Employee" },
          ].map(({ to, label }) => (
            <Link
              key={to}
              to={to}
              // active → white tint, inactive → dimmed
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                pathname === to
                  ? "bg-white/10 text-white"
                  : "text-slate-400 hover:text-white hover:bg-white/5"
              }`}
            >
              {label}
            </Link>
          ))}
        </div>
      </div>
    </nav>
  );
}
