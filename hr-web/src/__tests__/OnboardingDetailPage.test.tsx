import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import OnboardingDetailPage from '../pages/OnboardingDetailPage'
import type { Employee, OnboardingStatus } from '../types'

// no real HTTP calls — subscribeToOnboarding returns a no-op EventSource stub
vi.mock('../api', () => ({
  getEmployee: vi.fn(),
  getOnboardingStatus: vi.fn(),
  retryWorkflow: vi.fn(),
  subscribeToOnboarding: vi.fn(() => ({ close: vi.fn() })),
}))

import { getEmployee, getOnboardingStatus, retryWorkflow, subscribeToOnboarding } from '../api'
const mockGetEmployee = vi.mocked(getEmployee)
const mockGetOnboardingStatus = vi.mocked(getOnboardingStatus)
const mockRetryWorkflow = vi.mocked(retryWorkflow)
const mockSubscribeToOnboarding = vi.mocked(subscribeToOnboarding)

// ── fixtures ───────────────────────────────────────────────────────────────────

const employee: Employee = {
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

// mixed statuses — covers all task row variants
const onboarding: OnboardingStatus = {
  onboardingId: 'ob-1',
  employeeId: 'emp-1',
  status: 'completed',
  startedAt: '2026-05-28T01:00:00Z',
  completedAt: '2026-05-28T02:00:00Z',
  createdAt: '2026-05-28T01:00:00Z',
  retryCount: 0,
  accountTasks: [
    { taskId: 'at-1', accountType: 'email',     username: 'alice.tan@htx.gov.sg', status: 'completed', errorMessage: null,        completedAt: '2026-05-28T01:05:00Z' },
    { taskId: 'at-2', accountType: 'vpn',       username: 'alice.tan@htx.gov.sg', status: 'failed',    errorMessage: 'VPN error', completedAt: null },
    { taskId: 'at-3', accountType: 'hr_portal', username: null,                   status: 'pending',   errorMessage: null,        completedAt: null },
  ],
  equipmentTasks: [
    { taskId: 'et-1', itemType: 'laptop',     itemDetails: null, status: 'issued', errorMessage: null,           issuedAt: '2026-05-28T01:15:00Z' },
    { taskId: 'et-2', itemType: 'staff_pass', itemDetails: null, status: 'failed', errorMessage: 'Out of stock', issuedAt: null },
  ],
  retryHistory: [],
}

// sets URL to /onboarding/:employeeId so useParams resolves correctly
function renderPage(employeeId = 'emp-1') {
  return render(
    <MemoryRouter initialEntries={[`/onboarding/${employeeId}`]}>
      <Routes>
        <Route path="/onboarding/:employeeId" element={<OnboardingDetailPage />} />
      </Routes>
    </MemoryRouter>
  )
}

describe('OnboardingDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── Loading state ──────────────────────────────────────────────────────────

  it('shows loading spinner initially', () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    expect(document.querySelector('.animate-spin')).toBeInTheDocument()
  })

  // ── Successful load ────────────────────────────────────────────────────────

  it('shows employee name after data loads', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    expect(await screen.findByText("Alice Tan's Onboarding")).toBeInTheDocument()
  })

  it('shows employee number and email', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    await screen.findByText("Alice Tan's Onboarding")
    expect(screen.getByText(/EMP-0001/)).toBeInTheDocument()
    const emailMatches = screen.getAllByText(/alice\.tan@htx\.gov\.sg/)
    expect(emailMatches.length).toBeGreaterThan(0)
  })

  // ── Error state ────────────────────────────────────────────────────────────

  // hr-svc down → surface error immediately
  it('shows error when employee fetch fails', async () => {
    mockGetEmployee.mockRejectedValue(new Error('Not found'))
    mockGetOnboardingStatus.mockRejectedValue(new Error('Not found'))
    renderPage()
    expect(await screen.findByText(/HR Service is unavailable/)).toBeInTheDocument()
  })

  // ── Task sections ──────────────────────────────────────────────────────────

  it('renders account tasks with correct statuses', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    await screen.findByText('Account Creation')
    expect(screen.getAllByText('alice.tan@htx.gov.sg').length).toBeGreaterThan(0)
    expect(screen.getByText('VPN error')).toBeInTheDocument()
  })

  it('renders equipment tasks', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    await screen.findByText('Equipment Issuance')
    expect(screen.getByText('Out of stock')).toBeInTheDocument()
  })

  // empty arrays → "No tasks yet" placeholder, not blank
  it('shows empty states when tasks are empty', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({
      ...onboarding,
      accountTasks: [],
      equipmentTasks: [],
    })
    renderPage()
    await screen.findByText('Account Creation')
    expect(screen.getByText('No account tasks yet.')).toBeInTheDocument()
    expect(screen.getByText('No equipment tasks yet.')).toBeInTheDocument()
  })

  // ── Retry button ───────────────────────────────────────────────────────────

  it('shows retry button when status is failed', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({ ...onboarding, status: 'failed' })
    renderPage()
    expect(await screen.findByRole('button', { name: 'Retry Workflow' })).toBeInTheDocument()
  })

  // completed → no retry needed
  it('hides retry button when status is completed', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({ ...onboarding, status: 'completed' })
    renderPage()
    await screen.findByText("Alice Tan's Onboarding")
    expect(screen.queryByRole('button', { name: 'Retry Workflow' })).not.toBeInTheDocument()
  })

  // clicking Retry → calls retryWorkflow with correct employee ID
  it('calls retryWorkflow when retry button is clicked', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({ ...onboarding, status: 'failed' })
    mockRetryWorkflow.mockResolvedValue(undefined)
    renderPage()
    await screen.findByRole('button', { name: 'Retry Workflow' })
    await userEvent.click(screen.getByRole('button', { name: 'Retry Workflow' }))
    expect(mockRetryWorkflow).toHaveBeenCalledWith('emp-1')
  })

  // ── Refresh button ─────────────────────────────────────────────────────────

  // last button in header is the refresh icon
  it('re-fetches data when refresh button is clicked', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    await screen.findByText('Account Creation')
    const buttons = screen.getAllByRole('button')
    await userEvent.click(buttons[buttons.length - 1])
    expect(mockGetEmployee).toHaveBeenCalledTimes(2)
  })

  // ── Sidebar: Timeline ──────────────────────────────────────────────────────

  it('shows onboarding timeline with dates', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    expect(await screen.findByText('Timeline')).toBeInTheDocument()
    expect(screen.getByText('Started')).toBeInTheDocument()
    expect(screen.getAllByText(/\d{1,2}\/\d{1,2}\/\d{4}/).length).toBeGreaterThan(0)
  })

  // null timestamps → "—" placeholders
  it('shows dashes when startedAt and completedAt are null', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({ ...onboarding, startedAt: null, completedAt: null })
    renderPage()
    await screen.findByText('Timeline')
    const dashes = screen.getAllByText('—')
    expect(dashes.length).toBeGreaterThanOrEqual(2)
  })

  // ── Sidebar: Attempt History ───────────────────────────────────────────────

  it('shows attempt history when retryHistory has entries', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({
      ...onboarding,
      retryCount: 1,
      retryHistory: [{ historyId: 'h-1', attempt: 1, status: 'failed', attemptedAt: '2026-05-28T01:00:00Z', errorMessage: 'network error' }],
    })
    renderPage()
    await screen.findByText('Attempt History')
    expect(screen.getByText('Attempt 1')).toBeInTheDocument()
    expect(screen.getByText('network error')).toBeInTheDocument()
  })

  // ── Retry workflow error ───────────────────────────────────────────────────

  it('shows error banner when retryWorkflow call fails', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue({ ...onboarding, status: 'failed' })
    mockRetryWorkflow.mockRejectedValue(new Error('Service unavailable'))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Retry Workflow' }))
    expect(await screen.findByText(/Workflow Service is unavailable/)).toBeInTheDocument()
  })

  // ── Onboarding fetch errors ────────────────────────────────────────────────

  it('shows error when onboarding fetch fails with a non-404 status', async () => {
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockRejectedValue({ response: { status: 503 } })
    renderPage()
    expect(await screen.findByText(/Onboarding Service is unavailable/)).toBeInTheDocument()
  })

  it('shows not-started banner when onboarding fetch returns 404 on all attempts', async () => {
    vi.useFakeTimers()
    try {
      mockGetEmployee.mockResolvedValue(employee)
      mockGetOnboardingStatus.mockRejectedValue({ response: { status: 404 } })
      renderPage()
      // advance past all 4 retry delays (5 attempts × 5 s each)
      for (let i = 0; i < 5; i++) {
        await act(async () => { vi.advanceTimersByTime(5_000) })
        await act(async () => {})
      }
      expect(screen.getByText(/Onboarding has not started yet/)).toBeInTheDocument()
    } finally {
      vi.useRealTimers()
    }
  }, 15_000)

  // ── SSE live updates ───────────────────────────────────────────────────────

  it('does not re-fetch when SSE fires with not_started status', async () => {
    let capturedOnUpdate: ((status: string) => void) | null = null
    mockSubscribeToOnboarding.mockImplementation((_id, onUpdate) => {
      capturedOnUpdate = onUpdate
      return { close: vi.fn() } as unknown as EventSource
    })
    mockGetEmployee.mockResolvedValue(employee)
    mockGetOnboardingStatus.mockResolvedValue(onboarding)
    renderPage()
    await screen.findByText("Alice Tan's Onboarding")
    await act(async () => { capturedOnUpdate!('not_started') })
    // only the initial load — not_started early-returns without calling getOnboardingStatus again
    expect(mockGetOnboardingStatus).toHaveBeenCalledTimes(1)
  })

  it('re-fetches and updates onboarding when SSE fires with a real status', async () => {
    let capturedOnUpdate: ((status: string) => void) | null = null
    mockSubscribeToOnboarding.mockImplementation((_id, onUpdate) => {
      capturedOnUpdate = onUpdate
      return { close: vi.fn() } as unknown as EventSource
    })
    mockGetEmployee.mockResolvedValue(employee)
    const updatedOnboarding = { ...onboarding, status: 'completed' as const }
    mockGetOnboardingStatus
      .mockResolvedValueOnce(onboarding)
      .mockResolvedValueOnce(updatedOnboarding)
    renderPage()
    await screen.findByText("Alice Tan's Onboarding")
    await act(async () => { capturedOnUpdate!('completed') })
    expect(mockGetOnboardingStatus).toHaveBeenCalledTimes(2)
  })
})
