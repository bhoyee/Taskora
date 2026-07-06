export const projectId = '10000000-0000-0000-0000-000000000001'

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
  title: string
  status: TaskStatus
  isBlocked: boolean
  dueDate: string | null
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

export interface TaskActivity {
  sequence: number
  taskId: string
  actor: string
  activityType: string
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
  tasks: (search = '', pageNumber = 1, pageSize = 10) =>
    request<TaskPage>(
      `/api/v1/tasks?projectId=${projectId}&search=${encodeURIComponent(search)}&pageNumber=${pageNumber}&pageSize=${pageSize}`,
    ),
  createTask: (title: string, dueDate: string, effort: number) =>
    request<TaskItem>(`/api/v1/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify({ title, dueDate: dueDate || null, effort }),
    }),
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
  activity: (id: string) =>
    request<TaskActivity[]>(`/api/v1/tasks/${id}/activity`),
}
