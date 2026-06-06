import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import App from '../App'

// no real HTTP calls during tests
vi.mock('../api', () => ({
  getEmployees: vi.fn().mockResolvedValue([]),
  getEmployee: vi.fn(),
  getOnboardingStatus: vi.fn(),
  createEmployee: vi.fn(),
  retryWorkflow: vi.fn(),
}))

// renders the full App (BrowserRouter + Navbar + routes) at the root path
function renderAppNative() {
  return render(<App />)
}

// ─── App ─────────────────────────────────────────────────────────────────────
// smoke tests — shell mounts correctly
describe('App', () => {
  it('renders navbar HTX brand', () => {
    renderAppNative()
    expect(screen.getByText('HTX')).toBeInTheDocument()
    expect(screen.getByText('HR System')).toBeInTheDocument()
  })

  it('renders Employees nav link', () => {
    renderAppNative()
    const links = screen.getAllByRole('link', { name: 'Employees' })
    expect(links.length).toBeGreaterThan(0)
  })

  it('renders Add Employee nav link', () => {
    renderAppNative()
    const links = screen.getAllByRole('link', { name: 'Add Employee' })
    expect(links.length).toBeGreaterThan(0)
  })

  it('renders EmployeeListPage at root path', async () => {
    renderAppNative()
    const matches = await screen.findAllByText('Employees', {}, { timeout: 2000 })
    expect(matches.length).toBeGreaterThan(0)
  })
})

// ─── Navbar ──────────────────────────────────────────────────────────────────
// active-link highlight logic
describe('Navbar', () => {
  it('highlights Employees link when at root', () => {
    renderAppNative()
    const employeesLinks = screen.getAllByRole('link', { name: 'Employees' })
    // find the navbar link (href="/")
    const navLink = employeesLinks.find(l => l.getAttribute('href') === '/')
    expect(navLink).toHaveClass('text-white')
  })
})
