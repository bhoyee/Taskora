import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

const dashboard = {
  projectCount: 1,
  activeTaskCount: 3,
  blockedTaskCount: 1,
  overdueTaskCount: 1,
  criticalTaskCount: 2,
}
const taskPage = {
  totalCount: 1,
  items: [{
    id: 'task-1',
    assignedUserId: null,
    title: 'Ship portfolio',
    status: 'InProgress',
    isBlocked: false,
    dueDate: '2026-07-10',
    priorityScore: 12.5,
    priorityBand: 'Critical',
    deadlineHealth: 'AtRisk',
    priorityExplanation: {
      score: 12.5,
      band: 'Critical',
      effort: 2,
      businessValueContribution: 15,
      urgencyContribution: 10,
      riskReductionContribution: 8,
    },
  }],
}
const workspaces = [{
  id: 'workspace-1',
  name: 'Portfolio team',
  role: 'Owner',
}]
const members = [{
  userId: 'user-1',
  displayName: 'Jadesola Aliu',
  email: 'jadesola@example.com',
  role: 'Owner',
}]

function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input) => {
      const url = String(input)
      const value = url.includes('/workspaces/workspace-1/members')
        ? members
        : url.endsWith('/workspaces')
          ? workspaces
          : url.includes('/dashboard')
            ? dashboard
            : taskPage
      return new Response(JSON.stringify(value), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    })
}

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})

describe('delivery workspace', () => {
  it('shows portfolio health and explainable task priority', async () => {
    mockApi()

    render(<App />)

    expect(await screen.findByText('Ship portfolio')).toBeInTheDocument()
    expect(screen.getByText('12.5')).toBeInTheDocument()
    expect(screen.getByText(/Value 15/)).toBeInTheDocument()
    expect(screen.getByText('Critical', { selector: '.metric span' })).toBeInTheDocument()
  })

  it('switches to the board and opens task creation', async () => {
    mockApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /board/i }))
    expect(screen.getByText('In progress')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /new task/i }))
    await waitFor(() =>
      expect(screen.getByRole('dialog', { name: 'Create task' })).toBeInTheDocument())
  })

  it('opens an existing task for planning and workflow changes', async () => {
    mockApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: 'Edit Ship portfolio' }))

    expect(screen.getByRole('dialog', { name: 'Edit task' })).toBeInTheDocument()
    expect(screen.getByLabelText('Business value')).toHaveValue(5)
    expect(screen.getByRole('button', { name: 'Complete task' })).toBeInTheDocument()
  })

  it('explains when an API request is routed to the frontend', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response('<!doctype html>', {
        status: 200,
        headers: { 'Content-Type': 'text/html' },
      }),
    )

    render(<App />)

    expect(await screen.findByText(
      'The API returned an unexpected response. Check that the API server is running.',
    )).toBeInTheDocument()
  })
})
