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
  if (!response.ok) throw new Error('The workspace could not be loaded.')

  const contentType = response.headers.get('content-type')
  if (!contentType?.includes('application/json')) {
    throw new Error('The API returned an unexpected response. Check that the API server is running.')
  }

  return response.json() as Promise<T>
}

export const api = {
  workspaces: () => request<Workspace[]>('/api/v1/workspaces'),
  members: (workspaceId: string) =>
    request<WorkspaceMember[]>(`/api/v1/workspaces/${workspaceId}/members`),
  dashboard: () => request<Dashboard>('/api/v1/dashboard'),
  projects: (workspaceId: string) =>
    request<ProjectDetails[]>(`/api/v1/workspaces/${workspaceId}/projects`),
  project: (projectId = developmentProjectId) =>
    request<ProjectDetails>(`/api/v1/projects/${projectId}`),
  tasks: (projectId = developmentProjectId, search = '', pageNumber = 1, pageSize = 10) =>
    request<TaskPage>(
      `/api/v1/tasks?projectId=${projectId}&search=${encodeURIComponent(search)}&pageNumber=${pageNumber}&pageSize=${pageSize}`,
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
