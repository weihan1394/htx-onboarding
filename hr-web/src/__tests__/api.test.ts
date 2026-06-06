import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// vi.hoisted ensures these exist when the vi.mock factory runs (before imports)
const { mockGet, mockPost } = vi.hoisted(() => ({
  mockGet: vi.fn(),
  mockPost: vi.fn(),
}))

vi.mock('axios', () => ({
  default: {
    create: vi.fn(() => ({ get: mockGet, post: mockPost })),
  },
}))

import { getEmployees, getEmployee, createEmployee, getOnboardingStatus, subscribeToOnboarding, retryWorkflow } from '../api'

const employee = {
  employeeId: 'emp-1',
  employeeNumber: 'EMP-0001',
  firstName: 'Alice',
  lastName: 'Tan',
  fullName: 'Alice Tan',
  email: 'alice.tan@htx.gov.sg',
  department: 'Engineering',
  position: 'Engineer',
  hireDate: '2026-06-01',
  status: 'active',
  createdAt: '2026-05-27T00:00:00Z',
}

describe('getEmployees', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls GET /employees and returns data', async () => {
    mockGet.mockResolvedValue({ data: [employee] })
    const result = await getEmployees()
    expect(mockGet).toHaveBeenCalledWith('/employees')
    expect(result).toEqual([employee])
  })

  it('propagates API errors', async () => {
    mockGet.mockRejectedValue(new Error('Network error'))
    await expect(getEmployees()).rejects.toThrow('Network error')
  })
})

describe('getEmployee', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls GET /employees/:id and returns employee', async () => {
    mockGet.mockResolvedValue({ data: employee })
    const result = await getEmployee('emp-1')
    expect(mockGet).toHaveBeenCalledWith('/employees/emp-1')
    expect(result).toEqual(employee)
  })
})

describe('createEmployee', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls POST /employees with payload and returns created employee', async () => {
    const payload = {
      firstName: 'Alice',
      lastName: 'Tan',
      email: 'alice.tan@htx.gov.sg',
      department: 'Engineering',
      position: 'Engineer',
      hireDate: '2026-06-01',
    }
    mockPost.mockResolvedValue({ data: employee })
    const result = await createEmployee(payload)
    expect(mockPost).toHaveBeenCalledWith('/employees', payload)
    expect(result).toEqual(employee)
  })

  it('propagates API errors', async () => {
    mockPost.mockRejectedValue(new Error('Conflict'))
    await expect(createEmployee({} as never)).rejects.toThrow('Conflict')
  })
})

describe('getOnboardingStatus', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls GET /employee/:id and returns onboarding status', async () => {
    const status = { onboardingId: 'ob-1', employeeId: 'emp-1' }
    mockGet.mockResolvedValue({ data: status })
    const result = await getOnboardingStatus('emp-1')
    expect(mockGet).toHaveBeenCalledWith('/employees/emp-1/onboarding')
    expect(result).toEqual(status)
  })

  it('propagates API errors', async () => {
    mockGet.mockRejectedValue(new Error('Not found'))
    await expect(getOnboardingStatus('emp-1')).rejects.toThrow('Not found')
  })
})

describe('subscribeToOnboarding', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('creates EventSource at correct URL and wires onmessage to onUpdate', () => {
    let capturedOnMessage: ((e: MessageEvent) => void) | null = null
    let capturedUrl = ''
    class MockEventSource {
      close = vi.fn()
      set onmessage(fn: (e: MessageEvent) => void) { capturedOnMessage = fn }
      constructor(url: string) { capturedUrl = url }
    }
    vi.stubGlobal('EventSource', MockEventSource)

    const onUpdate = vi.fn()
    subscribeToOnboarding('emp-1', onUpdate)

    expect(capturedUrl).toBe('/api/hr/employees/emp-1/onboarding/stream')
    capturedOnMessage!({ data: JSON.stringify({ status: 'in_progress' }) } as MessageEvent)
    expect(onUpdate).toHaveBeenCalledWith('in_progress')
  })
})

describe('retryWorkflow', () => {
  beforeEach(() => vi.clearAllMocks())

  it('calls POST /employees/:id/onboarding/retry', async () => {
    mockPost.mockResolvedValue({})
    await retryWorkflow('emp-1')
    expect(mockPost).toHaveBeenCalledWith('/employees/emp-1/onboarding/retry')
  })
})
