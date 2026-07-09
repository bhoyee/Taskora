export const developmentProjectId = '10000000-0000-0000-0000-000000000001'

export type TaskStatus = 'Backlog' | 'Ready' | 'InProgress' | 'Blocked' | 'Completed'
export type DeadlineHealth = 'Healthy' | 'AtRisk' | 'Overdue' | 'Completed'

export interface PriorityExplanation {
  score: number
  band: string
  effort: number
  businessValueContribution: number
  urgencyContribution: number
  riskReductionContribution: number
}

export interface TaskItem {
  id: string
  assignedUserId: string | null
  categoryId: string | null
  title: string
  status: TaskStatus
  isBlocked: boolean
  dueDate: string | null
  tags: string[]
  notes?: TaskNote[]
  priorityScore: number | null
  priorityBand: string | null
  priorityExplanation: PriorityExplanation | null
  deadlineHealth: DeadlineHealth
}

export interface TaskPage {
  items: TaskItem[]
  totalCount: number
}

export interface Dashboard {
  projectCount: number
  activeTaskCount: number
  blockedTaskCount: number
  overdueTaskCount: number
  criticalTaskCount: number
}

export interface Workspace {
  id: string
  name: string
  role: 'Owner' | 'Manager' | 'Member'
}

export interface WorkspaceMember {
  userId: string
  displayName: string
  email: string
  role: 'Owner' | 'Manager' | 'Member'
}

export interface WorkspaceInvitation {
  id: string
  workspaceId: string
  workspaceName: string
  fullName: string
  email: string
  role: 'Owner' | 'Manager' | 'Member'
  status: 'Pending' | 'Accepted' | 'Declined' | 'Cancelled' | 'Expired'
  createdAt: string
  expiresAt: string
  inviteLink: string | null
}

export interface ProjectCategory {
  id: string
  projectId: string
  name: string
}

export interface ProjectDetails {
  id: string
  name: string
  description: string | null
  targetDate: string | null
  isArchived: boolean
  archivedAt: string | null
  categories: ProjectCategory[]
}

export interface TaskNote {
  id: string
  taskId: string
  authorId: string
  body: string
  createdAt: string
}

export interface AccountSession {
  userId: string
  displayName: string
  email: string
  accessToken: string
}

export interface TaskActivity {
  sequence: number
  taskId: string
  actor: string
  action?: string
  activityType?: string
  previousValue: string | null
  currentValue: string | null
  occurredAt: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const accessToken = localStorage.getItem('todoapp_access_token')
  const identityHeaders: Record<string, string> = accessToken
    ? { Authorization: `Bearer ${accessToken}` }
    : import.meta.env.DEV
      ? { 'X-User-Id': '30000000-0000-0000-0000-000000000001' }
      : {}
  const response = await fetch(path, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...identityHeaders,
      ...init?.headers,
    },
  })
  const contentType = response.headers.get('content-type')
  if (!response.ok) {
    if (contentType?.includes('application/json')) {
      const problem = await response.json().catch(() => null) as {
        title?: string
        detail?: string
      } | null
      throw new Error(
        problem?.detail ??
        problem?.title ??
        `The API returned ${response.status}.`)
    }

    if (response.status === 401) {
      throw new Error('Your session is no longer valid. Reset the session or sign in again.')
    }

    throw new Error(`The API returned ${response.status}. Check that the API server is running.`)
  }

  if (!contentType?.includes('application/json')) {
    throw new Error('The API returned an unexpected response. Check that the API server is running.')
  }

  return response.json() as Promise<T>
}

