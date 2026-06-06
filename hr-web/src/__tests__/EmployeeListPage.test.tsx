import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import EmployeeListPage from '../pages/EmployeeListPage'
import type { Employee } from '../types'

// no real HTTP calls
vi.mock('../api', () => ({
  getEmployees: vi.fn(),
}))

import { getEmployees } from '../api'
const mockGetEmployees = vi.mocked(getEmployees)

// ── fixtures ───────────────────────────────────────────────────────────────────

const alice: Employee = {
  employeeId: 'emp-1',
  employeeNumber: 'EMP-0001',
  firstName: 'Alice',
  lastName: 'Tan',
  fullName: 'Alice Tan',
  email: 'alice.tan@htx.gov.sg',
  department: 'Engineering',
  position: 'Software Engineer',
  hireDate: '2026-06-01',
  status: 'active',
  createdAt: '2026-05-27T00:00:00Z',
}

// null department + position — used to test em-dash fallback
const bob: Employee = {
  employeeId: 'emp-2',
  employeeNumber: 'EMP-0002',
  firstName: 'Bob',
  lastName: 'Lim',
  fullName: 'Bob Lim',
  email: 'bob.lim@htx.gov.sg',
  department: null,
  position: null,
  hireDate: '2026-06-02',
  status: 'active',
  createdAt: '2026-05-27T00:00:00Z',
}

// MemoryRouter required for Link components
function renderPage() {
  return render(
    <MemoryRouter>
      <EmployeeListPage />
    </MemoryRouter>
  )
}

describe('EmployeeListPage', () => {
  beforeEach(() => vi.clearAllMocks())

  // ── Loading state ──────────────────────────────────────────────────────────

  it('shows loading spinner initially', () => {
    mockGetEmployees.mockResolvedValue([])
    renderPage()
    expect(document.querySelector('.animate-spin')).toBeInTheDocument()
  })

  // ── Successful fetch ───────────────────────────────────────────────────────

  it('shows employees after successful fetch', async () => {
    mockGetEmployees.mockResolvedValue([alice, bob])
    renderPage()
    expect(await screen.findByText('Alice Tan')).toBeInTheDocument()
    expect(screen.getByText('Bob Lim')).toBeInTheDocument()
  })

  it('shows employee count', async () => {
    mockGetEmployees.mockResolvedValue([alice, bob])
    renderPage()
    expect(await screen.findByText('Total Employees')).toBeInTheDocument()
  })

  it('shows employee number, department, and position', async () => {
    mockGetEmployees.mockResolvedValue([alice])
    renderPage()
    expect(await screen.findByText('EMP-0001')).toBeInTheDocument()
    expect(screen.getByText('Engineering')).toBeInTheDocument()
    expect(screen.getByText('Software Engineer')).toBeInTheDocument()
  })

  // null fields → "—" instead of blank
  it('shows em-dash for null department and position', async () => {
    mockGetEmployees.mockResolvedValue([bob])
    renderPage()
    await screen.findByText('Bob Lim')
    const dashes = screen.getAllByText('—')
    expect(dashes.length).toBeGreaterThanOrEqual(2)
  })

  // ── Empty state ────────────────────────────────────────────────────────────

  it('shows empty state when no employees', async () => {
    mockGetEmployees.mockResolvedValue([])
    renderPage()
    expect(await screen.findByText('No employees yet')).toBeInTheDocument()
  })

  // ── Error state ────────────────────────────────────────────────────────────

  it('shows error when fetch fails', async () => {
    mockGetEmployees.mockRejectedValue(new Error('Network error'))
    renderPage()
    expect(await screen.findByText(/Failed to load employees/)).toBeInTheDocument()
  })

  // ── Refresh button ─────────────────────────────────────────────────────────

  // Refresh → calls getEmployees again and shows updated data
  it('re-fetches when Refresh button is clicked', async () => {
    mockGetEmployees.mockResolvedValue([alice])
    renderPage()
    await screen.findByText('Alice Tan')

    mockGetEmployees.mockResolvedValue([alice, bob])
    await userEvent.click(screen.getByRole('button', { name: /Refresh/i }))

    expect(await screen.findByText('Bob Lim')).toBeInTheDocument()
    expect(mockGetEmployees).toHaveBeenCalledTimes(2)
  })

  // ── Links ──────────────────────────────────────────────────────────────────

  it('renders Add Employee link', async () => {
    mockGetEmployees.mockResolvedValue([])
    renderPage()
    await waitFor(() => expect(document.querySelector('.animate-spin')).not.toBeInTheDocument())
    const links = screen.getAllByText('Add Employee')
    expect(links.length).toBeGreaterThan(0)
  })

  // each row → link to /onboarding/:employeeId
  it('renders Onboarding link for each employee', async () => {
    mockGetEmployees.mockResolvedValue([alice, bob])
    renderPage()
    await screen.findByText('Alice Tan')
    const links = document.querySelectorAll('a[href*="/onboarding/"]')
    expect(links).toHaveLength(2)
  })
})
