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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) throw new Error('The workspace could not be loaded.')
  return response.json() as Promise<T>
}

export const api = {
  dashboard: () => request<Dashboard>('/api/v1/dashboard'),
  tasks: (search = '') =>
    request<TaskPage>(
      `/api/v1/tasks?projectId=${projectId}&search=${encodeURIComponent(search)}&pageNumber=1&pageSize=100`,
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
}