export const api = {
  workspaces: () => request<Workspace[]>('/api/v1/workspaces'),
  createWorkspace: (name: string) =>
    request<Workspace>('/api/v1/workspaces', {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  members: (workspaceId: string) =>
    request<WorkspaceMember[]>(`/api/v1/workspaces/${workspaceId}/members`),
  removeMember: (workspaceId: string, userId: string) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/members/${userId}`, {
      method: 'DELETE',
    }),
  changeMemberRole: (
    workspaceId: string,
    userId: string,
    role: 'Manager' | 'Member',
  ) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/members/${userId}`, {
      method: 'PUT',
      body: JSON.stringify({ role }),
    }),
  invitations: (workspaceId: string) =>
    request<WorkspaceInvitation[]>(`/api/v1/workspaces/${workspaceId}/invitations`),
  inviteMember: (
    workspaceId: string,
    fullName: string,
    email: string,
    role: 'Manager' | 'Member',
  ) =>
    request<WorkspaceInvitation>(`/api/v1/workspaces/${workspaceId}/invitations`, {
      method: 'POST',
      body: JSON.stringify({ fullName, email, role }),
    }),
  cancelInvitation: (workspaceId: string, invitationId: string) =>
    request<WorkspaceInvitation>(
      `/api/v1/workspaces/${workspaceId}/invitations/${invitationId}`,
      { method: 'DELETE' },
    ),
  invitation: (token: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}`),
  acceptInvitation: (token: string, displayName: string, password: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}/accept`, {
      method: 'POST',
      body: JSON.stringify({ displayName, password }),
    }),
  declineInvitation: (token: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}/decline`, {
      method: 'POST',
    }),
  dashboard: (workspaceId?: string, projectId?: string) =>
    request<Dashboard>(
      `/api/v1/dashboard?${new URLSearchParams({
        ...(workspaceId ? { workspaceId } : {}),
        ...(projectId ? { projectId } : {}),
      })}`,
    ),
  projects: (workspaceId: string) =>
    request<ProjectDetails[]>(`/api/v1/workspaces/${workspaceId}/projects`),
  createWorkspaceProject: (workspaceId: string, name: string) =>
    request<ProjectDetails>(`/api/v1/workspaces/${workspaceId}/projects`, {
      method: 'POST',
      body: JSON.stringify({
        name,
        description: 'Starter project created for this workspace.',
      }),
    }),
  project: (projectId = developmentProjectId) =>
    request<ProjectDetails>(`/api/v1/projects/${projectId}`),
  tasks: (
    workspaceId: string,
    search = '',
    pageNumber = 1,
    pageSize = 10,
    projectId?: string,
  ) =>
    request<TaskPage>(
      `/api/v1/tasks?${new URLSearchParams({
        workspaceId,
        ...(projectId ? { projectId } : {}),
        search,
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
      })}`,
    ),
  createTask: (projectId: string, title: string, dueDate: string, effort: number) =>
    request<TaskItem>(`/api/v1/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify({ title, dueDate: dueDate || null, effort }),
    }),
  task: (id: string) => request<TaskItem>(`/api/v1/tasks/${id}`),
  updateTask: (id: string, title: string, dueDate: string, effort: number) =>
    request(`/api/v1/tasks/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, dueDate: dueDate || null, effort }),
    }),
  updatePlanning: (
    id: string,
    businessValue: number,
    urgency: number,
    riskReduction: number,
    effort: number,
  ) =>
    request(`/api/v1/tasks/${id}/planning`, {
      method: 'PUT',
      body: JSON.stringify({ businessValue, urgency, riskReduction, effort }),
    }),
  transition: (id: string, action: string, body?: object) =>
    request(`/api/v1/tasks/${id}/${action}`, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  assign: (id: string, userId: string) =>
    request(`/api/v1/tasks/${id}/assignment`, {
      method: 'PUT',
      body: JSON.stringify({ userId }),
    }),
  unassign: (id: string) =>
    request(`/api/v1/tasks/${id}/assignment`, { method: 'DELETE' }),
  createCategory: (projectId: string, name: string) =>
    request<ProjectCategory>(`/api/v1/projects/${projectId}/categories`, {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  updateCategory: (id: string, categoryId: string | null) =>
    request<string | null>(`/api/v1/tasks/${id}/category`, {
      method: 'PUT',
      body: JSON.stringify({ categoryId }),
    }),
  addTag: (id: string, tag: string) =>
    request<string[]>(`/api/v1/tasks/${id}/tags`, {
      method: 'POST',
      body: JSON.stringify({ tag }),
    }),
  removeTag: (id: string, tag: string) =>
    request<string[]>(`/api/v1/tasks/${id}/tags/${encodeURIComponent(tag)}`, {
      method: 'DELETE',
    }),
  addNote: (id: string, body: string) =>
    request<TaskNote>(`/api/v1/tasks/${id}/notes`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  activity: (id: string) =>
    request<TaskActivity[]>(`/api/v1/tasks/${id}/activity`),
  login: (email: string, password: string) =>
    request<AccountSession>('/api/v1/account/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  register: (
    displayName: string,
    email: string,
    password: string,
    workspaceName: string,
  ) =>
    request<AccountSession>('/api/v1/account/register', {
      method: 'POST',
      body: JSON.stringify({ displayName, email, password, workspaceName }),
    }),
}
