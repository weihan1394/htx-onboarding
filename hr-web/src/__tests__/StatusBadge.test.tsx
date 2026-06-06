import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import StatusBadge from '../components/StatusBadge'

describe('StatusBadge', () => {
  // [status value, expected display label, expected bg class]
  const cases: [string, string, string][] = [
    ['active',      'Active',      'bg-emerald-50'],
    ['inactive',    'Inactive',    'bg-slate-100'],
    ['completed',   'Completed',   'bg-emerald-50'],
    ['in_progress', 'In Progress', 'bg-blue-50'],
    ['pending',     'Pending',     'bg-amber-50'],
    ['failed',      'Failed',      'bg-red-50'],
    // task-level terminal states → same "Completed" label as workflow-level
    ['issued',      'Completed',   'bg-emerald-50'],
    ['scheduled',   'Completed',   'bg-emerald-50'],
    ['cancelled',   'Cancelled',   'bg-slate-100'],
    ['submitted',   'Completed',   'bg-emerald-50'],
  ]

  it.each(cases)('status "%s" renders label "%s" with class "%s"', (status, label, cls) => {
    render(<StatusBadge status={status} />)
    const badge = screen.getByText(label)
    expect(badge).toBeInTheDocument()
    expect(badge).toHaveClass(cls)
  })

  // unknown status → raw string, no crash
  it('falls back to raw status value for unknown status', () => {
    render(<StatusBadge status="unknown_status" />)
    expect(screen.getByText('unknown_status')).toBeInTheDocument()
  })

  // unknown status → grey fallback style
  it('uses default style for unknown status', () => {
    render(<StatusBadge status="mystery" />)
    expect(screen.getByText('mystery')).toHaveClass('bg-slate-100')
  })
})
