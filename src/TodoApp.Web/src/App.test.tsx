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
const secondTask = {
  ...taskPage.items[0],
  id: 'task-2',
  title: 'Review deployment runbook',
  status: 'Ready',
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
const activity = [{
  sequence: 1,
  taskId: 'task-1',
  actor: 'Jadesola Aliu',
  activityType: 'StatusChanged',
  previousValue: 'Ready',
  currentValue: 'InProgress',
  occurredAt: '2026-07-06T10:00:00Z',
}]

function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input) => {
      const url = String(input)
      const value = url.includes('/activity')
        ? activity
        : url.includes('/workspaces/workspace-1/members')
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

function mockPagedApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input) => {
      const url = String(input)
      const pageTwo = url.includes('pageNumber=2')
      const value = url.includes('/activity')
        ? []
        : url.includes('/workspaces/workspace-1/members')
        ? members
        : url.endsWith('/workspaces')
          ? workspaces
          : url.includes('/dashboard')
            ? dashboard
            : {
                totalCount: 11,
                items: pageTwo ? [secondTask] : taskPage.items,
              }
      return new Response(JSON.stringify(value), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    })
}

afterEach(() => {
  cleanup()
  localStorage.clear()
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

  it('opens activity, settings, and profile pages from the sidebar', async () => {
    mockApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /activity/i }))
    expect(screen.getByRole('heading', { name: 'Activity timeline' })).toBeInTheDocument()
    expect(screen.getByText(/changed status changed/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /settings/i }))
    expect(screen.getByRole('heading', { name: 'Workspace settings' })).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Default view'), 'board')
    await user.click(screen.getByRole('button', { name: /save settings/i }))
    expect(screen.getByText('Settings saved.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /profile/i }))
    expect(screen.getByRole('heading', { name: 'Profile' })).toBeInTheDocument()
    await user.clear(screen.getByLabelText('Display name'))
    await user.type(screen.getByLabelText('Display name'), 'Jadesola Portfolio')
    await user.click(screen.getByRole('button', { name: /save profile/i }))
    expect(screen.getByText('Profile updated.')).toBeInTheDocument()
  })

  it('shows activity fallback, paginates tasks, and logs out from the menu', async () => {
    mockPagedApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /activity/i }))
    expect(screen.getByText('No recorded activity yet')).toBeInTheDocument()
    expect(screen.getByText('Current task snapshot')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /workspace/i }))
    expect(screen.getByText('Showing 1-10 of 11')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /next/i }))
    expect(await screen.findByText('Review deployment runbook')).toBeInTheDocument()
    expect(screen.getByText('Showing 11-11 of 11')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /^logout$/i }))
    expect(screen.getByText('You have been logged out of the browser session.')).toBeInTheDocument()
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
