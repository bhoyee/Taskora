// Fixed id of the seed/demo project used for local development when no real project id is supplied.
export const developmentProjectId = '10000000-0000-0000-0000-000000000001'

// The lifecycle states a task moves through on the delivery board.
export type TaskStatus = 'Backlog' | 'Ready' | 'InProgress' | 'Blocked' | 'Completed'
// How a task's due date is tracking relative to today.
export type DeadlineHealth = 'Healthy' | 'AtRisk' | 'Overdue' | 'Completed'

// Breakdown of how a task's priority score was derived from its planning inputs.
export interface PriorityExplanation {
  score: number
  band: string
  effort: number
  businessValueContribution: number
  urgencyContribution: number
  riskReductionContribution: number
}

// A single task as returned by the tasks API, including status, scheduling, and priority scoring.
export interface TaskItem {
  id: string
  projectId: string
  createdByUserId: string | null
  assignedUserId: string | null
  createdAt: string
  sprintId: string | null
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
  effort?: number | null
}

// One page of tasks along with the total matching count (used where PagedResponse isn't).
export interface TaskPage {
  items: TaskItem[]
  totalCount: number
}

// Generic paginated API response shape shared by several list endpoints.
export interface PagedResponse<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

// Workspace (or workspace+project) summary counts and breakdowns shown on the home dashboard.
export interface Dashboard {
  projectCount: number
  activeTaskCount: number
  blockedTaskCount: number
  overdueTaskCount: number
  criticalTaskCount: number
  statusBreakdown: DashboardBreakdownItem[]
  priorityBreakdown: DashboardBreakdownItem[]
  deadlineBreakdown: DashboardBreakdownItem[]
  projectProgress: DashboardProjectProgress
  warnings: DashboardWarning[]
}

// A single labeled count used in dashboard/report breakdown charts (status, priority, deadline).
export interface DashboardBreakdownItem {
  label: string
  count: number
}

// Completed vs. total task counts and percentage for the current project selection.
export interface DashboardProjectProgress {
  completedTasks: number
  totalTasks: number
  completionPercentage: number
}

// A dashboard warning/notification (deadline risk, delivery risk, carry-over, etc.).
export interface DashboardWarning {
  type: string
  severity: 'info' | 'warning' | 'critical'
  title: string
  message: string
  projectId: string | null
  taskId: string | null
  dueDate: string | null
}

// Full workspace report payload for a date range: totals, breakdowns, and per-project/per-task detail.
export interface WorkspaceReport {
  workspaceId: string
  from: string | null
  to: string | null
  totalProjects: number
  activeProjects: number
  archivedProjects: number
  projectsDeliveredInRange: number
  totalTasks: number
  completedTasks: number
  activeTasks: number
  blockedTasks: number
  criticalTasks: number
  overdueTasks: number
  statusBreakdown: DashboardBreakdownItem[]
  priorityBreakdown: DashboardBreakdownItem[]
  deadlineBreakdown: DashboardBreakdownItem[]
  projects: WorkspaceReportProject[]
  tasks: WorkspaceReportTask[]
  notifications: DashboardWarning[]
}

// One project's summary row within a workspace report.
export interface WorkspaceReportProject {
  id: string
  name: string
  description: string | null
  deliveryDate: string | null
  isArchived: boolean
  archivedAt: string | null
  totalTasks: number
  completedTasks: number
  activeTasks: number
  blockedTasks: number
  overdueTasks: number
  criticalTasks: number
  completionPercentage: number
}

// One task row within a workspace report (a lighter-weight shape than TaskItem, used for reporting/export).
export interface WorkspaceReportTask {
  id: string
  projectId: string
  projectName: string
  assignedUserId: string | null
  title: string
  status: TaskStatus
  isBlocked: boolean
  dueDate: string | null
  createdAt: string
  completedAt: string | null
  priorityScore: number | null
  priorityBand: string | null
  deadlineHealth: DeadlineHealth
  tags: string[]
}

// A workspace the current user belongs to, with their role in it.
export interface Workspace {
  id: string
  name: string
  role: 'Owner' | 'Manager' | 'Member'
}

// A member of a workspace.
export interface WorkspaceMember {
  userId: string
  displayName: string
  email: string
  role: 'Owner' | 'Manager' | 'Member'
}

// An outstanding or resolved invitation to join a workspace.
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

// A task category scoped to a single project.
export interface ProjectCategory {
  id: string
  projectId: string
  name: string
}

