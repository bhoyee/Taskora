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
    categoryId: 'category-1',
    title: 'Ship portfolio',
    status: 'InProgress',
    isBlocked: false,
    dueDate: '2026-07-10',
    tags: ['portfolio'],
    notes: [{
      id: 'note-1',
      taskId: 'task-1',
      authorId: 'user-1',
      body: 'Confirm launch checklist.',
      createdAt: '2026-07-06T09:00:00Z',
    }],
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
const secondWorkspace = {
  id: 'workspace-2',
  name: 'Client delivery',
  role: 'Owner',
}
const invitation = {
  id: 'invite-1',
  workspaceId: 'workspace-1',
  workspaceName: 'Portfolio team',
  fullName: 'Ada Lovelace',
  email: 'ada@example.com',
  role: 'Member',
  status: 'Pending',
  createdAt: '2026-07-09T09:00:00Z',
  expiresAt: '2026-07-16T09:00:00Z',
  inviteLink: '/invite/token-1',
}
const projectDetails = {
  id: '10000000-0000-0000-0000-000000000001',
  name: 'Portfolio launch',
  description: null as string | null,
  targetDate: null,
  isArchived: false,
  archivedAt: null,
  categories: [{
    id: 'category-1',
    projectId: '10000000-0000-0000-0000-000000000001',
    name: 'Client Work',
  }],
}
const secondProjectDetails = {
  ...projectDetails,
  id: 'project-2',
  name: 'Client delivery project',
  categories: [],
}
const createdProjectDetails = {
  ...projectDetails,
  id: 'project-3',
  name: 'Client onboarding',
  description: 'New project for onboarding work.',
  categories: [],
}
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
  action: 'StatusChanged',
  previousValue: 'Ready',
  currentValue: 'InProgress',
  occurredAt: '2026-07-06T10:00:00Z',
}]
const accountProfile = {
  userId: 'user-1',
  displayName: 'Jadesola Aliu',
  email: 'jadesola@example.com',
}

function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input) => {
      const url = String(input)
      const value = mockResponseFor(url, taskPage)
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
      const value = mockResponseFor(url, {
        totalCount: 11,
        items: pageTwo ? [secondTask] : taskPage.items,
      }, [])
      return new Response(JSON.stringify(value), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    })
}

function mockMemberWorkspaceWithoutProjectsApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input) => {
      const url = String(input)
      if (url.includes('/account/me')) return jsonResponse(accountProfile)
      if (url.endsWith('/workspaces')) {
        return jsonResponse([{
          id: 'workspace-member',
          name: 'Delivery workspace',
          role: 'Member',
        }])
      }
      if (url.includes('/workspaces/workspace-member/projects')) {
        return jsonResponse([])
      }
      if (url.includes('/workspaces/workspace-member/members')) {
        return jsonResponse([{
          ...members[0],
          role: 'Member',
        }])
      }
      if (url.includes('/dashboard')) {
        return jsonResponse({
          projectCount: 0,
          activeTaskCount: 0,
          blockedTaskCount: 0,
          overdueTaskCount: 0,
          criticalTaskCount: 0,
        })
      }
      return jsonResponse({ totalCount: 0, items: [] })
    })
}

function mockResponseFor(url: string, page = taskPage, activityItems = activity) {
  if (url.includes('/account/me')) return accountProfile
  if (url.includes('/account/profile')) return {
    ...accountProfile,
    email: 'jadesola.portfolio@example.com',
  }
  if (url.includes('/account/password')) return true
  if (url.includes('/activity')) return activityItems
  if (url.includes('/workspaces/workspace-1/invitations')) return []
  if (url.includes('/workspaces/workspace-1/projects')) return [projectDetails]
  if (url.includes('/workspaces/workspace-1/members')) return members
  if (url.endsWith('/workspaces')) return workspaces
  if (url.includes('/api/v1/projects/10000000-0000-0000-0000-000000000001')) return projectDetails
  if (url.includes('/api/v1/tasks/task-1')) return taskPage.items[0]
  if (url.includes('/api/v1/tasks/task-2')) return secondTask
  if (url.includes('/dashboard')) return dashboard
  return page
}

