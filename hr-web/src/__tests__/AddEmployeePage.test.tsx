import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import AddEmployeePage from '../pages/AddEmployeePage'
import type { Employee } from '../types'

// capture navigate calls to assert redirects
const mockNavigate = vi.fn()

// no real HTTP calls
vi.mock('../api', () => ({
  createEmployee: vi.fn(),
}))

// replace useNavigate with spy, keep rest of module intact
vi.mock('react-router-dom', async (importOriginal) => {
  const mod = await importOriginal<typeof import('react-router-dom')>()
  return { ...mod, useNavigate: () => mockNavigate }
})

import { createEmployee } from '../api'
const mockCreateEmployee = vi.mocked(createEmployee)

// fixture — employee returned by API on successful create
const createdEmployee: Employee = {
  employeeId: 'new-emp-id',
  employeeNumber: 'EMP-0006',
  firstName: 'Charlie',
  lastName: 'Ong',
  fullName: 'Charlie Ong',
  email: 'charlie.ong@htx.gov.sg',
  department: 'Engineering',
  position: 'Engineer',
  hireDate: '2026-06-01',
  status: 'active',
  createdAt: '2026-05-27T00:00:00Z',
}

// MemoryRouter required for Link / useNavigate
function renderPage() {
  return render(
    <MemoryRouter>
      <AddEmployeePage />
    </MemoryRouter>
  )
}

// fills form fields with defaults; pass overrides for specific test cases
async function fillForm(overrides: Partial<{
  firstName: string
  lastName: string
  emailPrefix: string
  position: string
}> = {}) {
  const user = userEvent.setup()
  const { firstName = 'Charlie', lastName = 'Ong', emailPrefix = 'charlie.ong', position = 'Engineer' } = overrides

  await user.type(screen.getByPlaceholderText('Alice'), firstName)
  await user.type(screen.getByPlaceholderText('Tan'), lastName)
  await user.type(screen.getByPlaceholderText('alice.tan'), emailPrefix)
  await user.type(screen.getByPlaceholderText('Software Engineer'), position)
  return user
}

describe('AddEmployeePage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  // ── Rendering ──────────────────────────────────────────────────────────────

  it('renders all form fields', () => {
    renderPage()
    expect(screen.getByPlaceholderText('Alice')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Tan')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('alice.tan')).toBeInTheDocument()
    expect(screen.getByText('@htx.gov.sg')).toBeInTheDocument()
    expect(screen.getByPlaceholderText('Software Engineer')).toBeInTheDocument()
    expect(screen.getByRole('combobox')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Create Employee/i })).toBeInTheDocument()
  })

  it('renders department options', async () => {
    renderPage()
    const select = screen.getByRole('combobox')
    expect(select).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Engineering' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Human Resources' })).toBeInTheDocument()
  })

  // ── Submission ─────────────────────────────────────────────────────────────

  it('submits with email constructed from prefix + @htx.gov.sg', async () => {
    mockCreateEmployee.mockResolvedValue(createdEmployee)
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /Create Employee/i }))

    await waitFor(() => {
      expect(mockCreateEmployee).toHaveBeenCalledWith(
        expect.objectContaining({ email: 'charlie.ong@htx.gov.sg' })
      )
    })
  })

  it('submits correct firstName and lastName', async () => {
    mockCreateEmployee.mockResolvedValue(createdEmployee)
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /Create Employee/i }))

    await waitFor(() => {
      expect(mockCreateEmployee).toHaveBeenCalledWith(
        expect.objectContaining({ firstName: 'Charlie', lastName: 'Ong' })
      )
    })
  })

  // on success → redirect to new employee's onboarding page
  it('navigates to onboarding page on success', async () => {
    mockCreateEmployee.mockResolvedValue(createdEmployee)
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /Create Employee/i }))

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/onboarding/new-emp-id')
    })
  })

  // ── Error handling ─────────────────────────────────────────────────────────

  it('shows error message when API returns error with message', async () => {
    const err = { response: { data: { message: 'Employee already exists' } } }
    mockCreateEmployee.mockRejectedValue(err)
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /Create Employee/i }))

    expect(await screen.findByText('Employee already exists')).toBeInTheDocument()
  })

  it('shows generic error when API error has no message', async () => {
    mockCreateEmployee.mockRejectedValue(new Error('Network error'))
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    await user.click(screen.getByRole('button', { name: /Create Employee/i }))

    expect(await screen.findByText(/Failed to create employee/)).toBeInTheDocument()
  })

  // ── Loading state ──────────────────────────────────────────────────────────

  // button disabled while in-flight — prevents double-submit
  it('disables submit button while submitting', async () => {
    let resolve!: (v: Employee) => void
    mockCreateEmployee.mockReturnValue(new Promise<Employee>(r => { resolve = r }))
    renderPage()
    await fillForm()

    const user = userEvent.setup()
    const btn = screen.getByRole('button', { name: /Create Employee/i })
    await user.click(btn)

    expect(btn).toBeDisabled()
    resolve(createdEmployee)
  })

  // ── Navigation ─────────────────────────────────────────────────────────────

  it('shows Cancel link back to employee list', () => {
    renderPage()
    expect(screen.getByRole('link', { name: 'Cancel' })).toBeInTheDocument()
  })
})
