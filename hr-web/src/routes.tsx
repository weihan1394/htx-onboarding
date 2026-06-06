import { Routes, Route } from "react-router-dom";
import EmployeeListPage from "./pages/EmployeeListPage";
import AddEmployeePage from "./pages/AddEmployeePage";
import OnboardingDetailPage from "./pages/OnboardingDetailPage";

// URL map — every route and its page component
//  /                        → employee list (home)
//  /employees/new           → add-employee form
//  /onboarding/:employeeId  → live onboarding status for one employee
export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<EmployeeListPage />} />
      <Route path="/employees/new" element={<AddEmployeePage />} />
      <Route
        path="/onboarding/:employeeId"
        element={<OnboardingDetailPage />}
      />
    </Routes>
  );
}