function mockWorkspaceManagementApi() {
  let currentWorkspaces = [...workspaces]
  let currentInvitations: typeof invitation[] = []
  let currentProjects = [projectDetails]

  return vi.spyOn(globalThis, 'fetch').mockImplementation(
    async (input, init) => {
      const url = String(input)
      const method = init?.method?.toUpperCase() ?? 'GET'

      if (url.endsWith('/workspaces') && method === 'POST') {
        currentWorkspaces = [...currentWorkspaces, secondWorkspace]
        return jsonResponse(secondWorkspace)
      }

      if (url.includes('/workspaces/workspace-1/invitations') && method === 'POST') {
        currentInvitations = [invitation]
        return jsonResponse(invitation)
      }

      if (url.includes('/workspaces/workspace-1/invitations')) {
        return jsonResponse(currentInvitations)
      }

      if (url.includes('/account/me')) {
        return jsonResponse(accountProfile)
      }

      if (url.includes('/account/profile')) {
        return jsonResponse({
          ...accountProfile,
          email: 'jadesola.portfolio@example.com',
        })
      }

      if (url.includes('/account/password')) {
        return jsonResponse(true)
      }

      if (url.includes('/workspaces/workspace-1/projects') && method === 'POST') {
        currentProjects = [...currentProjects, createdProjectDetails]
        return jsonResponse(createdProjectDetails)
      }

      if (url.includes('/workspaces/workspace-1/projects')) {
        return jsonResponse(currentProjects)
      }

      if (url.endsWith('/workspaces')) {
        return jsonResponse(currentWorkspaces)
      }

      if (url.includes('/workspaces/workspace-2/projects')) {
        return jsonResponse([secondProjectDetails])
      }

      if (url.includes('workspaceId=workspace-2') && url.includes('/dashboard')) {
        return jsonResponse({
          projectCount: 1,
          activeTaskCount: 0,
          blockedTaskCount: 0,
          overdueTaskCount: 0,
          criticalTaskCount: 0,
        })
      }

      if (url.includes('workspaceId=workspace-2') && url.includes('/tasks')) {
        return jsonResponse({ totalCount: 0, items: [] })
      }

      return jsonResponse(mockResponseFor(url, taskPage, activity))
    })
}

function jsonResponse(value: unknown) {
  return new Response(JSON.stringify(value), {
    status: 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

afterEach(() => {
  cleanup()
  localStorage.clear()
  window.history.replaceState(null, '', '/')
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
    expect(screen.getByLabelText('Full name')).toHaveValue('Jadesola Aliu')
    expect(screen.getByLabelText('Workspace role')).toHaveValue('Owner')
    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'jadesola.portfolio@example.com')
    await user.click(screen.getByRole('button', { name: /save profile/i }))
    expect(screen.getByText('Profile updated.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Current password'), 'Portfolio123!')
    await user.type(screen.getByLabelText('New password'), 'Portfolio456!')
    await user.type(screen.getByLabelText('Confirm password'), 'Portfolio456!')
    await user.click(screen.getByRole('button', { name: /change password/i }))
    expect(screen.getByText('Password changed.')).toBeInTheDocument()
  }, 20000)

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
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
  })

  it('opens the activity page from a direct hash URL', async () => {
    mockPagedApi()
    window.history.replaceState(null, '', '/#activity')

    render(<App />)

    expect(await screen.findByRole('heading', { name: 'Activity timeline' })).toBeInTheDocument()
    expect(await screen.findByText('No recorded activity yet')).toBeInTheDocument()
    expect(screen.getByText('Current task snapshot')).toBeInTheDocument()
  })

  it('creates a workspace from the workspace switcher', async () => {
    mockWorkspaceManagementApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /^new$/i }))
    await user.type(screen.getByLabelText('Workspace name'), 'Client delivery')
    await user.click(screen.getByRole('button', { name: /create/i }))

    expect(await screen.findByText('Workspace Client delivery created.')).toBeInTheDocument()
    expect(screen.getByLabelText(/^Project$/)).toHaveValue('project-2')
  })

  it('creates and selects a project inside the workspace', async () => {
    mockWorkspaceManagementApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /new project/i }))
    await user.type(screen.getByLabelText('Project name'), 'Client onboarding')
    await user.type(screen.getByLabelText('Description'), 'New project for onboarding work.')
    await user.click(screen.getByRole('button', { name: /create project/i }))

    expect(await screen.findByText('Project Client onboarding created.')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Client onboarding')).toBeInTheDocument()
  })

  it('hides project creation and disables tasks for member workspaces with no project', async () => {
    mockMemberWorkspaceWithoutProjectsApi()

    render(<App />)

    expect(await screen.findByRole('heading', { name: 'No project yet' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /new project/i })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /new task/i })).toBeDisabled()
  })

  it('creates a pending invite from workspace settings', async () => {
    mockWorkspaceManagementApi()
    const user = userEvent.setup()
    render(<App />)
    await screen.findByText('Ship portfolio')

    await user.click(screen.getByRole('button', { name: /settings/i }))
    await user.type(screen.getByLabelText('Full name'), 'Ada Lovelace')
    await user.type(screen.getByLabelText('Email'), 'ada@example.com')
    await user.click(screen.getByRole('button', { name: /send invite/i }))

    expect(await screen.findByText(/Invite link created for ada@example.com/)).toBeInTheDocument()
    expect(screen.getByText('Pending invitations')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '/invite/token-1' })).toBeInTheDocument()
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
