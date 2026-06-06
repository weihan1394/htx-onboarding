import { BrowserRouter } from "react-router-dom";
import Navbar from "./components/Navbar";
import AppRoutes from "./routes";

// BrowserRouter wraps the whole app so every child can use React Router hooks
// future flags opt-in to v7 behaviour ahead of the upgrade
export default function App() {
  return (
    <BrowserRouter
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      {/* full-height slate background — no white flash on any page */}
      <div className="min-h-screen bg-slate-50">
        <Navbar /> {/* sticky top bar */}
        <AppRoutes /> {/* page content swaps here based on URL */}
      </div>
    </BrowserRouter>
  );
}