// The lifecycle states a sprint moves through.
export type SprintStatus = 'Planned' | 'Active' | 'Completed' | 'Cancelled'

// A sprint (delivery cycle) belonging to a project.
export interface Sprint {
  id: string
  projectId: string
  name: string
  goal: string | null
  startDate: string
  endDate: string
  status: SprintStatus
  closedAt: string | null
}

// A project, including its categories and sprints.
export interface ProjectDetails {
  id: string
  name: string
  description: string | null
  targetDate: string | null
  isArchived: boolean
  archivedAt: string | null
  categories: ProjectCategory[]
  sprints: Sprint[]
}

// A single note attached to a task.
export interface TaskNote {
  id: string
  taskId: string
  authorId: string
  body: string
  createdAt: string
}

// The authenticated session returned after login/register/invitation-accept.
export interface AccountSession {
  userId: string
  displayName: string
  email: string
  accessToken: string
}

// The current user's basic account profile.
export interface AccountProfile {
  userId: string
  displayName: string
  email: string
}

// A single field-change entry in a task's activity/audit history.
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

// A TaskActivity entry enriched with the owning task/project context, used in the workspace-wide feed.
export interface WorkspaceActivity extends TaskActivity {
  action: string
  taskTitle: string
  projectId: string
  projectName: string
}

// Full super-admin operations snapshot: health, runtime config, reminders, backups, and recent logs.
export interface OperationsSummary {
  isSuperAdmin: boolean
  generatedAt: string
  overallHealth: string
  healthChecks: OperationHealthCheck[]
  runtime: OperationsRuntime
  reminderScheduler: ReminderScheduler
  databaseBackups: DatabaseBackupScheduler
  recentLogs: OperationLogRecord[]
}

// Result of a single backend health probe (API, database, email, etc.).
export interface OperationHealthCheck {
  name: string
  status: string
  description: string | null
  durationMilliseconds: number
}

// Deployment/runtime configuration values reported by the API for the operations page.
export interface OperationsRuntime {
  environment: string
  databaseProvider: string
  publicBaseUrl: string
  timeZoneId: string
  corsAllowedOrigins: string[]
  emailMode: string
  smtpEnabled: boolean
  reminderSchedulerEnabled: boolean
  logRetentionDays: number
  logMaxEntries: number
  logFileEnabled: boolean
  logDirectory: string
}

// Status of the background job that sends due-date/delivery reminder emails and My Day carry-overs.
export interface ReminderScheduler {
  enabled: boolean
  status: string
  intervalMinutes: number
  lastRunStartedAt: string | null
  lastRunCompletedAt: string | null
  nextRunAt: string | null
  lastTaskReminderCount: number
  lastProjectReminderCount: number
  lastTodoCarryOverCount: number
  lastEmailCount: number
  lastError: string | null
}

// Status of the background job that produces automatic database backups.
export interface DatabaseBackupScheduler {
  enabled: boolean
  status: string
  intervalHours: number
  lastRunStartedAt: string | null
  lastRunCompletedAt: string | null
  nextRunAt: string | null
  lastBackupFileName: string | null
  lastBackupSizeBytes: number
  lastError: string | null
}

// Metadata for one downloadable database backup file.
export interface DatabaseBackupFile {
  fileName: string
  sizeBytes: number
  createdAt: string
  lastModifiedAt: string
}

// A single structured application log entry surfaced on the operations page.
export interface OperationLogRecord {
  timestamp: string
  level: string
  category: string
  message: string
  exception: string | null
  eventId: string | null
  correlationId: string | null
}

// Platform-wide (super-admin) summary row for one workspace, across all owners.
export interface PlatformWorkspaceSummary {
  workspaceId: string
  workspaceName: string
  ownerId: string
  ownerName: string
  ownerEmail: string
  isSuspended: boolean
  suspendedAt: string | null
  managerCount: number
  memberCount: number
  projectCount: number
  sprintCount: number
  taskCount: number
}

// Platform-wide summary row for one project, used within a workspace's platform detail view.
export interface PlatformProjectSummary {
  projectId: string
  projectName: string
  isArchived: boolean
  sprintCount: number
  taskCount: number
}

// Full platform-admin drill-down detail for a single workspace: members, projects, and dashboard.
export interface PlatformWorkspaceDetail {
  workspaceId: string
  workspaceName: string
  isSuspended: boolean
  suspendedAt: string | null
  suspendedReason: string | null
  members: WorkspaceMember[]
  projects: PlatformProjectSummary[]
  dashboard: Dashboard
}

// Fetches a binary file (e.g. a database backup) from the API as a Blob, attaching auth headers.
async function downloadFile(path: string): Promise<Blob> {
  const response = await fetch(apiUrl(path), {
    headers: {
      ...identityHeaders(),
    },
  })
  if (!response.ok) {
    throw new Error(`The API returned ${response.status}. Check that the API server is running.`)
  }

  return response.blob()
}

// A single realtime event pushed over the workspace's server-sent-events stream.
export interface WorkspaceRealtimeEvent {
  eventType: string
  workspaceId: string
  entityType: string
  entityId: string | null
  actorId: string | null
  occurredAt: string
}

// A comment left on a personal My Day todo.
export interface PersonalTodoComment {
  id: string
  todoId: string
  body: string
  createdAt: string
}

// A personal (non-workspace) My Day todo item, possibly generated from a daily routine or carried over from an earlier date.
export interface PersonalTodo {
  id: string
  title: string
  todoDate: string
  originalTodoDate: string
  carriedOverFromDate: string | null
  notes: string | null
  priority: TodoPriority
  dailyRoutineId: string | null
  isGeneratedFromDailyRoutine: boolean
  isCompleted: boolean
  createdAt: string
  updatedAt: string
  completedAt: string | null
  comments: PersonalTodoComment[]
}

// Priority levels available for personal todos and daily routines.
export type TodoPriority = 'Low' | 'Medium' | 'High' | 'Critical'

// A recurring routine definition that auto-generates a My Day todo for each active business date.
export interface DailyRoutine {
  id: string
  title: string
  notes: string | null
  priority: TodoPriority
  startDate: string
  endDate: string | null
  isActive: boolean
  lastGeneratedDate: string | null
  createdAt: string
  updatedAt: string
}

// Base URL for the API, read from the Vite env and stripped of a trailing slash.
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

// Joins the configured API base URL with a request path, ensuring exactly one leading slash.
function apiUrl(path: string) {
  return `${apiBaseUrl}${path.startsWith('/') ? path : `/${path}`}`
}

// Builds the auth header(s) for a request: a bearer token if signed in, or a dev-only
// impersonation header in local development when no token is present.
function identityHeaders(): Record<string, string> {
  const accessToken = localStorage.getItem('todoapp_access_token')
  return accessToken
    ? { Authorization: `Bearer ${accessToken}` }
    : import.meta.env.DEV
      ? { 'X-User-Id': '30000000-0000-0000-0000-000000000001' }
      : {}
}

// Core JSON request helper used by every method on `api`: attaches auth headers, parses
// JSON responses, and converts non-OK responses (including RFC 7807 problem+json bodies)
// into thrown Errors with a user-facing message.
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(apiUrl(path), {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...identityHeaders(),
      ...init?.headers,
    },
  })
  const contentType = response.headers.get('content-type')
  const isJson = contentType?.includes('application/json') ||
    contentType?.includes('application/problem+json')
  if (!response.ok) {
    if (isJson) {
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

  if (!isJson) {
    throw new Error('The API returned an unexpected response. Check that the API server is running.')
  }

  return response.json() as Promise<T>
}

// Best-effort "wake up" ping for the API's lightweight liveness check. On a
// free hosting tier the backend can spin down after inactivity and take
// 20-50s to cold-start on the next real request; firing this the moment a
// visitor lands on the demo page lets that cold start happen in the
// background while they're still reading, rather than only starting once
// they click a role. Errors are swallowed - this is purely an optimization,
// never something a caller should need to handle or react to.
export function wakeApiServer() {
  fetch(apiUrl('/health/live')).catch(() => {})
}

// Opens a server-sent-events connection for a workspace and invokes `onEvent` for each
// realtime event received, until `signal` is aborted. Used to trigger near-instant UI
// refreshes when other users/tabs change workspace data.
export async function streamWorkspaceEvents(
  workspaceId: string,
  onEvent: (event: WorkspaceRealtimeEvent) => void,
  signal: AbortSignal,
) {
  const response = await fetch(
    apiUrl(`/api/v1/workspaces/${workspaceId}/events`),
    {
      headers: {
        Accept: 'text/event-stream',
        ...identityHeaders(),
      },
      signal,
    },
  )

  if (!response.ok || !response.body) {
    throw new Error('Realtime workspace updates could not be started.')
  }

  const reader = response.body
    .pipeThrough(new TextDecoderStream())
    .getReader()
  let buffer = ''

  while (!signal.aborted) {
    const { value, done } = await reader.read()
    if (done) break
    buffer += value
    const messages = buffer.split('\n\n')
    buffer = messages.pop() ?? ''

    for (const message of messages) {
      const data = message
        .split('\n')
        .find((line) => line.startsWith('data: '))
        ?.slice(6)
      if (!data) continue
      onEvent(JSON.parse(data) as WorkspaceRealtimeEvent)
    }
  }
}

// The full typed API client: one method per backend endpoint, each a thin wrapper around
// `request` (or `downloadFile` for binary responses) that builds the path/query/body.
export const api = {
  // GET the workspaces the current user belongs to.
  workspaces: () => request<Workspace[]>('/api/v1/workspaces'),
  // POST a new workspace.
  createWorkspace: (name: string) =>
    request<Workspace>('/api/v1/workspaces', {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  // PUT a rename of an existing workspace.
  updateWorkspace: (workspaceId: string, name: string) =>
    request<Workspace>(`/api/v1/workspaces/${workspaceId}`, {
      method: 'PUT',
      body: JSON.stringify({ name }),
    }),
  // DELETE a workspace permanently.
  deleteWorkspace: (workspaceId: string) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}`, {
      method: 'DELETE',
    }),
  // GET the members of a workspace.
  members: (workspaceId: string) =>
    request<WorkspaceMember[]>(`/api/v1/workspaces/${workspaceId}/members`),
  // DELETE (remove) a member from a workspace.
  removeMember: (workspaceId: string, userId: string) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/members/${userId}`, {
      method: 'DELETE',
    }),
  // PUT a new role for a workspace member.
  changeMemberRole: (
    workspaceId: string,
    userId: string,
    role: 'Manager' | 'Member',
  ) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/members/${userId}`, {
      method: 'PUT',
      body: JSON.stringify({ role }),
    }),
  // GET a workspace's pending/past invitations.
  invitations: (workspaceId: string) =>
    request<WorkspaceInvitation[]>(`/api/v1/workspaces/${workspaceId}/invitations`),
  // GET a page of a workspace's activity feed, optionally filtered by activity type.
  workspaceActivity: (
    workspaceId: string,
    type = 'All',
    pageNumber = 1,
    pageSize = 10,
  ) =>
    request<PagedResponse<WorkspaceActivity>>(
      `/api/v1/workspaces/${workspaceId}/activity?${new URLSearchParams({
        type,
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
      })}`,
    ),
  // POST a new invitation for a workspace.
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
  // DELETE (cancel) a pending invitation.
  cancelInvitation: (workspaceId: string, invitationId: string) =>
    request<WorkspaceInvitation>(
      `/api/v1/workspaces/${workspaceId}/invitations/${invitationId}`,
      { method: 'DELETE' },
    ),
  // GET the public details of an invitation by its token.
  invitation: (token: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}`),
  // POST acceptance of an invitation, creating the account/membership.
  acceptInvitation: (token: string, displayName: string, password: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}/accept`, {
      method: 'POST',
      body: JSON.stringify({ displayName, password }),
    }),
  // POST decline of an invitation.
  declineInvitation: (token: string) =>
    request<WorkspaceInvitation>(`/api/v1/invitations/${token}/decline`, {
      method: 'POST',
    }),
  // GET the dashboard summary for a workspace (optionally scoped to one project).
  dashboard: (workspaceId?: string, projectId?: string) =>
    request<Dashboard>(
      `/api/v1/dashboard?${new URLSearchParams({
        ...(workspaceId ? { workspaceId } : {}),
        ...(projectId ? { projectId } : {}),
      })}`,
    ),
  // GET the workspace report for an optional date range (and optional project scope).
  report: (
    workspaceId: string,
    from?: string,
    to?: string,
    projectId?: string,
  ) =>
    request<WorkspaceReport>(
      `/api/v1/workspaces/${workspaceId}/reports?${new URLSearchParams({
        ...(from ? { from } : {}),
        ...(to ? { to } : {}),
        ...(projectId ? { projectId } : {}),
      })}`,
    ),
  // GET all projects in a workspace.
  projects: (workspaceId: string) =>
    request<ProjectDetails[]>(`/api/v1/workspaces/${workspaceId}/projects`),
  // POST a new project within a workspace.
  createWorkspaceProject: (
    workspaceId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) =>
    request<ProjectDetails>(`/api/v1/workspaces/${workspaceId}/projects`, {
      method: 'POST',
      body: JSON.stringify({
        name,
        description: description || null,
        targetDate: deliveryDate,
      }),
    }),
  // PUT updated project fields (name/description/delivery date).
  updateProject: (
    projectId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) =>
    request<ProjectDetails>(`/api/v1/projects/${projectId}`, {
      method: 'PUT',
      body: JSON.stringify({
        name,
        description: description || null,
        targetDate: deliveryDate,
      }),
    }),
  // POST an archive action on a project (soft-close, not delete).
  archiveProject: (projectId: string) =>
    request<ProjectDetails>(`/api/v1/projects/${projectId}/archive`, {
      method: 'POST',
    }),
  // DELETE a project permanently.
  deleteProject: (projectId: string) =>
    request<boolean>(`/api/v1/projects/${projectId}`, {
      method: 'DELETE',
    }),
  // GET a single project (defaults to the seed development project).
  project: (projectId = developmentProjectId) =>
    request<ProjectDetails>(`/api/v1/projects/${projectId}`),
  // GET a page of tasks for a workspace, optionally filtered by search text, project, and sprint.
  tasks: (
    workspaceId: string,
    search = '',
    pageNumber = 1,
    pageSize = 10,
    projectId?: string,
    sprintId?: string,
  ) =>
    request<TaskPage>(
      `/api/v1/tasks?${new URLSearchParams({
        workspaceId,
        ...(projectId ? { projectId } : {}),
        ...(sprintId ? { sprintId } : {}),
        search,
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
      })}`,
    ),
  // POST a new task under a project, including its initial priority planning inputs.
  createTask: (
    projectId: string,
    title: string,
    dueDate: string,
    effort: number,
    businessValue: number,
    urgency: number,
    riskReduction: number,
    sprintId?: string,
  ) =>
    request<TaskItem>(`/api/v1/projects/${projectId}/tasks`, {
      method: 'POST',
      body: JSON.stringify({
        title,
        dueDate: dueDate || null,
        effort,
        businessValue,
        urgency,
        riskReduction,
        sprintId: sprintId || null,
      }),
    }),
  // GET a single task by id.
  task: (id: string) => request<TaskItem>(`/api/v1/tasks/${id}`),
  // PUT updated core task fields (title/due date/effort/sprint).
  updateTask: (
    id: string,
    title: string,
    dueDate: string,
    effort: number,
    sprintId?: string,
  ) =>
    request(`/api/v1/tasks/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title,
        dueDate: dueDate || null,
        effort,
        sprintId: sprintId || null,
      }),
    }),
  // DELETE a task permanently.
  deleteTask: (id: string) =>
    request<boolean>(`/api/v1/tasks/${id}`, {
      method: 'DELETE',
    }),
  // PUT updated priority planning inputs (business value/urgency/risk reduction/effort) for a task.
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
  // POST a named board-status transition action (e.g. 'ready', 'start', 'complete') for a task.
  transition: (id: string, action: string, body?: object) =>
    request(`/api/v1/tasks/${id}/${action}`, {
      method: 'POST',
      body: body ? JSON.stringify(body) : undefined,
    }),
  // POST a direct status change for a task, with an optional reason when blocking it.
  setStatus: (id: string, status: TaskStatus, blockedReason?: string) =>
    request(`/api/v1/tasks/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, blockedReason }),
    }),
  // PUT a task's assignee.
  assign: (id: string, userId: string) =>
    request(`/api/v1/tasks/${id}/assignment`, {
      method: 'PUT',
      body: JSON.stringify({ userId }),
    }),
  // DELETE a task's assignment (unassign).
  unassign: (id: string) =>
    request(`/api/v1/tasks/${id}/assignment`, { method: 'DELETE' }),
  // POST a new category under a project.
  createCategory: (projectId: string, name: string) =>
    request<ProjectCategory>(`/api/v1/projects/${projectId}/categories`, {
      method: 'POST',
      body: JSON.stringify({ name }),
    }),
  // POST a new sprint under a project.
  createSprint: (
    projectId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) =>
    request<Sprint>(`/api/v1/projects/${projectId}/sprints`, {
      method: 'POST',
      body: JSON.stringify({
        name,
        goal: goal || null,
        startDate,
        endDate,
      }),
    }),
  // PUT updated sprint fields (name/goal/dates).
  updateSprint: (
    projectId: string,
    sprintId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) =>
    request<Sprint>(`/api/v1/projects/${projectId}/sprints/${sprintId}`, {
      method: 'PUT',
      body: JSON.stringify({
        name,
        goal: goal || null,
        startDate,
        endDate,
      }),
    }),
  // POST a sprint status transition to Active.
  startSprint: (projectId: string, sprintId: string) =>
    request<Sprint>(`/api/v1/projects/${projectId}/sprints/${sprintId}/start`, {
      method: 'POST',
    }),
  // POST a sprint status transition to Completed.
  completeSprint: (projectId: string, sprintId: string) =>
    request<Sprint>(`/api/v1/projects/${projectId}/sprints/${sprintId}/complete`, {
      method: 'POST',
    }),
  // POST a sprint status transition to Cancelled.
  cancelSprint: (projectId: string, sprintId: string) =>
    request<Sprint>(`/api/v1/projects/${projectId}/sprints/${sprintId}/cancel`, {
      method: 'POST',
    }),
  // DELETE a sprint (its tasks are unassigned from the sprint, not deleted).
  deleteSprint: (projectId: string, sprintId: string) =>
    request<boolean>(`/api/v1/projects/${projectId}/sprints/${sprintId}`, {
      method: 'DELETE',
    }),
  // PUT a task's category (or clear it by passing null).
  updateCategory: (id: string, categoryId: string | null) =>
    request<string | null>(`/api/v1/tasks/${id}/category`, {
      method: 'PUT',
      body: JSON.stringify({ categoryId }),
    }),
  // POST a new tag onto a task.
  addTag: (id: string, tag: string) =>
    request<string[]>(`/api/v1/tasks/${id}/tags`, {
      method: 'POST',
      body: JSON.stringify({ tag }),
    }),
  // DELETE a tag from a task.
  removeTag: (id: string, tag: string) =>
    request<string[]>(`/api/v1/tasks/${id}/tags/${encodeURIComponent(tag)}`, {
      method: 'DELETE',
    }),
  // POST a new note onto a task.
  addNote: (id: string, body: string) =>
    request<TaskNote>(`/api/v1/tasks/${id}/notes`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  // GET a task's activity/audit history.
  activity: (id: string) =>
    request<TaskActivity[]>(`/api/v1/tasks/${id}/activity`),
  // GET a page of the current user's My Day todos for an optional date, with search.
  todos: (
    date?: string,
    search = '',
    pageNumber = 1,
    pageSize = 10,
  ) =>
    request<PagedResponse<PersonalTodo>>(
      `/api/v1/todos?${new URLSearchParams({
        ...(date ? { date } : {}),
        search,
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
      })}`,
    ),
  // GET all My Day todos falling within a date range (used by the calendar's My Day layer).
  todosRange: (from: string, to: string) =>
    request<PersonalTodo[]>(
      `/api/v1/todos/range?${new URLSearchParams({ from, to })}`,
    ),
  // POST a new personal todo.
  createTodo: (title: string, todoDate: string, notes: string, priority: TodoPriority) =>
    request<PersonalTodo>('/api/v1/todos', {
      method: 'POST',
      body: JSON.stringify({ title, todoDate, notes: notes || null, priority }),
    }),
  // PUT updated fields on a personal todo.
  updateTodo: (
    id: string,
    title: string,
    todoDate: string,
    notes: string,
    priority: TodoPriority,
  ) =>
    request<PersonalTodo>(`/api/v1/todos/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ title, todoDate, notes: notes || null, priority }),
    }),
  // POST marking a todo complete.
  completeTodo: (id: string) =>
    request<PersonalTodo>(`/api/v1/todos/${id}/complete`, {
      method: 'POST',
    }),
  // POST reopening a completed todo.
  reopenTodo: (id: string) =>
    request<PersonalTodo>(`/api/v1/todos/${id}/reopen`, {
      method: 'POST',
    }),
  // DELETE a personal todo permanently.
  deleteTodo: (id: string) =>
    request<boolean>(`/api/v1/todos/${id}`, {
      method: 'DELETE',
    }),
  // POST a new comment on a personal todo.
  addTodoComment: (id: string, body: string) =>
    request<PersonalTodo>(`/api/v1/todos/${id}/comments`, {
      method: 'POST',
      body: JSON.stringify({ body }),
    }),
  // GET a page of the current user's daily routines.
  dailyRoutines: (pageNumber = 1, pageSize = 10) =>
    request<PagedResponse<DailyRoutine>>(
      `/api/v1/todos/routines?${new URLSearchParams({
        pageNumber: String(pageNumber),
        pageSize: String(pageSize),
      })}`,
    ),
  // POST a new daily routine.
  createDailyRoutine: (
    title: string,
    notes: string,
    priority: TodoPriority,
    startDate: string,
    endDate: string,
  ) =>
    request<DailyRoutine>('/api/v1/todos/routines', {
      method: 'POST',
      body: JSON.stringify({
        title,
        notes: notes || null,
        priority,
        startDate,
        endDate: endDate || null,
      }),
    }),
  // PUT updated fields (including active/paused) on a daily routine.
  updateDailyRoutine: (
    id: string,
    title: string,
    notes: string,
    priority: TodoPriority,
    startDate: string,
    endDate: string,
    isActive: boolean,
  ) =>
    request<DailyRoutine>(`/api/v1/todos/routines/${id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title,
        notes: notes || null,
        priority,
        startDate,
        endDate: endDate || null,
        isActive,
      }),
    }),
  // DELETE a daily routine permanently.
  deleteDailyRoutine: (id: string) =>
    request<boolean>(`/api/v1/todos/routines/${id}`, {
      method: 'DELETE',
    }),
  // POST login credentials, returning a new account session.
  login: (email: string, password: string) =>
    request<AccountSession>('/api/v1/account/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),
  // GET the current authenticated user's account profile.
  me: () => request<AccountProfile>('/api/v1/account/me'),
  // PUT an updated email on the current user's profile.
  updateProfile: (email: string) =>
    request<AccountProfile>('/api/v1/account/profile', {
      method: 'PUT',
      body: JSON.stringify({ email }),
    }),
  // PUT a password change, verified against the current password.
  changePassword: (currentPassword: string, newPassword: string) =>
    request<boolean>('/api/v1/account/password', {
      method: 'PUT',
      body: JSON.stringify({ currentPassword, newPassword }),
    }),
  // POST a request to email the account a password-reset code.
  requestPasswordReset: (email: string) =>
    request<boolean>('/api/v1/account/password/reset/request', {
      method: 'POST',
      body: JSON.stringify({ email }),
    }),
  // POST a password reset using the emailed code/token.
  resetPasswordWithToken: (email: string, token: string, newPassword: string) =>
    request<boolean>('/api/v1/account/password/reset/confirm', {
      method: 'POST',
      body: JSON.stringify({ email, token, newPassword }),
    }),
  // GET the super-admin operations summary (health, runtime, reminders, backups, logs).
  operationsSummary: () =>
    request<OperationsSummary>('/api/v1/operations/summary'),
  // GET the list of available database backup files.
  operationBackups: () =>
    request<DatabaseBackupFile[]>('/api/v1/operations/backups'),
  // POST a request to create a new database backup immediately.
  createOperationBackup: () =>
    request<DatabaseBackupFile>('/api/v1/operations/backups', {
      method: 'POST',
    }),
  // GET (download) a specific database backup file as a Blob.
  downloadOperationBackup: (fileName: string) =>
    downloadFile(`/api/v1/operations/backups/${encodeURIComponent(fileName)}`),
  // GET the platform-wide list of workspaces (super-admin only).
  platformWorkspaces: () =>
    request<PlatformWorkspaceSummary[]>('/api/v1/platform/workspaces'),
  // GET the platform-admin detail view for a single workspace.
  platformWorkspaceDetail: (workspaceId: string) =>
    request<PlatformWorkspaceDetail>(`/api/v1/platform/workspaces/${workspaceId}`),
  // POST suspending a workspace (super-admin only), with an optional reason.
  suspendWorkspace: (workspaceId: string, reason: string) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/suspend`, {
      method: 'POST',
      body: JSON.stringify({ reason: reason || null }),
    }),
  // POST reactivating a previously suspended workspace (super-admin only).
  reactivateWorkspace: (workspaceId: string) =>
    request<boolean>(`/api/v1/workspaces/${workspaceId}/reactivate`, {
      method: 'POST',
    }),
  // POST a new account registration, creating the user and their first workspace.
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
