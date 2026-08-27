import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent, MouseEvent, ReactNode } from 'react'
import {
  DndContext, DragOverlay, KeyboardSensor, MouseSensor, TouchSensor,
  closestCenter, pointerWithin, useDraggable, useDroppable, useSensor, useSensors,
} from '@dnd-kit/core'
import type { CollisionDetection, DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import {
  Activity, AlertTriangle, Bell, CalendarDays, ChartBar, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, CircleGauge,
  Clock3, Columns3, Download, Eye, FolderPlus, HelpCircle, KeyRound, LayoutList, ListChecks, LogOut,
  Menu, MessageSquare, Pencil, Pin, Plus, Printer, Save, Search, Settings2, ShieldCheck,
  Tags, Trash2, UserPlus, UserRound, X,
} from 'lucide-react'
import { api, streamWorkspaceEvents } from './api'
import type {
  AccountSession, DailyRoutine, Dashboard, DashboardBreakdownItem, OperationHealthCheck, OperationsSummary, PersonalTodo, ProjectCategory, ProjectDetails,
  DatabaseBackupFile, PlatformProjectSummary, PlatformWorkspaceDetail, PlatformWorkspaceSummary, Sprint, TaskItem, TaskStatus, TodoPriority, Workspace, WorkspaceActivity, WorkspaceInvitation, WorkspaceMember, WorkspaceReport, WorkspaceReportTask,
} from './api'
import landingDashboard from './assets/landing-dashboard.png'
import './styles.css'

const statusLabels: Record<TaskStatus, string> = {
  Backlog: 'Backlog', Ready: 'Ready', InProgress: 'In progress',
  Blocked: 'Blocked', Completed: 'Completed',
}

const emptyDashboard: Dashboard = {
  projectCount: 0, activeTaskCount: 0, blockedTaskCount: 0,
  overdueTaskCount: 0, criticalTaskCount: 0,
  statusBreakdown: [],
  priorityBreakdown: [],
  deadlineBreakdown: [],
  projectProgress: { completedTasks: 0, totalTasks: 0, completionPercentage: 0 },
  warnings: [],
}

type View = 'home' | 'tasks' | 'myday' | 'routines' | 'projects' | 'sprints' | 'board' | 'reports' | 'calendar' | 'activity' | 'team' | 'profile' | 'operations' | 'backups' | 'platform'
type TaskDrilldown = 'all' | 'active' | 'critical' | 'blocked' | 'overdue'

const views: View[] = ['home', 'tasks', 'myday', 'routines', 'projects', 'sprints', 'board', 'reports', 'calendar', 'activity', 'team', 'profile', 'operations', 'backups', 'platform']
const todoPriorities: TodoPriority[] = ['Low', 'Medium', 'High', 'Critical']

// Parses the URL hash into a known View, defaulting to 'home' for empty/unknown values
// and mapping legacy hash aliases ('workspace', 'todos', 'my-day') to their current view.
function viewFromHash(hash: string): View {
  const value = hash.replace('#', '').toLowerCase()
  if (value === 'workspace' || value === '') return 'home'
  if (value === 'todos' || value === 'my-day') return 'myday'
  return views.includes(value as View) ? value as View : 'home'
}

// The public (unauthenticated) screens: landing plus every AuthPage/DemoLoginPage
// mode. Kept hash-routed just like View above, so the URL, browser back/forward,
// and page refresh all reflect which public screen (sign in, forgot password,
// enter reset code, view demo, etc.) is currently showing.
type PublicMode = 'landing' | 'login' | 'register' | 'demo' | 'forgot' | 'reset'
const publicModeHashes: Record<Exclude<PublicMode, 'landing'>, string> = {
  login: 'login',
  register: 'register',
  demo: 'demo',
  forgot: 'forgot-password',
  reset: 'reset-password',
}

// Parses the URL hash into a known PublicMode, defaulting to 'landing' for
// empty/unrecognized values (the inverse of publicModeHashes above).
function publicModeFromHash(hash: string): PublicMode {
  const value = hash.replace('#', '').toLowerCase()
  const match = (Object.entries(publicModeHashes) as [PublicMode, string][])
    .find(([, hashValue]) => hashValue === value)
  return match ? match[0] : 'landing'
}

interface UserProfile {
  displayName: string
  email: string
}

const defaultProfile: UserProfile = {
  displayName: 'Salisu Adeboye',
  email: 'salisu.adeboye@gmail.com',
}

const defaultAccount: AccountSession | null = null
const todayInput = new Date().toISOString().slice(0, 10)
const boardTaskPageSize = 100
type DashboardWarning = Dashboard['warnings'][number]
type NotificationItem = DashboardWarning & {
  id: string
  read: boolean
}

// Reads and JSON-parses a value from localStorage, shallow-merging it onto the fallback
// object so newly added fields still get sensible defaults; falls back silently on any error.
function readLocal<T>(key: string, fallback: T): T {
  try {
    const value = localStorage.getItem(key)
    return value ? { ...fallback, ...JSON.parse(value) } : fallback
  } catch {
    return fallback
  }
}

// Loads the set of task IDs the given user has pinned, stored per-user in localStorage.
function readPinnedTasks(userId: string) {
  if (!userId) return new Set<string>()
  try {
    const value = localStorage.getItem(`todoapp_pinned_tasks_${userId}`)
    return new Set<string>(value ? JSON.parse(value) : [])
  } catch {
    return new Set<string>()
  }
}

// Loads the set of project IDs the given user has pinned, stored per-user in localStorage.
function readPinnedProjects(userId: string) {
  if (!userId) return new Set<string>()
  try {
    const value = localStorage.getItem(`todoapp_pinned_projects_${userId}`)
    return new Set<string>(value ? JSON.parse(value) : [])
  } catch {
    return new Set<string>()
  }
}

// Builds a stable, deduplicated id for a dashboard warning by joining its identifying fields,
// used to track read/unread state across renders since warnings don't carry their own id.
function notificationId(warning: DashboardWarning) {
  return [
    warning.type,
    warning.taskId ?? '',
    warning.projectId ?? '',
    warning.title,
    warning.dueDate ?? '',
  ].join('|')
}

// Synthesizes a dashboard-style warning summarizing My Day todos that were carried over
// (rolled forward from an earlier date) into the selected date and are still incomplete.
function carryOverNotificationFromTodos(
  todos: PersonalTodo[],
  selectedDate: string,
): DashboardWarning[] {
  const carried = todos.filter((todo) =>
    todo.carriedOverFromDate &&
    todo.todoDate === selectedDate &&
    !todo.isCompleted)
  if (!carried.length) {
    return []
  }

  const oldestDate = carried
    .map((todo) => todo.carriedOverFromDate)
    .filter((date): date is string => Boolean(date))
    .sort()[0]
  const sampleTitles = carried
    .slice(0, 3)
    .map((todo) => todo.title)
    .join(', ')
  const moreCount = carried.length > 3
    ? `, plus ${carried.length - 3} more`
    : ''

  return [{
    type: 'PersonalTodoCarryOver',
    severity: 'warning',
    title: `${carried.length} My Day todo${carried.length === 1 ? '' : 's'} carried over`,
    message: `${sampleTitles}${moreCount} moved into today from ${oldestDate ? formatDate(oldestDate) : 'an earlier date'}.`,
    projectId: null,
    taskId: null,
    dueDate: selectedDate,
  }]
}

// Builds the localStorage key used to track which notifications a user has read, per workspace.
function notificationReadKey(userId: string, workspaceId: string) {
  return `todoapp_read_notifications_${userId || 'anonymous'}_${workspaceId || 'workspace'}`
}

// Builds the localStorage key used to track whether a user has already seen the onboarding dialog.
function onboardingSeenKey(userId: string) {
  return `todoapp_onboarding_seen_${userId || 'development'}`
}

// Loads the set of notification ids the user has already marked read for a given workspace.
function readStoredNotificationIds(userId: string, workspaceId: string) {
  try {
    const value = localStorage.getItem(notificationReadKey(userId, workspaceId))
    return new Set<string>(value ? JSON.parse(value) : [])
  } catch {
    return new Set<string>()
  }
}

// Maps each allowed board status transition (Backlog -> Ready -> InProgress -> ...) to the
// API action name and optional request body used to perform it when a card is dragged.
const boardTransitions: Partial<Record<TaskStatus, Partial<Record<TaskStatus, {
  action: string
  body?: object
}>>>> = {
  Backlog: { Ready: { action: 'ready' } },
  Ready: { InProgress: { action: 'start' } },
  InProgress: {
    Blocked: {
      action: 'block',
      body: { reason: 'Moved to Blocked from the delivery board.' },
    },
    Completed: { action: 'complete' },
  },
  Blocked: {
    Ready: { action: 'unblock' },
    InProgress: { action: 'resume' },
  },
  Completed: { Ready: { action: 'reopen' } },
}

// Determines whether the current user is permitted to drag a task from its current status
// to the target status, based on the allowed transition table plus role/assignment rules.
function canMoveTask(
  task: TaskItem,
  target: TaskStatus,
  currentUserId: string,
  workspaceRole?: Workspace['role'] | null,
) {
  if (!boardTransitions[task.status]?.[target]) return false
  const isAssignee = !!task.assignedUserId && task.assignedUserId === currentUserId
  const isCreator = !!task.createdByUserId && task.createdByUserId === currentUserId
  const isCoordinator = workspaceRole === 'Owner' || workspaceRole === 'Manager'

  if (task.status === 'Ready' && target === 'InProgress') return !task.assignedUserId || isAssignee || isCoordinator
  if (task.status === 'InProgress' && (target === 'Blocked' || target === 'Completed')) return isAssignee || isCoordinator
  if (task.status === 'Blocked' && target === 'Ready') return isAssignee || isCreator || isCoordinator
  if (task.status === 'Blocked' && target === 'InProgress') return isAssignee || isCoordinator
  if (task.status === 'Backlog' && target === 'Ready') return isCreator || isCoordinator || !task.createdByUserId
  if (task.status === 'Completed' && target === 'Ready') return isCreator || isAssignee

  return false
}

// Only workspace Owners and Managers are allowed to create/edit/delete projects.
function canManageProjects(workspaceRole?: Workspace['role'] | null) {
  return workspaceRole === 'Owner' || workspaceRole === 'Manager'
}

// Fetches all tasks for the board view by paging through the tasks API until every page
// has been retrieved, since the board needs the full result set rather than one page at a time.
async function loadBoardTasks(
  workspaceId: string,
  search: string,
  projectId: string,
  sprintId: string,
) {
  const firstPage = await api.tasks(
    workspaceId,
    search,
    1,
    boardTaskPageSize,
    projectId,
    sprintId)

  if (firstPage.items.length >= firstPage.totalCount) {
    return firstPage
  }

  const totalPages = Math.ceil(firstPage.totalCount / boardTaskPageSize)
  const remainingPages = await Promise.all(
    Array.from({ length: totalPages - 1 }, (_, index) =>
      api.tasks(
        workspaceId,
        search,
        index + 2,
        boardTaskPageSize,
        projectId,
        sprintId)),
  )

  return {
    totalCount: firstPage.totalCount,
    items: [
      ...firstPage.items,
      ...remainingPages.flatMap((page) => page.items),
    ],
  }
}

const activityTypes = [
  'All',
  'TaskCreated',
  'TaskRenamed',
  'StatusChanged',
  'DueDateChanged',
  'AssignmentChanged',
  'CategoryChanged',
  'TagAdded',
  'TagRemoved',
  'NoteAdded',
] as const

/** Renders the Taskora logo mark as an inline SVG, sized via the optional `size` prop. */
function BrandMark({ size = 30 }: { size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 32 32" role="img" aria-label="Taskora">
    <rect width="32" height="32" rx="7" fill="#147d68" />
    <rect x="7" y="19" width="4" height="8" rx="1.5" fill="#ffffff" fillOpacity=".55" />
    <rect x="14" y="14" width="4" height="13" rx="1.5" fill="#ffffff" fillOpacity=".8" />
    <rect x="21" y="9" width="4" height="18" rx="1.5" fill="#ffffff" />
  </svg>
}

/**
 * Root application component. Owns all top-level state (auth/session, workspace data,
 * tasks/todos/projects/sprints, dashboard, notifications, dialogs, and the active View)
 * and renders either the public/auth pages or the authenticated app shell with routing
 * driven by the URL hash (see `viewFromHash`). Most of the app's data fetching, mutation
 * handlers, and dialog open/close state live here and are passed down as props to the
 * page and dialog components defined later in this file.
 */
export default function App() {
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [taskTotal, setTaskTotal] = useState(0)
  const [todos, setTodos] = useState<PersonalTodo[]>([])
  const [todoTotal, setTodoTotal] = useState(0)
  const [todoDate, setTodoDate] = useState(todayInput)
  const [todoSearch, setTodoSearch] = useState('')
  const [todoPageNumber, setTodoPageNumber] = useState(1)
  const [todoLoading, setTodoLoading] = useState(false)
  const [todoError, setTodoError] = useState('')
  const todoPageSize = 10
  const [dailyRoutines, setDailyRoutines] = useState<DailyRoutine[]>([])
  const [dailyRoutineTotal, setDailyRoutineTotal] = useState(0)
  const [dailyRoutinePageNumber, setDailyRoutinePageNumber] = useState(1)
  const [dailyRoutineLoading, setDailyRoutineLoading] = useState(false)
  const [dailyRoutineError, setDailyRoutineError] = useState('')
  const dailyRoutinePageSize = 6
  const [dashboard, setDashboard] = useState(emptyDashboard)
  const [dashboardReport, setDashboardReport] = useState<WorkspaceReport | null>(null)
  const [workspaces, setWorkspaces] = useState<Workspace[]>([])
  const [workspace, setWorkspace] = useState<Workspace | null>(null)
  const [workspaceDeleteCandidate, setWorkspaceDeleteCandidate] =
    useState<Workspace | null>(null)
  const [workspaceDeleteBusy, setWorkspaceDeleteBusy] = useState(false)
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState(() =>
    localStorage.getItem('todoapp_workspace_id') ?? '')
  const [projects, setProjects] = useState<ProjectDetails[]>([])
  const [project, setProject] = useState<ProjectDetails | null>(null)
  const [projectDeleteCandidate, setProjectDeleteCandidate] =
    useState<ProjectDetails | null>(null)
  const [projectDeleteBusy, setProjectDeleteBusy] = useState(false)
  const [taskDeleteCandidate, setTaskDeleteCandidate] =
    useState<TaskItem | null>(null)
  const [taskDeleteBusy, setTaskDeleteBusy] = useState(false)
  const [todoDeleteCandidate, setTodoDeleteCandidate] =
    useState<PersonalTodo | null>(null)
  const [todoDeleteBusy, setTodoDeleteBusy] = useState(false)
  const [selectedProjectId, setSelectedProjectId] = useState(() =>
    localStorage.getItem('todoapp_project_id') ?? '')
  const [selectedSprintId, setSelectedSprintId] = useState(() =>
    localStorage.getItem('todoapp_sprint_id') ?? '')
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [invitations, setInvitations] = useState<WorkspaceInvitation[]>([])
  const [categories, setCategories] = useState<ProjectCategory[]>([])
  const [view, setView] = useState<View>(() => viewFromHash(window.location.hash))
  const [search, setSearch] = useState('')
  const [pageNumber, setPageNumber] = useState(1)
  const [drilldown, setDrilldown] = useState<TaskDrilldown>('all')
  const pageSize = 10
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [selectedTask, setSelectedTask] = useState<TaskItem | null>(null)
  const [quickNoteTask, setQuickNoteTask] = useState<TaskItem | null>(null)
  const [pinnedTaskIds, setPinnedTaskIds] = useState<Set<string>>(() =>
    readPinnedTasks(localStorage.getItem('todoapp_current_user_id') ?? ''))
  const [pinnedProjectIds, setPinnedProjectIds] = useState<Set<string>>(() =>
    readPinnedProjects(localStorage.getItem('todoapp_current_user_id') ?? ''))
  const [navOpen, setNavOpen] = useState(false)
  const [activity, setActivity] = useState<WorkspaceActivity[]>([])
  const [activityTotal, setActivityTotal] = useState(0)
  const [activityMore, setActivityMore] = useState<WorkspaceActivity[]>([])
  const [activityMorePage, setActivityMorePage] = useState(1)
  const [activityLoadingMore, setActivityLoadingMore] = useState(false)
  const activityPageSize = 10
  const [activityType, setActivityType] = useState<(typeof activityTypes)[number]>('All')
  const [profile, setProfile] = useState<UserProfile>(() =>
    readLocal('todoapp_profile', defaultProfile))
  const [account, setAccount] = useState<AccountSession | null>(() =>
    readLocal('todoapp_account', defaultAccount))
  const [currentUserId, setCurrentUserId] = useState(() =>
    localStorage.getItem('todoapp_current_user_id') ?? '')
  const [operations, setOperations] = useState<OperationsSummary | null>(null)
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  // Caps the Home page's deadline-warnings list so an active team with lots of
  // due/assigned/carry-over items doesn't get a wall of cards; "View all" reveals the rest.
  const [showAllDeadlineWarnings, setShowAllDeadlineWarnings] = useState(false)
  const [notifications, setNotifications] = useState<Dashboard['warnings']>([])
  const [todoNotifications, setTodoNotifications] = useState<Dashboard['warnings']>([])
  const [readNotificationIds, setReadNotificationIds] = useState<Set<string>>(() =>
    readStoredNotificationIds(
      localStorage.getItem('todoapp_current_user_id') ?? '',
      localStorage.getItem('todoapp_workspace_id') ?? ''))
  const [report, setReport] = useState<WorkspaceReport | null>(null)
  const [reportLoading, setReportLoading] = useState(false)
  const [reportFrom, setReportFrom] = useState(() => new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10))
  const [reportTo, setReportTo] = useState(todayInput)
  const [loggedOut, setLoggedOut] = useState(() =>
    localStorage.getItem('todoapp_logged_out') === 'true')
  const [notice, setNotice] = useState('')
  const [onboardingOpen, setOnboardingOpen] = useState(false)
  const [onboardingStep, setOnboardingStep] = useState(0)
  const [accountMenuOpen, setAccountMenuOpen] = useState(false)
  const inviteToken = inviteTokenFromPath()
  const activeUserId = currentUserId || account?.userId || 'development'

  const markOnboardingSeen = () => {
    localStorage.setItem(onboardingSeenKey(activeUserId), 'true')
    setOnboardingOpen(false)
  }

  // Re-read pinned tasks/projects from localStorage whenever the logged-in user changes,
  // since pins are stored per-user.
  useEffect(() => {
    setPinnedTaskIds(readPinnedTasks(currentUserId || account?.userId || ''))
    setPinnedProjectIds(readPinnedProjects(currentUserId || account?.userId || ''))
  }, [currentUserId, account?.userId])

  // Re-read which notifications have been marked read whenever the user or workspace changes,
  // since read-state is stored per-user-per-workspace.
  useEffect(() => {
    setReadNotificationIds(readStoredNotificationIds(
      currentUserId || account?.userId || '',
      workspace?.id ?? selectedWorkspaceId))
  }, [currentUserId, account?.userId, workspace?.id, selectedWorkspaceId])

  // Shows the onboarding dialog once per user, the first time they arrive while logged in.
  useEffect(() => {
    if (loggedOut || !activeUserId) return
    if (localStorage.getItem(onboardingSeenKey(activeUserId)) === 'true') return
    setOnboardingStep(0)
    setOnboardingOpen(true)
  }, [activeUserId, loggedOut])

  // Loads the full workspace snapshot: workspaces, projects/categories, dashboard summary,
  // report, current page of tasks (or all tasks when on the board view), members, invitations,
  // activity feed, current user profile, and operations summary. Also reconciles the selected
  // workspace/project/sprint against what's actually available. `silent` skips toggling the
  // page-level loading spinner, used for background polling refreshes.
  const load = async (options: { silent?: boolean } = {}) => {
    const silent = options.silent === true
    try {
      if (!silent) setLoading(true)
      setError('')
      const available = await api.workspaces()
      setWorkspaces(available)
      const selected = available.find((item) => item.id === selectedWorkspaceId) ?? available[0]
      if (!selected) throw new Error('No workspace membership was found.')
      if (selected.id !== selectedWorkspaceId) {
        setSelectedWorkspaceId(selected.id)
        localStorage.setItem('todoapp_workspace_id', selected.id)
      }
      const projects = await api.projects(selected.id)
      const selectedProject = projects.find((item) => item.id === selectedProjectId) ??
        projects[0] ??
        null
      const nextSprintId = selectedProject?.sprints.some((sprint) => sprint.id === selectedSprintId)
        ? selectedSprintId
        : ''
      if (nextSprintId !== selectedSprintId) {
        setSelectedSprintId(nextSprintId)
        localStorage.removeItem('todoapp_sprint_id')
      }
      if (selectedProject && selectedProject.id !== selectedProjectId) {
        setSelectedProjectId(selectedProject.id)
        localStorage.setItem('todoapp_project_id', selectedProject.id)
      } else if (!selectedProject) {
        setSelectedProjectId('')
        localStorage.removeItem('todoapp_project_id')
      }
      const [summary, workspaceSummary, reportSnapshot, page, workspaceMembers, workspaceInvitations, activityPage] = await Promise.all([
        api.dashboard(selected.id, selectedProject?.id),
        api.dashboard(selected.id),
        api.report(selected.id, undefined, undefined, selectedProject?.id),
        selectedProject
          ? view === 'board'
            ? loadBoardTasks(selected.id, search, selectedProject.id, nextSprintId)
            : api.tasks(selected.id, search, pageNumber, pageSize, selectedProject.id, nextSprintId)
          : Promise.resolve({ items: [], totalCount: 0 }),
        api.members(selected.id),
        selected.role === 'Owner' ? api.invitations(selected.id) : Promise.resolve([]),
        api.workspaceActivity(selected.id, activityType, 1, activityPageSize),
      ])
      const currentProfile = await api.me().catch(() => null)
      if (currentProfile) {
        const nextProfile = {
          displayName: currentProfile.displayName,
          email: currentProfile.email,
        }
        setCurrentUserId(currentProfile.userId)
        setProfile(nextProfile)
        localStorage.setItem('todoapp_current_user_id', currentProfile.userId)
        localStorage.setItem('todoapp_profile', JSON.stringify(nextProfile))
      }
      const operationsSummary = await api.operationsSummary().catch(() => null)
      setWorkspace(selected)
      setProjects(projects)
      setProject(selectedProject)
      setMembers(workspaceMembers)
      setInvitations(workspaceInvitations)
      setCategories(projects.flatMap((item) => item.categories))
      setDashboard(summary)
      setDashboardReport(reportSnapshot)
      setNotifications(workspaceSummary.warnings)
      setTasks(page.items)
      setTaskTotal(page.totalCount)
      setActivity(activityPage.items)
      setActivityTotal(activityPage.totalCount)
      setOperations(operationsSummary)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load workspace.')
    } finally {
      if (!silent) setLoading(false)
    }
  }

  // Loads the page of My Day todos for the given date/search/page, and derives any
  // carry-over notification for todos rolled forward from an earlier date.
  const loadTodos = async (
    date = todoDate,
    search = todoSearch,
    pageNumber = todoPageNumber,
  ) => {
    try {
      setTodoLoading(true)
      setTodoError('')
      const page = await api.todos(
        date,
        search,
        pageNumber,
        todoPageSize)
      setTodos(page.items)
      setTodoTotal(page.totalCount)
      setTodoNotifications(carryOverNotificationFromTodos(page.items, date))
    } catch (reason) {
      setTodoError(reason instanceof Error ? reason.message : 'Todos could not be loaded.')
    } finally {
      setTodoLoading(false)
    }
  }

  // Loads the current page of the user's daily routines.
  const loadDailyRoutines = async (
    pageNumber = dailyRoutinePageNumber,
  ) => {
    try {
      setDailyRoutineLoading(true)
      setDailyRoutineError('')
      const page = await api.dailyRoutines(
        pageNumber,
        dailyRoutinePageSize)
      setDailyRoutines(page.items)
      setDailyRoutineTotal(page.totalCount)
    } catch (reason) {
      setDailyRoutineError(
        reason instanceof Error
          ? reason.message
          : 'Daily routines could not be loaded.')
    } finally {
      setDailyRoutineLoading(false)
    }
  }

  // Reloads workspace data whenever the active view, pagination, search, workspace,
  // project, sprint, or activity filter changes.
  useEffect(() => { void load() }, [view, pageNumber, search, selectedWorkspaceId, selectedProjectId, selectedSprintId, activityType])
  // Resets the "load more" activity pagination whenever the workspace or activity filter changes.
  useEffect(() => {
    setActivityMore([])
    setActivityMorePage(1)
  }, [selectedWorkspaceId, activityType])
  // currentUserId is included so a fresh login (transitioning from '' to a real id)
  // re-triggers this fetch — without it, a brand-new session's first render fires this
  // effect before the access token is attached to any request, and since none of the
  // other dependencies change as a result of logging in, it would never retry and My
  // Day would be stuck showing a stale "session is no longer valid" error.
  useEffect(() => {
    if (loggedOut) return
    void loadTodos(todoDate, todoSearch, todoPageNumber)
  }, [todoDate, todoSearch, todoPageNumber, loggedOut, currentUserId])
  useEffect(() => {
    if (loggedOut) return
    void loadDailyRoutines(dailyRoutinePageNumber)
  }, [dailyRoutinePageNumber, loggedOut, currentUserId])
  // Polls the workspace snapshot every 15s in the background (silent, no spinner) so the
  // dashboard/board stay reasonably fresh even without a live event arriving.
  useEffect(() => {
    if (loading || loggedOut) return undefined
    const interval = window.setInterval(() => void load({ silent: true }), 15000)
    return () => window.clearInterval(interval)
  }, [loading, loggedOut, view, pageNumber, search, selectedWorkspaceId, selectedProjectId, selectedSprintId, activityType])
  // Subscribes to the workspace's server-sent event stream and triggers a debounced silent
  // reload whenever an event arrives, so changes from other users/tabs show up quickly.
  useEffect(() => {
    if (!selectedWorkspaceId || loading || loggedOut) return undefined

    const controller = new AbortController()
    let refreshTimer = 0
    const scheduleRefresh = () => {
      window.clearTimeout(refreshTimer)
      refreshTimer = window.setTimeout(
        () => void load({ silent: true }),
        150)
    }

    void streamWorkspaceEvents(
      selectedWorkspaceId,
      scheduleRefresh,
      controller.signal)
      .catch(() => {
        if (!controller.signal.aborted) {
          window.clearTimeout(refreshTimer)
        }
      })

    return () => {
      window.clearTimeout(refreshTimer)
      controller.abort()
    }
  }, [selectedWorkspaceId, loading, loggedOut, view, pageNumber, search, selectedProjectId, selectedSprintId, activityType])
  // Fetches the workspace report (and its notifications) whenever the Reports view is active
  // and the date range or workspace changes; clears the report when navigating away.
  useEffect(() => {
    if (view !== 'reports' || !workspace) {
      setReport(null)
      return undefined
    }

    let cancelled = false
    setReportLoading(true)
    api.report(workspace.id, reportFrom, reportTo)
      .then((nextReport) => {
        if (!cancelled) {
          setReport(nextReport)
          setNotifications(nextReport.notifications)
        }
      })
      .catch((reason) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Reports could not be loaded.')
      })
      .finally(() => {
        if (!cancelled) setReportLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [view, workspace?.id, reportFrom, reportTo])
  // Keeps `view` in sync with the URL hash for both back/forward navigation (popstate)
  // and direct hash edits, and runs once on mount to pick up the initial hash.
  useEffect(() => {
    const syncViewFromHash = () => setView(viewFromHash(window.location.hash))
    window.addEventListener('hashchange', syncViewFromHash)
    window.addEventListener('popstate', syncViewFromHash)
    syncViewFromHash()
    return () => {
      window.removeEventListener('hashchange', syncViewFromHash)
      window.removeEventListener('popstate', syncViewFromHash)
    }
  }, [])
  const visible = useMemo(() => filterTasksByDrilldown(tasks, drilldown), [tasks, drilldown])
  const filteredTaskTotal = drilldown === 'all' ? taskTotal : visible.length
  const totalPages = Math.max(1, Math.ceil(filteredTaskTotal / pageSize))
  // Merges workspace warnings and todo carry-over warnings into a single notification list,
  // tagging each with a stable id and whether the user has already read it.
  const notificationItems = useMemo<NotificationItem[]>(
    () => [...notifications, ...todoNotifications].map((warning) => {
      const id = notificationId(warning)
      return {
        ...warning,
        id,
        read: readNotificationIds.has(id),
      }
    }),
    [notifications, todoNotifications, readNotificationIds])
  const persistReadNotifications = (ids: Set<string>) => {
    localStorage.setItem(
      notificationReadKey(
        currentUserId || account?.userId || '',
        workspace?.id ?? selectedWorkspaceId),
      JSON.stringify([...ids]))
    setReadNotificationIds(ids)
  }
  const loadMoreActivity = async () => {
    if (activityLoadingMore) return
    if (activity.length + activityMore.length >= activityTotal) return
    setActivityLoadingMore(true)
    try {
      const nextPage = activityMorePage + 1
      const page = await api.workspaceActivity(
        selectedWorkspaceId, activityType, nextPage, activityPageSize)
      setActivityMore((current) => [...current, ...page.items])
      setActivityMorePage(nextPage)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'More activity could not be loaded.')
    } finally {
      setActivityLoadingMore(false)
    }
  }
  const markNotificationRead = (id: string) => {
    if (readNotificationIds.has(id)) return
    persistReadNotifications(new Set([...readNotificationIds, id]))
  }
  const markAllNotificationsRead = () => {
    persistReadNotifications(new Set([
      ...readNotificationIds,
      ...notificationItems.map((item) => item.id),
    ]))
  }
  const logout = () => {
    localStorage.removeItem('todoapp_access_token')
    localStorage.removeItem('todoapp_account')
    localStorage.removeItem('todoapp_current_user_id')
    localStorage.setItem('todoapp_logged_out', 'true')
    setAccount(null)
    setCurrentUserId('')
    setOperations(null)
    setTodoNotifications([])
    setLoggedOut(true)
    setNotice('You have been logged out of the browser session.')
    // Clears the hash (rather than leaving a stale authenticated-view hash
    // like #home) since the public landing page is about to show instead.
    window.history.replaceState(null, '', '/')
    setNavOpen(false)
  }
  const resetSession = () => {
    localStorage.removeItem('todoapp_access_token')
    localStorage.removeItem('todoapp_account')
    localStorage.removeItem('todoapp_current_user_id')
    localStorage.removeItem('todoapp_workspace_id')
    localStorage.removeItem('todoapp_logged_out')
    setAccount(null)
    setCurrentUserId('')
    setOperations(null)
    setLoggedOut(false)
    setSelectedWorkspaceId('')
    setNotice('Local browser session reset.')
    void load()
  }
  const authenticated = (session: AccountSession) => {
    localStorage.setItem('todoapp_access_token', session.accessToken)
    localStorage.setItem('todoapp_account', JSON.stringify(session))
    localStorage.setItem('todoapp_current_user_id', session.userId)
    localStorage.removeItem('todoapp_logged_out')
    setAccount(session)
    setCurrentUserId(session.userId)
    setLoggedOut(false)
    setProfile((current) => {
      const next = {
        ...current,
        displayName: session.displayName,
        email: session.email,
      }
      localStorage.setItem('todoapp_profile', JSON.stringify(next))
      return next
    })
    setNotice(`Signed in as ${session.displayName}.`)
    // Clears any leftover public-flow hash (#login, #forgot-password, etc.)
    // now that the authenticated app shell is about to render instead.
    window.history.replaceState(null, '', '#home')
    void load()
  }
  const openView = (next: View) => {
    setView(next)
    window.history.pushState(null, '', `#${next}`)
    setNavOpen(false)
    setAccountMenuOpen(false)
  }
  const switchWorkspace = (workspaceId: string) => {
    setSelectedWorkspaceId(workspaceId)
    localStorage.setItem('todoapp_workspace_id', workspaceId)
    setSelectedProjectId('')
    localStorage.removeItem('todoapp_project_id')
    setSelectedSprintId('')
    localStorage.removeItem('todoapp_sprint_id')
    setPageNumber(1)
    setDrilldown('all')
  }
  const switchProject = (projectId: string) => {
    setSelectedProjectId(projectId)
    localStorage.setItem('todoapp_project_id', projectId)
    setSelectedSprintId('')
    localStorage.removeItem('todoapp_sprint_id')
    setPageNumber(1)
    setDrilldown('all')
  }
  const switchSprint = (sprintId: string) => {
    setSelectedSprintId(sprintId)
    if (sprintId) localStorage.setItem('todoapp_sprint_id', sprintId)
    else localStorage.removeItem('todoapp_sprint_id')
    setPageNumber(1)
    setDrilldown('all')
  }
  const createProject = async (
    name: string,
    description: string,
    deliveryDate: string,
  ) => {
    if (!workspace) return
    try {
      setError('')
      const created = await api.createWorkspaceProject(
        workspace.id,
        name,
        description,
        deliveryDate,
      )
      setProjects((items) => [...items, created]
        .sort((left, right) => left.name.localeCompare(right.name)))
      setProject(created)
      setSelectedProjectId(created.id)
      setSelectedSprintId('')
      localStorage.setItem('todoapp_project_id', created.id)
      localStorage.removeItem('todoapp_sprint_id')
      setPageNumber(1)
      setNotice(`Project ${created.name} created.`)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Project could not be created.')
      throw reason
    }
  }
  const updateProject = async (
    projectId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) => {
    try {
      setError('')
      const updated = await api.updateProject(
        projectId,
        name,
        description,
        deliveryDate,
      )
      setProjects((items) => items.map((item) =>
        item.id === updated.id ? updated : item)
        .sort((left, right) => left.name.localeCompare(right.name)))
      setProject(updated)
      setCategories((items) => [
        ...items.filter((category) => category.projectId !== updated.id),
        ...updated.categories,
      ].sort((left, right) => left.name.localeCompare(right.name)))
      setNotice(`Project ${updated.name} updated.`)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Project could not be updated.')
      throw reason
    }
  }
  const archiveProject = async (projectId: string) => {
    try {
      setError('')
      const archived = await api.archiveProject(projectId)
      const remaining = projects.filter((item) => item.id !== projectId)
      setProjects(remaining)
      const next = remaining[0] ?? null
      setProject(next)
      setSelectedProjectId(next?.id ?? '')
      if (next) {
        localStorage.setItem('todoapp_project_id', next.id)
      } else {
        localStorage.removeItem('todoapp_project_id')
      }
      setPageNumber(1)
      setNotice(`Project ${archived.name} archived.`)
      await load()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Project could not be archived.')
      throw reason
    }
  }
  const requestProjectDelete = (projectId: string) => {
    const candidate = projects.find((item) => item.id === projectId)
    if (candidate) setProjectDeleteCandidate(candidate)
  }
  const confirmProjectDelete = async () => {
    if (!projectDeleteCandidate) return
    const projectToDelete = projectDeleteCandidate
    try {
      setProjectDeleteBusy(true)
      setError('')
      await api.deleteProject(projectToDelete.id)
      const remaining = projects.filter((item) => item.id !== projectToDelete.id)
      const next = remaining[0] ?? null
      setProjects(remaining)
      setProject(next)
      setTasks([])
      setCategories([])
      setSelectedProjectId(next?.id ?? '')
      setSelectedSprintId('')
      setPinnedProjectIds((current) => {
        const nextPinned = new Set(current)
        nextPinned.delete(projectToDelete.id)
        localStorage.setItem(
          `todoapp_pinned_projects_${currentUserId || account?.userId || ''}`,
          JSON.stringify(Array.from(nextPinned)))
        return nextPinned
      })
      if (next) {
        localStorage.setItem('todoapp_project_id', next.id)
      } else {
        localStorage.removeItem('todoapp_project_id')
      }
      localStorage.removeItem('todoapp_sprint_id')
      setPageNumber(1)
      setProjectDeleteCandidate(null)
      setNotice(`Project ${projectToDelete.name} deleted permanently.`)
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Project could not be deleted.')
      throw reason
    } finally {
      setProjectDeleteBusy(false)
    }
  }
  const createWorkspace = async (name: string) => {
    try {
      setError('')
      const created = await api.createWorkspace(name)
      setWorkspaces((items) => [...items.filter((item) => item.id !== created.id), created])
    setWorkspace(created)
    setProjects([])
    setProject(null)
    setTasks([])
    setDashboard(emptyDashboard)
    setSelectedWorkspaceId(created.id)
    setSelectedProjectId('')
    setSelectedSprintId('')
    localStorage.setItem('todoapp_workspace_id', created.id)
    localStorage.removeItem('todoapp_project_id')
    localStorage.removeItem('todoapp_sprint_id')
      setNotice(`Workspace ${created.name} created.`)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Workspace could not be created.')
      throw reason
    }
  }
  const updateWorkspace = async (workspaceId: string, name: string) => {
    try {
      setError('')
      const updated = await api.updateWorkspace(workspaceId, name)
      setWorkspaces((items) => items
        .map((item) => item.id === updated.id ? updated : item)
        .sort((left, right) => left.name.localeCompare(right.name)))
      setWorkspace((current) => current?.id === updated.id ? updated : current)
      setNotice(`Workspace ${updated.name} updated.`)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Workspace could not be updated.')
      throw reason
    }
  }
  const requestWorkspaceDelete = (workspaceId: string) => {
    const candidate = workspaces.find((item) => item.id === workspaceId)
    if (candidate) setWorkspaceDeleteCandidate(candidate)
  }
  const confirmWorkspaceDelete = async () => {
    if (!workspaceDeleteCandidate) return
    const workspaceToDelete = workspaceDeleteCandidate
    try {
      setWorkspaceDeleteBusy(true)
      setError('')
      await api.deleteWorkspace(workspaceToDelete.id)
      const remaining = workspaces.filter((item) => item.id !== workspaceToDelete.id)
      const next = remaining[0] ?? null
      setWorkspaces(remaining)
      setWorkspace(next)
      setProjects([])
      setProject(null)
      setTasks([])
      setCategories([])
      setDashboard(emptyDashboard)
      setDashboardReport(null)
      setNotifications([])
      setTodoNotifications([])
      setMembers([])
      setInvitations([])
      setActivity([])
      setActivityTotal(0)
      setActivityMore([])
      setActivityMorePage(1)
      setSelectedWorkspaceId(next?.id ?? '')
      setSelectedProjectId('')
      setSelectedSprintId('')
      if (next) {
        localStorage.setItem('todoapp_workspace_id', next.id)
      } else {
        localStorage.removeItem('todoapp_workspace_id')
      }
      localStorage.removeItem('todoapp_project_id')
      localStorage.removeItem('todoapp_sprint_id')
      setWorkspaceDeleteCandidate(null)
      setNotice(`Workspace ${workspaceToDelete.name} deleted permanently.`)
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Workspace could not be deleted.')
      throw reason
    } finally {
      setWorkspaceDeleteBusy(false)
    }
  }
  // Moves a task to a new board status optimistically (updating local state and the
  // dashboard counts immediately) then calls the API; rolls both back on failure.
  const moveTask = async (task: TaskItem, target: TaskStatus) => {
    if (target === task.status) return
    const previousTasks = tasks
    const previousDashboard = dashboard
    const userId = currentUserId || account?.userId || ''

    try {
      setError('')
      setTasks((items) => items.map((item) => item.id === task.id
        ? {
            ...item,
            status: target,
            isBlocked: target === 'Blocked',
            deadlineHealth: target === 'Completed' ? 'Completed' : item.deadlineHealth,
            assignedUserId: target === 'InProgress' && !item.assignedUserId
              ? userId
              : item.assignedUserId,
          }
        : item))
      setDashboard((current) => applyTaskMoveToDashboard(current, task, target))
      await api.setStatus(task.id, target)
      await load({ silent: true })
    } catch (reason) {
      setTasks(previousTasks)
      setDashboard(previousDashboard)
      setError(reason instanceof Error ? reason.message : 'The task could not be moved.')
      throw reason
    }
  }
  const openTaskEditor = async (task: TaskItem) => {
    setSelectedTask(task)
    try {
      setSelectedTask(await api.task(task.id))
    } catch {
      setSelectedTask(task)
    }
  }
  const openTaskNotes = async (task: TaskItem) => {
    setQuickNoteTask(task)
    try {
      setQuickNoteTask(await api.task(task.id))
    } catch {
      setQuickNoteTask(task)
    }
  }
  const refreshTaskNotes = async (taskId: string) => {
    const task = await api.task(taskId)
    setQuickNoteTask(task)
    await load({ silent: true })
  }
  const togglePinnedTask = (taskId: string) => {
    const userId = currentUserId || account?.userId || ''
    if (!userId) return
    setPinnedTaskIds((current) => {
      const next = new Set(current)
      if (next.has(taskId)) next.delete(taskId)
      else next.add(taskId)
      localStorage.setItem(
        `todoapp_pinned_tasks_${userId}`,
        JSON.stringify(Array.from(next)))
      return next
    })
  }
  const togglePinnedProject = (projectId: string) => {
    const userId = currentUserId || account?.userId || ''
    if (!userId) return
    setPinnedProjectIds((current) => {
      const next = new Set(current)
      if (next.has(projectId)) next.delete(projectId)
      else next.add(projectId)
      localStorage.setItem(
        `todoapp_pinned_projects_${userId}`,
        JSON.stringify(Array.from(next)))
      return next
    })
  }
  const openPinnedProject = (projectId: string) => {
    switchProject(projectId)
    openView('board')
  }
  const createSprint = async (
    projectId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => {
    try {
      setError('')
      const sprint = await api.createSprint(projectId, name, goal, startDate, endDate)
      setNotice(`Sprint ${sprint.name} created.`)
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint could not be created.')
      throw reason
    }
  }
  const updateSprint = async (
    projectId: string,
    sprintId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => {
    try {
      setError('')
      const sprint = await api.updateSprint(projectId, sprintId, name, goal, startDate, endDate)
      setNotice(`Sprint ${sprint.name} updated.`)
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint could not be updated.')
      throw reason
    }
  }
  const changeSprintStatus = async (
    projectId: string,
    sprintId: string,
    action: 'start' | 'complete' | 'cancel',
  ) => {
    try {
      setError('')
      const sprint = action === 'start'
        ? await api.startSprint(projectId, sprintId)
        : action === 'complete'
          ? await api.completeSprint(projectId, sprintId)
          : await api.cancelSprint(projectId, sprintId)
      setNotice(`Sprint ${sprint.name} ${action === 'start' ? 'started' : action === 'complete' ? 'completed' : 'cancelled'}.`)
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint status could not be changed.')
      throw reason
    }
  }
  const deleteSprint = async (projectId: string, sprintId: string) => {
    try {
      setError('')
      await api.deleteSprint(projectId, sprintId)
      setNotice('Sprint deleted. Any assigned tasks were moved back to no sprint.')
      await load({ silent: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint could not be deleted.')
      throw reason
    }
  }
  const deleteTask = (task: TaskItem) => {
    setTaskDeleteCandidate(task)
  }
  const confirmTaskDelete = async () => {
    if (!taskDeleteCandidate) return
    const taskToDelete = taskDeleteCandidate
    try {
      setTaskDeleteBusy(true)
      setError('')
      await api.deleteTask(taskToDelete.id)
      setTaskDeleteCandidate(null)
      setNotice(`Task ${taskToDelete.title} deleted.`)
      await load()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'The task could not be deleted.')
    } finally {
      setTaskDeleteBusy(false)
    }
  }
  const requestTodoDelete = (todo: PersonalTodo) => {
    setTodoDeleteCandidate(todo)
  }
  const confirmTodoDelete = async () => {
    if (!todoDeleteCandidate) return
    const todoToDelete = todoDeleteCandidate
    try {
      setTodoDeleteBusy(true)
      setTodoError('')
      await api.deleteTodo(todoToDelete.id)
      setTodoDeleteCandidate(null)
      setNotice(`Todo ${todoToDelete.title} deleted.`)
      await loadTodos(todoDate, todoSearch, todoPageNumber)
    } catch (reason) {
      setTodoError(reason instanceof Error ? reason.message : 'The todo could not be deleted.')
    } finally {
      setTodoDeleteBusy(false)
    }
  }

  // Routing gate: an invitation link always takes over the page regardless of auth state,
  // otherwise an unauthenticated user (or explicit logout) sees the public landing/auth page
  // instead of the app shell. Dev mode is allowed through without a stored token.
  if (inviteToken) {
    return <InvitationPage token={inviteToken} onAuthenticated={authenticated} />
  }

  if (loggedOut || (!import.meta.env.DEV && !localStorage.getItem('todoapp_access_token'))) {
    return <PublicAccessPage onAuthenticated={authenticated} />
  }

  return (
    <div className="app-shell">
      {navOpen && <button className="nav-backdrop" onClick={() => setNavOpen(false)} aria-label="Close navigation" />}
      <aside className={navOpen ? 'sidebar open' : 'sidebar'}>
        <div className="brand">
          <span className="brand-mark"><BrandMark /></span><strong>Taskora</strong>
          <button className="icon-button sidebar-close" onClick={() => setNavOpen(false)} aria-label="Close navigation"><X /></button>
        </div>
        <nav aria-label="Primary navigation">
          <button className={view === 'home' ? 'active' : ''} onClick={() => openView('home')}><CircleGauge size={18} /> Home</button>
          <button className={view === 'myday' ? 'active' : ''} onClick={() => openView('myday')}><ListChecks size={18} /> My Day</button>
          <button className={view === 'routines' ? 'active' : ''} onClick={() => openView('routines')}><Clock3 size={18} /> Daily Routines</button>
          <button className={view === 'tasks' ? 'active' : ''} onClick={() => openView('tasks')}><LayoutList size={18} /> Tasks</button>
          <button className={view === 'projects' ? 'active' : ''} onClick={() => openView('projects')}><FolderPlus size={18} /> Projects</button>
          <button className={view === 'sprints' ? 'active' : ''} onClick={() => openView('sprints')}><Clock3 size={18} /> Sprints</button>
          <button className={view === 'board' ? 'active' : ''} onClick={() => openView('board')}><Columns3 size={18} /> Board</button>
          <button className={view === 'reports' ? 'active' : ''} onClick={() => openView('reports')}><ChartBar size={18} /> Reports</button>
          <button className={view === 'calendar' ? 'active' : ''} onClick={() => openView('calendar')}><CalendarDays size={18} /> Calendar</button>
          <button className={view === 'activity' ? 'active' : ''} onClick={() => openView('activity')}><Activity size={18} /> Activity</button>
          <button className={view === 'team' ? 'active' : ''} onClick={() => openView('team')}><UserPlus size={18} /> Team</button>
          {operations?.isSuperAdmin && <>
            <button className={view === 'operations' ? 'active' : ''} onClick={() => openView('operations')}><ShieldCheck size={18} /> Operations</button>
            <button className={view === 'backups' ? 'active' : ''} onClick={() => openView('backups')}><Save size={18} /> Database Backups</button>
            <button className={view === 'platform' ? 'active' : ''} onClick={() => openView('platform')}><UserRound size={18} /> Platform</button>
          </>}
        </nav>
        <PinnedProjects
          projects={projects}
          pinnedProjectIds={pinnedProjectIds}
          selectedProjectId={project?.id ?? selectedProjectId}
          onOpen={openPinnedProject}
        />
      </aside>

      <main id="workspace">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setNavOpen(!navOpen)} aria-label="Toggle navigation"><Menu /></button>
          <div className="topbar-title"><h1>Workspace</h1></div>
          <WorkspaceSwitcher
            workspaces={workspaces}
            selectedWorkspaceId={workspace?.id ?? selectedWorkspaceId}
            isSuperAdmin={operations?.isSuperAdmin ?? false}
            onSwitch={switchWorkspace}
            onCreate={createWorkspace}
            onUpdate={updateWorkspace}
            onDelete={requestWorkspaceDelete}
          />
          <div className="topbar-search"><Search size={17} /><input value={search} onChange={(event) => { setSearch(event.target.value); setPageNumber(1) }} placeholder="Search tasks, projects..." aria-label="Search tasks and projects" /></div>
          {(view === 'tasks' || view === 'board') && <button className="primary topbar-page-action" disabled={!project} onClick={() => setDialogOpen(true)} title={project ? 'Create task' : 'Create a project first'}><Plus size={17} /> New task</button>}
          <NotificationBell
            notifications={notificationItems}
            open={notificationsOpen}
            onToggle={() => {
              setAccountMenuOpen(false)
              setNotificationsOpen((current) => !current)
            }}
            onClose={() => setNotificationsOpen(false)}
            onMarkRead={markNotificationRead}
            onMarkAllRead={markAllNotificationsRead}
            onViewAll={() => {
              setNotificationsOpen(false)
              openView('activity')
            }}
          />
          <div className="account-menu">
            <button
              className="topbar-avatar"
              onClick={() => {
                setNotificationsOpen(false)
                setAccountMenuOpen((open) => !open)
              }}
              aria-expanded={accountMenuOpen}
              aria-haspopup="menu"
              aria-label={`Open account menu for ${profile.displayName}`}
            >
              {initials(profile.displayName)}
            </button>
            {accountMenuOpen && <div className="account-dropdown" role="menu">
              <div className="account-summary">
                <span className="avatar">{initials(profile.displayName)}</span>
                <div>
                  <strong>{profile.displayName}</strong>
                  <small>{workspace?.role ?? 'Member'}{workspace ? `, ${workspace.name}` : ''}</small>
                </div>
              </div>
              <button role="menuitem" onClick={() => openView('profile')}><UserRound size={16} /> My profile</button>
              <button role="menuitem" onClick={() => {
                setAccountMenuOpen(false)
                setOnboardingStep(0)
                setOnboardingOpen(true)
              }}><HelpCircle size={16} /> Help tour</button>
              <button role="menuitem" className="danger-menu-action" onClick={logout}><LogOut size={16} /> Logout</button>
            </div>}
          </div>
        </header>

        {notice && <div className="success-state"><ShieldCheck /> <span>{notice}</span><button onClick={() => setNotice('')}>Dismiss</button></div>}
        {error && <div className="error-state"><AlertTriangle /> <span>{error}</span><button onClick={() => void load()}>Retry</button><button onClick={resetSession}>Reset session</button></div>}

        {view === 'home' && <>
          <section className="home-heading">
            <div>
              <p className="eyebrow">Workspace Health</p>
              <h2>{workspace?.name ?? 'Workspace'} overview</h2>
            </div>
            <span>{projects.length} project{projects.length === 1 ? '' : 's'} tracked</span>
          </section>
          <section className="metrics" aria-label="Portfolio health">
            <Metric label="Projects" value={dashboard.projectCount} detail={`${projects.filter((item) => item.archivedAt === null).length} active`} icon={<FolderPlus />} selected={false} onClick={() => openView('projects')} />
            <Metric label="Active work" value={dashboard.activeTaskCount} detail={`${dashboard.overdueTaskCount} overdue`} icon={<LayoutList />} selected={drilldown === 'active'} onClick={() => { setDrilldown('active'); setPageNumber(1); openView('tasks') }} />
            <Metric label="Critical" value={dashboard.criticalTaskCount} detail={dashboard.criticalTaskCount ? 'Needs attention' : 'No critical tasks'} icon={<AlertTriangle />} tone="danger" selected={drilldown === 'critical'} onClick={() => { setDrilldown('critical'); setPageNumber(1); openView('tasks') }} />
            <Metric label="Blocked" value={dashboard.blockedTaskCount} detail={dashboard.blockedTaskCount ? 'Workflow blocked' : 'No blocked work'} icon={<Columns3 />} tone="warn" selected={drilldown === 'blocked'} onClick={() => { setDrilldown('blocked'); setPageNumber(1); openView('tasks') }} />
          </section>

          <DashboardAnalytics dashboard={dashboard} report={dashboardReport} members={members} />
          <ProjectGovernance dashboard={dashboard} project={project} tasks={tasks} />

          {!!dashboard.warnings.length && <section className="deadline-warnings" aria-label="Deadline warnings">
            {(showAllDeadlineWarnings ? dashboard.warnings : dashboard.warnings.slice(0, 5)).map((warning) => <article className={`deadline-warning ${warning.severity}`} key={`${warning.type}-${warning.taskId ?? warning.projectId ?? warning.title}-${warning.dueDate}`}>
              <AlertTriangle size={18} />
              <div>
                <strong>{warning.title}</strong>
                <span>{warning.message}{warning.dueDate ? ` Deadline: ${new Date(`${warning.dueDate}T00:00:00`).toLocaleDateString()}.` : ''}</span>
              </div>
            </article>)}
            {!showAllDeadlineWarnings && dashboard.warnings.length > 5 && <button
              type="button"
              className="link-button deadline-warnings-view-all"
              onClick={() => setShowAllDeadlineWarnings(true)}
            >
              View all ({dashboard.warnings.length - 5} more)
            </button>}
          </section>}
        </>}

        {(view === 'tasks' || view === 'board') && <>
          <ProjectBar
            projects={projects}
            selectedProjectId={project?.id ?? selectedProjectId}
            workspaceRole={workspace?.role ?? null}
            pinnedProjectIds={pinnedProjectIds}
            selectedSprintId={selectedSprintId}
            onSwitch={switchProject}
            onSprintSwitch={switchSprint}
            onTogglePin={togglePinnedProject}
            onCreate={createProject}
            onUpdate={updateProject}
            onArchive={archiveProject}
            onDelete={requestProjectDelete}
          />
          <section className="work-area">
            <div className="toolbar">
              <div className="search"><Search size={17} /><input value={search} onChange={(e) => { setSearch(e.target.value); setPageNumber(1) }} placeholder="Search tasks" aria-label="Search tasks" /></div>
              <div className="segmented" aria-label="View">
                <button className={view === 'tasks' ? 'selected' : ''} onClick={() => openView('tasks')}><LayoutList size={16} /> List</button>
                <button className={view === 'board' ? 'selected' : ''} onClick={() => openView('board')}><Columns3 size={16} /> Board</button>
              </div>
            </div>
            {drilldown !== 'all' && <div className="task-filter-banner">
              <span>Showing {drilldownLabels[drilldown].toLowerCase()} tasks{view === 'tasks' ? ' on this page' : ''}.</span>
              <button onClick={() => setDrilldown('all')}>Clear filter</button>
            </div>}

            {loading ? <div className="loading">Loading workspace...</div> :
              view === 'tasks'
                ? <TaskList tasks={visible} categories={categories} sprints={project?.sprints ?? []} members={members} currentUserId={currentUserId || account?.userId || ''} onEdit={(task) => void openTaskEditor(task)} onDelete={(task) => void deleteTask(task)} />
                : <Board tasks={visible} categories={categories} sprints={project?.sprints ?? []} members={members} currentUserId={currentUserId || account?.userId || ''} pinnedTaskIds={pinnedTaskIds} onEdit={(task) => void openTaskEditor(task)} onDelete={(task) => void deleteTask(task)} onMove={moveTask} onNote={(task) => void openTaskNotes(task)} onTogglePin={togglePinnedTask} />}
            {!loading && view === 'tasks' && <Pagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={filteredTaskTotal}
              totalPages={totalPages}
              onPageChange={setPageNumber}
            />}
          </section>
        </>}
        {view === 'projects' && <ProjectsPage
          workspaceId={workspace?.id ?? selectedWorkspaceId}
          projects={projects}
          selectedProjectId={project?.id ?? selectedProjectId}
          workspaceRole={workspace?.role ?? null}
          pinnedProjectIds={pinnedProjectIds}
          onSwitch={switchProject}
          onOpenTasks={(projectId) => {
            switchProject(projectId)
            openView('tasks')
          }}
          onOpenSprints={(projectId) => {
            switchProject(projectId)
            openView('sprints')
          }}
          onTogglePin={togglePinnedProject}
          onCreate={createProject}
          onUpdate={updateProject}
          onArchive={archiveProject}
          onDelete={requestProjectDelete}
        />}
        {view === 'sprints' && <SprintsPage
          workspaceId={workspace?.id ?? selectedWorkspaceId}
          projects={projects}
          selectedProjectId={project?.id ?? selectedProjectId}
          workspaceRole={workspace?.role ?? null}
          pinnedProjectIds={pinnedProjectIds}
          onSwitch={switchProject}
          onOpenTasks={(projectId) => {
            switchProject(projectId)
            openView('tasks')
          }}
          onTogglePin={togglePinnedProject}
          onCreate={createProject}
          onUpdate={updateProject}
          onArchive={archiveProject}
          onDelete={requestProjectDelete}
          onCreateSprint={createSprint}
          onUpdateSprint={updateSprint}
          onChangeSprintStatus={changeSprintStatus}
          onDeleteSprint={deleteSprint}
        />}
        {view === 'myday' && <TodoPage
          todos={todos}
          totalCount={todoTotal}
          selectedDate={todoDate}
          search={todoSearch}
          pageNumber={todoPageNumber}
          pageSize={todoPageSize}
          loading={todoLoading}
          error={todoError}
          onDateChange={(date) => {
            setTodoDate(date)
            setTodoPageNumber(1)
          }}
          onSearchChange={(nextSearch) => {
            setTodoSearch(nextSearch)
            setTodoPageNumber(1)
          }}
          onPageChange={setTodoPageNumber}
          onReload={() => void loadTodos()}
          onCreate={async (title, date, notes, priority) => {
            setTodoError('')
            const created = await api.createTodo(title, date, notes, priority)
            setNotice(`Todo ${created.title} created.`)
            setTodoPageNumber(1)
            await loadTodos(date, todoSearch, 1)
          }}
          onUpdate={async (todo, title, date, notes, priority) => {
            setTodoError('')
            const updated = await api.updateTodo(todo.id, title, date, notes, priority)
            setTodos((items) => items.map((item) => item.id === todo.id ? updated : item))
            if (updated.todoDate !== todoDate) await loadTodos(todoDate, todoSearch, todoPageNumber)
            setNotice(`Todo ${updated.title} updated.`)
          }}
          onToggle={async (todo) => {
            setTodoError('')
            const updated = todo.isCompleted
              ? await api.reopenTodo(todo.id)
              : await api.completeTodo(todo.id)
            setTodos((items) => items.map((item) => item.id === todo.id ? updated : item))
          }}
          onDelete={requestTodoDelete}
          onAddComment={async (todo, body) => {
            setTodoError('')
            const updated = await api.addTodoComment(todo.id, body)
            setTodos((items) => items.map((item) => item.id === todo.id ? updated : item))
          }}
        />}
        {view === 'routines' && <DailyRoutinesPage
          dailyRoutines={dailyRoutines}
          totalCount={dailyRoutineTotal}
          pageNumber={dailyRoutinePageNumber}
          pageSize={dailyRoutinePageSize}
          loading={dailyRoutineLoading}
          error={dailyRoutineError}
          onDailyRoutinePageChange={setDailyRoutinePageNumber}
          onDailyRoutineReload={() => void loadDailyRoutines()}
          onCreateDailyRoutine={async (title, notes, priority, startDate, endDate) => {
            setDailyRoutineError('')
            const created = await api.createDailyRoutine(title, notes, priority, startDate, endDate)
            setNotice(`Daily routine ${created.title} created.`)
            setDailyRoutinePageNumber(1)
            await loadDailyRoutines(1)
            await loadTodos(todoDate, todoSearch, todoPageNumber)
          }}
          onUpdateDailyRoutine={async (routine, title, notes, priority, startDate, endDate, isActive) => {
            setDailyRoutineError('')
            const updated = await api.updateDailyRoutine(routine.id, title, notes, priority, startDate, endDate, isActive)
            setDailyRoutines((items) => items.map((item) => item.id === routine.id ? updated : item))
            setNotice(`Daily routine ${updated.title} updated.`)
          }}
          onDeleteDailyRoutine={async (routine) => {
            setDailyRoutineError('')
            await api.deleteDailyRoutine(routine.id)
            setNotice(`Daily routine ${routine.title} deleted.`)
            await loadDailyRoutines(dailyRoutinePageNumber)
          }}
        />}
        {view === 'calendar' && <CalendarPage
          workspaceId={workspace?.id ?? selectedWorkspaceId}
          projects={projects}
          members={members}
          isMember={workspace?.role === 'Member'}
        />}
        {view === 'activity' && <ActivityPage
          activity={[...activity, ...activityMore]}
          tasks={tasks}
          notifications={notificationItems}
          loading={loading}
          selectedType={activityType}
          onTypeChange={setActivityType}
          totalCount={activityTotal}
          hasMore={activity.length + activityMore.length < activityTotal}
          loadingMore={activityLoadingMore}
          onLoadMore={() => void loadMoreActivity()}
          members={members}
          currentUserId={currentUserId || account?.userId || ''}
          onMarkNotificationRead={markNotificationRead}
        />}
        {view === 'reports' && <ReportsPage
          dashboard={dashboard}
          report={report}
          loading={reportLoading}
          from={reportFrom}
          to={reportTo}
          onFromChange={setReportFrom}
          onToChange={setReportTo}
        />}
        {view === 'team' && <TeamPage
          workspace={workspace}
          members={members}
          invitations={invitations}
          currentUserId={currentUserId || account?.userId}
          onMemberRemoved={async (userId) => {
            if (!workspace) return
            await api.removeMember(workspace.id, userId)
            setNotice('Workspace member removed.')
            await load()
          }}
          onRoleChanged={async (userId, role) => {
            if (!workspace) return
            await api.changeMemberRole(workspace.id, userId, role)
            setNotice('Workspace role updated.')
            await load()
          }}
          onInvited={async (fullName, email, role) => {
            if (!workspace) return
            const invitation = await api.inviteMember(workspace.id, fullName, email, role)
            setNotice(`Invite link created for ${invitation.email}. They become a member after accepting it.`)
            await load()
          }}
          onInvitationCancelled={async (invitationId) => {
            if (!workspace) return
            await api.cancelInvitation(workspace.id, invitationId)
            setNotice('Workspace invitation cancelled.')
            await load()
          }}
        />}
        {view === 'profile' && <ProfilePage
          profile={profile}
          account={account}
          role={workspace?.role ?? null}
          onSave={async (next) => {
            const updated = await api.updateProfile(next.email)
            const profileUpdate = {
              displayName: updated.displayName,
              email: updated.email,
            }
            setProfile(profileUpdate)
            localStorage.setItem('todoapp_profile', JSON.stringify(profileUpdate))
            setAccount((current) => {
              if (!current) return current
              const updatedAccount = { ...current, email: updated.email }
              localStorage.setItem('todoapp_account', JSON.stringify(updatedAccount))
              return updatedAccount
            })
            setNotice('Profile updated.')
          }}
          onPasswordChanged={async (currentPassword, newPassword) => {
            await api.changePassword(currentPassword, newPassword)
            setNotice('Password changed.')
          }}
          onLogout={logout}
        />}
        {view === 'operations' && operations?.isSuperAdmin && <OperationsPage summary={operations} />}
        {view === 'backups' && operations?.isSuperAdmin && <DatabaseBackupsPage summary={operations} />}
        {view === 'platform' && operations?.isSuperAdmin && <PlatformPage />}
        <footer className="app-credit">Copyright 2026 - Developed by <a href="https://salisu.dev" target="_blank" rel="noreferrer">salisu.dev</a></footer>
      </main>
      {dialogOpen && project && <TaskDialog projectId={project.id} isMember={workspace?.role === 'Member'} members={members} categories={categories} sprints={project.sprints} onCategoryCreated={(category) => setCategories((items) => [...items, category].sort((left, right) => left.name.localeCompare(right.name)))} onClose={() => setDialogOpen(false)} onCreated={() => { setDialogOpen(false); void load() }} />}
      {selectedTask && project && <TaskEditor projectId={project.id} task={selectedTask} currentUserId={currentUserId || account?.userId || ''} workspaceRole={workspace?.role ?? null} isMember={workspace?.role === 'Member'} members={members} categories={categories} sprints={project.sprints} onCategoryCreated={(category) => setCategories((items) => [...items, category].sort((left, right) => left.name.localeCompare(right.name)))} onClose={() => setSelectedTask(null)} onSaved={() => { setSelectedTask(null); void load() }} />}
      {quickNoteTask && <QuickNoteDialog task={quickNoteTask} members={members} currentUserId={currentUserId || account?.userId || ''} onClose={() => setQuickNoteTask(null)} onSaved={(taskId) => void refreshTaskNotes(taskId)} />}
      {taskDeleteCandidate && <TaskDeleteDialog
        task={taskDeleteCandidate}
        busy={taskDeleteBusy}
        onClose={() => {
          if (!taskDeleteBusy) setTaskDeleteCandidate(null)
        }}
        onConfirm={() => void confirmTaskDelete()}
      />}
      {todoDeleteCandidate && <TodoDeleteDialog
        todo={todoDeleteCandidate}
        busy={todoDeleteBusy}
        onClose={() => {
          if (!todoDeleteBusy) setTodoDeleteCandidate(null)
        }}
        onConfirm={() => void confirmTodoDelete()}
      />}
      {projectDeleteCandidate && <ProjectDeleteDialog
        project={projectDeleteCandidate}
        busy={projectDeleteBusy}
        onClose={() => {
          if (!projectDeleteBusy) setProjectDeleteCandidate(null)
        }}
        onConfirm={() => void confirmProjectDelete()}
      />}
      {workspaceDeleteCandidate && <WorkspaceDeleteDialog
        workspace={workspaceDeleteCandidate}
        busy={workspaceDeleteBusy}
        onClose={() => {
          if (!workspaceDeleteBusy) setWorkspaceDeleteCandidate(null)
        }}
        onConfirm={() => void confirmWorkspaceDelete()}
      />}
      {onboardingOpen && <OnboardingDialog
        step={onboardingStep}
        onStepChange={setOnboardingStep}
        onSkip={markOnboardingSeen}
        onComplete={markOnboardingSeen}
        onClose={markOnboardingSeen}
      />}
    </div>
  )
}

// Derives up to two-letter initials from a display name, used for avatar badges; falls back to 'U'.
function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('') || 'U'
}

// Extracts the invitation token from a `/invite/{token}` URL path, or '' if not on that route.
function inviteTokenFromPath() {
  const match = window.location.pathname.match(/^\/invite\/([^/]+)$/i)
  return match ? decodeURIComponent(match[1]) : ''
}

const onboardingSlides = [
  {
    eyebrow: 'Welcome',
    title: 'Run delivery work from one workspace.',
    body: 'Taskora connects workspaces, projects, sprints, tasks, reports, notifications, and personal todos so the team can see the same delivery picture.',
    icon: <CircleGauge />,
  },
  {
    eyebrow: 'Workspace first',
    title: 'Everything follows the selected workspace.',
    body: 'Cards, boards, lists, projects, members, activity, reports, and notifications are scoped to the workspace you switch into.',
    icon: <UserPlus />,
  },
  {
    eyebrow: 'Projects',
    title: 'Create a project before creating tasks.',
    body: 'Projects are delivery containers with required delivery dates. Tasks must belong to an active project so work never floats without context.',
    icon: <FolderPlus />,
  },
  {
    eyebrow: 'Sprints',
    title: 'Plan short cycles with Sprint 1, Sprint 2, and more.',
    body: 'Use planned and active sprints to group project tasks into focused delivery windows, then drill into each sprint to see task status and deadlines.',
    icon: <Clock3 />,
  },
  {
    eyebrow: 'Board workflow',
    title: 'Move work from backlog to completion.',
    body: 'Use Backlog, Ready, In Progress, Blocked, and Completed to show what is waiting, what is picked up, what is stuck, and what is done.',
    icon: <Columns3 />,
  },
  {
    eyebrow: 'Visibility',
    title: 'Use reports and notifications to stay ahead.',
    body: 'Dashboard charts, reports, activity, due-date reminders, and in-app notifications help the team catch blockers and delivery risk early.',
    icon: <ChartBar />,
  },
]

/**
 * First-run onboarding modal that walks the user through `onboardingSlides` one at a time.
 * `step` is controlled by the parent; skipping, completing, or closing all mark onboarding
 * as seen (persisted in localStorage by the caller) so it won't show again for this user.
 */
function OnboardingDialog({
  step,
  onStepChange,
  onSkip,
  onComplete,
  onClose,
}: {
  step: number
  onStepChange: (step: number) => void
  onSkip: () => void
  onComplete: () => void
  onClose: () => void
}) {
  const current = onboardingSlides[step] ?? onboardingSlides[0]
  const isLast = step === onboardingSlides.length - 1

  return <div className="dialog-backdrop onboarding-backdrop" role="presentation">
    <dialog open className="onboarding-dialog" aria-labelledby="onboarding-title">
      <header>
        <div>
          <p className="eyebrow">{current.eyebrow}</p>
          <h2 id="onboarding-title">Welcome to Taskora</h2>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Close onboarding"><X /></button>
      </header>
      <section className="onboarding-body">
        <div className="onboarding-visual">
          <span>{current.icon}</span>
        </div>
        <div className="onboarding-copy">
          <span>Step {step + 1} of {onboardingSlides.length}</span>
          <h3>{current.title}</h3>
          <p>{current.body}</p>
        </div>
        <div className="onboarding-progress" aria-label="Onboarding progress">
          {onboardingSlides.map((slide, index) =>
            <button
              key={slide.eyebrow}
              className={index === step ? 'active' : ''}
              onClick={() => onStepChange(index)}
              aria-label={`Show onboarding step ${index + 1}`}
            />)}
        </div>
      </section>
      <footer className="onboarding-actions">
        <button className="secondary" onClick={onSkip}>Skip tour</button>
        <div>
          <button className="secondary" disabled={step === 0} onClick={() => onStepChange(Math.max(0, step - 1))}>Back</button>
          <button className="primary" onClick={() => isLast ? onComplete() : onStepChange(step + 1)}>
            {isLast ? 'Get started' : 'Next'}
          </button>
        </div>
      </footer>
    </dialog>
  </div>
}

/**
 * Unauthenticated entry point: shows the marketing landing page by default, or hands off
 * to `AuthPage`/`DemoLoginPage` once the user picks an action from the nav/hero. The current
 * screen is hash-routed (#login, #register, #demo, #forgot-password, #reset-password) so the
 * URL, browser back/forward, and page refresh all reflect which public screen is showing.
 */
function PublicAccessPage({ onAuthenticated }: { onAuthenticated: (session: AccountSession) => void }) {
  const [mode, setModeState] = useState<PublicMode>(() => publicModeFromHash(window.location.hash))

  // Updates the mode and pushes a matching hash entry (so browser back/forward works),
  // clearing the hash entirely for 'landing' rather than using a #landing placeholder.
  const setMode = (next: PublicMode) => {
    setModeState(next)
    window.history.pushState(null, '', next === 'landing' ? '/' : `#${publicModeHashes[next]}`)
  }

  // Keeps mode in sync with the URL hash for back/forward navigation and direct hash edits.
  useEffect(() => {
    const syncModeFromHash = () => setModeState(publicModeFromHash(window.location.hash))
    window.addEventListener('hashchange', syncModeFromHash)
    window.addEventListener('popstate', syncModeFromHash)
    return () => {
      window.removeEventListener('hashchange', syncModeFromHash)
      window.removeEventListener('popstate', syncModeFromHash)
    }
  }, [])

  if (mode === 'demo') {
    return <DemoLoginPage
      onBack={() => setMode('landing')}
      onAuthenticated={onAuthenticated}
    />
  }

  if (mode !== 'landing') {
    return <AuthPage
      mode={mode}
      onModeChange={setMode}
      onBack={() => setMode('landing')}
      onAuthenticated={onAuthenticated}
    />
  }

  return <main className="landing-page">
    <header className="landing-nav">
      <div className="brand"><span className="brand-mark"><BrandMark /></span><strong>Taskora</strong></div>
      <nav aria-label="Public navigation">
        <button className="secondary" onClick={() => setMode('login')}><KeyRound size={16} /> Sign in</button>
        <button className="demo-nav-button" onClick={() => setMode('demo')}><Eye size={16} /> View demo</button>
        <button className="primary" onClick={() => setMode('register')}><UserRound size={16} /> Create account</button>
      </nav>
    </header>

    <section className="landing-hero" aria-labelledby="landing-title">
      <div className="landing-copy">
        <p className="eyebrow">Workspace delivery platform</p>
        <h1 id="landing-title">Run project work, daily tasks, and delivery signals from one calm workspace.</h1>
        <p>
          Taskora combines project boards, priority scoring, reminders, reports,
          workspace roles, and personal todos in one production-style portfolio app.
        </p>
        <div className="landing-actions">
          <button className="primary" onClick={() => setMode('register')}><UserPlus size={17} /> Start a workspace</button>
          <button className="secondary" onClick={() => setMode('login')}><KeyRound size={17} /> Sign in</button>
        </div>
        <dl className="landing-proof">
          <div><dt>Role-based</dt><dd>Owners, managers, members</dd></div>
          <div><dt>Realtime</dt><dd>Board and dashboard updates</dd></div>
          <div><dt>Ops-ready</dt><dd>Health, logs, CI/CD</dd></div>
        </dl>
      </div>
      <figure className="landing-visual">
        <img src={landingDashboard} alt="Taskora dashboard preview showing workspace health cards, charts, and task board columns" />
      </figure>
    </section>

    <section className="landing-section" aria-label="Taskora capabilities">
      <article>
        <CircleGauge size={20} />
        <h2>Delivery workspace</h2>
        <p>Track projects, delivery dates, active work, blockers, overdue tasks, and release readiness by workspace.</p>
      </article>
      <article>
        <Columns3 size={20} />
        <h2>Smarter task flow</h2>
        <p>Move tasks through Backlog, Ready, In Progress, Blocked, and Completed with assignment-aware rules.</p>
      </article>
      <article>
        <Bell size={20} />
        <h2>Notifications</h2>
        <p>Use in-app alerts and SMTP email reminders for assignments, due dates, project delivery, and risks.</p>
      </article>
      <article>
        <ChartBar size={20} />
        <h2>Reports and operations</h2>
        <p>Review activity, date-range reports, health checks, logs, and deployment-ready configuration.</p>
      </article>
    </section>

    <footer className="landing-footer">
      <span>Developed by <a href="https://salisu.dev" target="_blank" rel="noreferrer">salisu.dev</a></span>
      <span>Copyright 2026</span>
    </footer>
  </main>
}

/**
 * Combined login/register/forgot-password/reset-password form. `mode` drives which fields
 * render and which API call `submit` makes; it's a controlled prop (owned by `PublicAccessPage`
 * and kept in sync with the URL hash there) rather than local state, so switching between
 * login/register/forgot/reset updates the address bar and works with browser back/forward.
 */
function AuthPage({
  mode,
  onModeChange,
  onBack,
  onAuthenticated,
}: {
  mode: 'login' | 'register' | 'forgot' | 'reset'
  onModeChange: (mode: 'login' | 'register' | 'forgot' | 'reset') => void
  onBack?: () => void
  onAuthenticated: (session: AccountSession) => void
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [resetEmail, setResetEmail] = useState('')

  // Handles all four auth form submissions (login, register, forgot, reset) based on `mode`.
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    setNotice('')
    const data = new FormData(event.currentTarget)
    try {
      if (mode === 'forgot') {
        const email = String(data.get('email')).trim()
        await api.requestPasswordReset(email)
        setResetEmail(email)
        setNotice('If that email exists, a 6-digit reset code has been sent.')
        onModeChange('reset')
        return
      }

      if (mode === 'reset') {
        const email = String(data.get('email')).trim()
        await api.resetPasswordWithToken(
          email,
          String(data.get('token')),
          String(data.get('password')),
        )
        setResetEmail(email)
        setNotice('Password reset. Sign in with your new password.')
        onModeChange('login')
        return
      }

      const session = mode === 'login'
        ? await api.login(String(data.get('email')), String(data.get('password')))
        : await api.register(
            String(data.get('displayName')),
            String(data.get('email')),
            String(data.get('password')),
            String(data.get('workspaceName')),
          )
      onAuthenticated(session)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Account access failed.')
    } finally {
      setBusy(false)
    }
  }

  return <main className="auth-shell">
    <section className="auth-panel">
      <div className="brand"><span className="brand-mark"><BrandMark /></span><strong>Taskora</strong></div>
      <div>
        <p className="eyebrow">Account access</p>
        <h1>{mode === 'login'
          ? 'Sign in'
          : mode === 'register'
            ? 'Create account'
            : mode === 'forgot'
              ? 'Reset password'
              : 'Enter reset code'}</h1>
      </div>
      {(mode === 'login' || mode === 'register') && <div className="segmented" aria-label="Account mode">
        <button className={mode === 'login' ? 'selected' : ''} onClick={() => { setError(''); setNotice(''); onModeChange('login') }}><KeyRound size={16} /> Login</button>
        <button className={mode === 'register' ? 'selected' : ''} onClick={() => { setError(''); setNotice(''); onModeChange('register') }}><UserRound size={16} /> Register</button>
      </div>}
      <form className="auth-form" onSubmit={(event) => void submit(event)}>
        {mode === 'register' && <>
          <label>Display name<input name="displayName" required maxLength={120} autoComplete="name" /></label>
          <label>Workspace name<input name="workspaceName" required maxLength={160} defaultValue="Portfolio workspace" /></label>
        </>}
        <label>Email<input key={`${mode}-${resetEmail}`} name="email" type="email" required autoComplete="email" defaultValue={resetEmail} /></label>
        {mode === 'reset' && <label>Reset code<input name="token" required inputMode="numeric" pattern="[0-9]{6}" maxLength={6} placeholder="6-digit code" /></label>}
        {mode !== 'forgot' && <label>{mode === 'reset' ? 'New password' : 'Password'}<input name="password" type="password" required minLength={8} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /></label>}
        {error && <p className="field-error">{error}</p>}
        {notice && <p className="field-success">{notice}</p>}
        <button className="primary" disabled={busy}>{busy ? 'Working...' : mode === 'login' ? 'Login' : mode === 'register' ? 'Register' : mode === 'forgot' ? 'Send reset code' : 'Reset password'}</button>
      </form>
      {mode === 'login' && <button className="link-button" type="button" onClick={() => { setError(''); setNotice(''); onModeChange('forgot') }}>Forgot password?</button>}
      {(mode === 'forgot' || mode === 'reset') && <button className="secondary" type="button" onClick={() => { setError(''); onModeChange('login') }}>Back to sign in</button>}
      {onBack && <button className="secondary" onClick={onBack}>Back to overview</button>}
    </section>
  </main>
}

// Fixed demo credentials for the four "View demo" personas — kept in sync
// with PublicDemoSeeder.cs's OwnerEmail/ManagerEmail/MemberEmail/
// SuperAdminEmail/Password constants. This is a fully separate, isolated
// demo workspace with its own "demo-" prefixed emails and id namespace —
// it must never share an id or email with DevelopmentDataSeeder's data.
// `detail` deliberately names real seeded projects/data rather than generic
// role-permission language, so the picker reads as "here's who's on this
// team" rather than a feature-comparison chart.
const demoRoles = [
  {
    key: 'owner',
    name: 'Demo Owner',
    avatarLabel: 'OW',
    role: 'Owner',
    pillClass: 'owner',
    email: 'demo-owner@example.com',
    detail: 'Owns the workspace and all three projects — Portfolio launch, Client onboarding sprint, and the archived Discovery phase.',
  },
  {
    key: 'manager',
    name: 'Demo Manager',
    avatarLabel: 'MG',
    role: 'Manager',
    pillClass: 'manager',
    email: 'demo-manager@example.com',
    detail: 'Running the Client onboarding sprint and the deployment checklist in Portfolio launch.',
  },
  {
    key: 'member',
    name: 'Demo Member',
    avatarLabel: 'MB',
    role: 'Member',
    pillClass: '',
    email: 'demo-member@example.com',
    detail: 'Has tasks on the active sprint, a My Day list, and a daily standup routine.',
  },
  {
    key: 'superadmin',
    name: 'Demo Super Admin',
    avatarLabel: 'SA',
    role: 'Super Admin',
    pillClass: 'superadmin',
    email: 'demo-superadmin@example.com',
    detail: 'Everything a Member sees, plus Platform, Operations, and Database Backups.',
  },
] as const
const demoPassword = 'TaskoraDemo123!'

/**
 * Public "View demo" page: lets a visitor log straight into the shared, seeded
 * demo workspace as one of four fixed personas (Super Admin/Owner/Manager/
 * Member), using the same `api.login` call and session handoff a normal sign-in
 * uses — no special auth path, just well-known published credentials.
 */
function DemoLoginPage({
  onBack,
  onAuthenticated,
}: {
  onBack: () => void
  onAuthenticated: (session: AccountSession) => void
}) {
  const [busyKey, setBusyKey] = useState<string | null>(null)
  const [error, setError] = useState('')

  const loginAs = async (role: typeof demoRoles[number]) => {
    setBusyKey(role.key)
    setError('')
    try {
      const session = await api.login(role.email, demoPassword)
      onAuthenticated(session)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Demo login failed.')
    } finally {
      setBusyKey(null)
    }
  }

  return <main className="auth-shell">
    <section className="auth-panel demo-panel">
      <div className="brand"><span className="brand-mark"><BrandMark /></span><strong>Taskora</strong></div>
      <div>
        <p className="eyebrow">View demo</p>
        <h1>Pick who you want to be</h1>
        <p className="demo-intro">
          Everyone below works in the same shared demo workspace. Log in as any of them —
          nothing you do here touches a real account.
        </p>
      </div>
      {error && <p className="field-error">{error}</p>}
      <div className="demo-role-list">
        {demoRoles.map((role) => <button
          key={role.key}
          type="button"
          className={`demo-role-row${busyKey === role.key ? ' demo-role-row-busy' : ''}`}
          disabled={busyKey !== null}
          onClick={() => void loginAs(role)}
        >
          <span className={`avatar demo-role-avatar ${role.pillClass || 'member'}`}>{role.avatarLabel}</span>
          <span className="demo-role-text">
            <span className="demo-role-name">
              {role.name}
              <span className={`role-pill ${role.pillClass}`}>{role.role}</span>
            </span>
            <small>{role.detail}</small>
          </span>
          {busyKey === role.key
            ? <span className="demo-role-status">Signing in...</span>
            : <ChevronRight size={18} className="demo-role-arrow" />}
        </button>)}
      </div>
      <button className="secondary" onClick={onBack}>Back to overview</button>
    </section>
  </main>
}

/**
 * Landing page for a `/invite/{token}` link. Loads the invitation details on mount, then
 * lets the visitor accept (signing them in/up and joining the workspace) or decline.
 */
function InvitationPage({
  token,
  onAuthenticated,
}: {
  token: string
  onAuthenticated: (session: AccountSession) => void
}) {
  const [invitation, setInvitation] = useState<WorkspaceInvitation | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [declined, setDeclined] = useState(false)

  // Loads the invitation details for display whenever the token in the URL changes.
  useEffect(() => {
    api.invitation(token)
      .then(setInvitation)
      .catch((reason) => setError(
        reason instanceof Error ? reason.message : 'Invitation could not be loaded.'))
  }, [token])

  const accept = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    const data = new FormData(event.currentTarget)
    try {
      await api.acceptInvitation(
        token,
        String(data.get('displayName')),
        String(data.get('password')),
      )
      const session = await api.login(
        invitation?.email ?? String(data.get('email')),
        String(data.get('password')),
      )
      window.history.replaceState(null, '', '/')
      onAuthenticated(session)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Invitation could not be accepted.')
    } finally {
      setBusy(false)
    }
  }

  const decline = async () => {
    setBusy(true)
    setError('')
    try {
      await api.declineInvitation(token)
      setDeclined(true)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Invitation could not be declined.')
    } finally {
      setBusy(false)
    }
  }

  return <main className="auth-shell">
    <section className="auth-panel">
      <div className="brand"><span className="brand-mark"><BrandMark /></span><strong>Taskora</strong></div>
      {declined ? <>
        <div><p className="eyebrow">Workspace invitation</p><h1>Invitation declined</h1></div>
        <p className="muted">The workspace owner can send a new invitation later.</p>
      </> : <>
        <div>
          <p className="eyebrow">Workspace invitation</p>
          <h1>{invitation ? invitation.workspaceName : 'Loading invitation'}</h1>
        </div>
        {invitation && <p className="muted">{invitation.fullName} was invited as {invitation.role}. This invite expires {new Date(invitation.expiresAt).toLocaleDateString()}.</p>}
        <form className="auth-form" onSubmit={(event) => void accept(event)}>
          <label>Display name<input name="displayName" required maxLength={120} defaultValue={invitation?.fullName ?? ''} /></label>
          <label>Email<input name="email" type="email" required readOnly value={invitation?.email ?? ''} /></label>
          <label>Password<input name="password" type="password" required minLength={8} autoComplete="new-password" /></label>
          {error && <p className="field-error">{error}</p>}
          <footer className="invite-actions">
            <button type="button" className="secondary" disabled={busy || !invitation} onClick={() => void decline()}>Decline</button>
            <button className="primary" disabled={busy || !invitation}>{busy ? 'Working...' : 'Accept invitation'}</button>
          </footer>
        </form>
      </>}
    </section>
  </main>
}

/**
 * Topbar dropdown for switching between the user's workspaces, with inline create/rename/
 * delete forms swapped in for the select control depending on `creating`/`editing` state.
 * Rename/delete are only available to Owners (or a super admin).
 */
function WorkspaceSwitcher({
  workspaces,
  selectedWorkspaceId,
  isSuperAdmin,
  onSwitch,
  onCreate,
  onUpdate,
  onDelete,
}: {
  workspaces: Workspace[]
  selectedWorkspaceId: string
  isSuperAdmin: boolean
  onSwitch: (workspaceId: string) => void
  onCreate: (name: string) => Promise<void>
  onUpdate: (workspaceId: string, name: string) => Promise<void>
  onDelete: (workspaceId: string) => void
}) {
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState(false)
  const [name, setName] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const selectedWorkspace = workspaces.find((item) => item.id === selectedWorkspaceId) ?? null
  const canManage = selectedWorkspace?.role === 'Owner' || isSuperAdmin
  const canDelete = canManage && workspaces.length > 1

  if (creating) {
    return <form className="workspace-create" onSubmit={(event) => {
      event.preventDefault()
      if (!name.trim()) return
      setBusy(true)
      setError('')
      onCreate(name.trim())
        .then(() => {
          setName('')
          setCreating(false)
        })
        .catch((reason) => setError(
          reason instanceof Error ? reason.message : 'Workspace could not be created.'))
        .finally(() => setBusy(false))
    }}>
      <input aria-label="Workspace name" value={name} onChange={(event) => setName(event.target.value)} maxLength={160} autoFocus />
      <button className="primary" disabled={busy}><Plus size={16} /> {busy ? 'Creating...' : 'Create'}</button>
      <button type="button" className="secondary" disabled={busy} onClick={() => setCreating(false)}>Cancel</button>
      {error && <p className="field-error">{error}</p>}
    </form>
  }

  if (editing && selectedWorkspace && canManage) {
    return <form className="workspace-create" onSubmit={(event) => {
      event.preventDefault()
      if (!name.trim()) return
      setBusy(true)
      setError('')
      onUpdate(selectedWorkspace.id, name.trim())
        .then(() => {
          setName('')
          setEditing(false)
        })
        .catch((reason) => setError(
          reason instanceof Error ? reason.message : 'Workspace could not be updated.'))
        .finally(() => setBusy(false))
    }}>
      <input aria-label="Workspace name" value={name} onChange={(event) => setName(event.target.value)} maxLength={160} autoFocus />
      <button className="primary" disabled={busy}><Save size={16} /> {busy ? 'Saving...' : 'Save'}</button>
      <button type="button" className="secondary" disabled={busy} onClick={() => setEditing(false)}>Cancel</button>
      {error && <p className="field-error">{error}</p>}
    </form>
  }

  return <div className="workspace-switcher">
    <label aria-label="Workspace">
      <select value={selectedWorkspaceId} onChange={(event) => onSwitch(event.target.value)}>
        {workspaces.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}
      </select>
      <ChevronDown />
    </label>
    <button className="topbar-create" onClick={() => setCreating(true)} aria-label="Create workspace"><Plus size={20} /></button>
    {canManage && selectedWorkspace && <button
      className="icon-button workspace-action"
      onClick={() => {
        setName(selectedWorkspace.name)
        setEditing(true)
      }}
      aria-label={`Edit ${selectedWorkspace.name}`}
      title="Edit workspace name"
    ><Pencil /></button>}
    {canDelete && selectedWorkspace && <button
      className="icon-button workspace-action danger-action"
      onClick={() => onDelete(selectedWorkspace.id)}
      aria-label={`Delete ${selectedWorkspace.name}`}
      title="Delete workspace"
    ><Trash2 /></button>}
  </div>
}

/** Sidebar list of up to 4 pinned projects (with a delivery-status dot), with a count of any extra pinned projects. */
function PinnedProjects({
  projects,
  pinnedProjectIds,
  selectedProjectId,
  onOpen,
}: {
  projects: ProjectDetails[]
  pinnedProjectIds: Set<string>
  selectedProjectId: string
  onOpen: (projectId: string) => void
}) {
  const pinned = projects.filter((project) => pinnedProjectIds.has(project.id))
  const visiblePinned = pinned.slice(0, 4)
  const remainingPinned = pinned.length - visiblePinned.length

  return <section className="pinned-projects" aria-label="Pinned projects">
    <h2>Pinned projects</h2>
    {visiblePinned.length ? visiblePinned.map((project) => {
      const delivery = deliveryStatus(project.targetDate)
      return <button
        key={project.id}
        className={project.id === selectedProjectId ? 'active' : ''}
        onClick={() => onOpen(project.id)}
      >
        <span className={`project-dot ${delivery?.tone ?? 'healthy'}`} />
        <span>{project.name}</span>
      </button>
    }) : <p>Pin important projects from the Projects page.</p>}
    {remainingPinned > 0 && <p>+{remainingPinned} more pinned project{remainingPinned === 1 ? '' : 's'}</p>}
  </section>
}

/**
 * Header bar shown on the Tasks/Board pages for picking the active project and sprint,
 * with inline create/edit project forms, pin toggling, and archive/delete actions gated
 * by `canCreateProject` (Owner/Manager only for creation; parent gates archive/delete similarly).
 */
function ProjectBar({
  projects,
  selectedProjectId,
  workspaceRole,
  pinnedProjectIds,
  selectedSprintId,
  onSwitch,
  onSprintSwitch,
  onTogglePin,
  onAddSprint,
  onCreate,
  onUpdate,
  onArchive,
  onDelete,
}: {
  projects: ProjectDetails[]
  selectedProjectId: string
  workspaceRole: Workspace['role'] | null
  pinnedProjectIds?: Set<string>
  selectedSprintId?: string
  onSwitch: (projectId: string) => void
  onSprintSwitch?: (sprintId: string) => void
  onTogglePin?: (projectId: string) => void
  onAddSprint?: () => void
  onCreate: (
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onUpdate: (
    projectId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onArchive: (projectId: string) => Promise<void>
  onDelete: (projectId: string) => void
}) {
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const canCreateProject = workspaceRole === 'Owner' || workspaceRole === 'Manager'
  const selectedProject = projects.find((item) => item.id === selectedProjectId) ?? null
  const delivery = deliveryStatus(selectedProject?.targetDate ?? null)
  const selectedPinned = selectedProject ? pinnedProjectIds?.has(selectedProject.id) : false
  const activeSprints = selectedProject?.sprints.filter((sprint) => sprint.status !== 'Cancelled') ?? []

  if (creating && canCreateProject) {
    return <form className="project-create panel-page" onSubmit={(event) => {
      event.preventDefault()
      const form = event.currentTarget
      const data = new FormData(form)
      setBusy(true)
      setError('')
      onCreate(
        String(data.get('name')),
        String(data.get('description')),
        String(data.get('deliveryDate')),
      )
        .then(() => {
          form.reset()
          setCreating(false)
        })
        .catch((reason) => setError(
          reason instanceof Error ? reason.message : 'Project could not be created.'))
        .finally(() => setBusy(false))
    }}>
      <label>Project name<input name="name" required maxLength={160} autoFocus /></label>
      <label>Description<input name="description" maxLength={500} /></label>
      <label>Delivery date<input name="deliveryDate" type="date" required /></label>
      {error && <p className="field-error">{error}</p>}
      <footer>
        <button type="button" className="secondary" disabled={busy} onClick={() => setCreating(false)}>Cancel</button>
        <button className="primary" disabled={busy}><FolderPlus size={16} /> {busy ? 'Creating...' : 'Create project'}</button>
      </footer>
    </form>
  }

  if (editing && selectedProject && canCreateProject) {
    return <form className="project-create panel-page" onSubmit={(event) => {
      event.preventDefault()
      const form = event.currentTarget
      const data = new FormData(form)
      setBusy(true)
      setError('')
      onUpdate(
        selectedProject.id,
        String(data.get('name')),
        String(data.get('description')),
        String(data.get('deliveryDate')),
      )
        .then(() => setEditing(false))
        .catch((reason) => setError(
          reason instanceof Error ? reason.message : 'Project could not be updated.'))
        .finally(() => setBusy(false))
    }}>
      <label>Project name<input name="name" required maxLength={160} defaultValue={selectedProject.name} autoFocus /></label>
      <label>Description<input name="description" maxLength={500} defaultValue={selectedProject.description ?? ''} /></label>
      <label>Delivery date<input name="deliveryDate" type="date" required defaultValue={selectedProject.targetDate ?? ''} /></label>
      {error && <p className="field-error">{error}</p>}
      <footer>
        <button type="button" className="secondary" disabled={busy} onClick={() => setEditing(false)}>Cancel</button>
        <button className="primary" disabled={busy}><Save size={16} /> {busy ? 'Saving...' : 'Save project'}</button>
      </footer>
    </form>
  }

  return <section className="project-bar panel-page" aria-label="Projects">
    <div className="project-context">
      <p className="eyebrow">Project</p>
      <h2>{selectedProject?.name ?? 'No project yet'}</h2>
      {selectedProject && <p className="project-delivery">
        Delivery date: <strong>{selectedProject.targetDate ? formatDate(selectedProject.targetDate) : 'Not set'}</strong>
        {delivery && <span className={`delivery-badge ${delivery.tone}`}>{delivery.label}</span>}
      </p>}
    </div>
    <div className="project-controls">
      <div className="project-selectors">
        {projects.length > 0 && <label>
          <span>Project</span>
          <select value={selectedProjectId} onChange={(event) => onSwitch(event.target.value)}>
            {projects.map((item) => <option value={item.id} key={item.id}>{item.name}</option>)}
          </select>
          <ChevronDown />
        </label>}
        {selectedProject && onSprintSwitch && <label>
          <span>Sprint</span>
          <select value={selectedSprintId ?? ''} onChange={(event) => onSprintSwitch(event.target.value)}>
            <option value="">All sprints</option>
            {activeSprints.map((sprint) => <option value={sprint.id} key={sprint.id}>{sprint.name} ({sprint.status})</option>)}
          </select>
          <ChevronDown />
        </label>}
      </div>
      {(canCreateProject || selectedProject) && <div className="project-actions">
        {canCreateProject && <button className="primary project-main-action" onClick={() => setCreating(true)}><FolderPlus size={16} /> New project</button>}
        <div className="project-action-row">
          {canCreateProject && selectedProject && onAddSprint && <button className="secondary sprint-action" onClick={onAddSprint}><Clock3 size={16} /> New sprint</button>}
          {selectedProject && onTogglePin && <button className={`secondary ${selectedPinned ? 'selected-action' : ''}`} onClick={() => onTogglePin(selectedProject.id)}><Pin size={16} /> {selectedPinned ? 'Pinned' : 'Pin'}</button>}
        </div>
        {canCreateProject && selectedProject && <div className="project-action-row compact">
          <button className="secondary" onClick={() => setEditing(true)}><Pencil size={16} /> Edit</button>
          <button className="secondary danger-action" disabled={busy} onClick={() => {
            setBusy(true)
            setError('')
            onArchive(selectedProject.id)
              .catch((reason) => setError(reason instanceof Error ? reason.message : 'Project could not be archived.'))
              .finally(() => setBusy(false))
          }}><Trash2 size={16} /> Archive</button>
          <button
            className="secondary danger-action"
            disabled={busy}
            onClick={() => onDelete(selectedProject.id)}
          ><Trash2 size={16} /> Delete</button>
        </div>}
      </div>}
    </div>
    {error && <p className="field-error">{error}</p>}
  </section>
}

// Computes a human label and severity tone (healthy/warning/critical) for how close/overdue
// a delivery date is relative to today, or null if there's no delivery date.
function deliveryStatus(deliveryDate: string | null) {
  if (!deliveryDate) return null
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const delivery = new Date(`${deliveryDate}T00:00:00`)
  const days = Math.ceil((delivery.getTime() - today.getTime()) / 86400000)
  if (days < 0) return { label: `${Math.abs(days)} day${Math.abs(days) === 1 ? '' : 's'} overdue`, tone: 'critical' }
  if (days === 0) return { label: 'Due today', tone: 'critical' }
  if (days === 1) return { label: '1 day left', tone: 'warning' }
  if (days <= 7) return { label: `${days} days left`, tone: 'warning' }
  return { label: `${days} days left`, tone: 'healthy' }
}

// Formats a date-only or ISO datetime string as a localized date, or a fallback label if missing/invalid.
function formatDate(value?: string | null) {
  if (!value) return 'Not recorded'
  const date = value.includes('T')
    ? new Date(value)
    : new Date(`${value}T00:00:00`)
  return Number.isNaN(date.getTime()) ? 'Not recorded' : date.toLocaleDateString()
}

// Formats a byte count as a human-readable B/KB/MB string.
function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

// Escapes a single CSV field, quoting and doubling internal quotes when it contains
// a comma, quote, or newline.
function csvField(value: string) {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value
}

// Serializes a list of report tasks into CSV text (with header row) for export/download.
function tasksToCsv(tasks: WorkspaceReportTask[]) {
  const header = ['Task', 'Project', 'Status', 'Due date', 'Completed at', 'Priority band', 'Priority score', 'Tags']
  const rows = tasks.map((task) => [
    task.title,
    task.projectName,
    statusLabels[task.status],
    task.dueDate ?? '',
    task.completedAt ? new Date(task.completedAt).toLocaleDateString() : '',
    task.priorityBand ?? '',
    task.priorityScore != null ? String(task.priorityScore) : '',
    task.tags.join('; '),
  ])
  return [header, ...rows]
    .map((row) => row.map(csvField).join(','))
    .join('\r\n')
}

// Triggers a browser download of text content by creating a temporary Blob URL and
// programmatically clicking a hidden anchor; adds a UTF-8 BOM for spreadsheet compatibility.
function downloadTextFile(filename: string, content: string, mimeType: string) {
  const blob = new Blob(['\ufeff' + content], { type: mimeType })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

/** Confirmation dialog for permanently deleting a task; disables actions while `busy`. */
function TaskDeleteDialog({
  task,
  busy,
  onClose,
  onConfirm,
}: {
  task: TaskItem
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}) {
  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="danger-dialog" aria-labelledby="task-delete-title">
      <header>
        <div>
          <p className="eyebrow">Permanent delete</p>
          <h2 id="task-delete-title">Delete {task.title}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="danger-dialog-body">
        <div className="danger-dialog-icon"><AlertTriangle /></div>
        <div>
          <p>
            This will permanently delete the task, its notes, tags, and
            activity history.
          </p>
          <strong>This action cannot be rolled back.</strong>
        </div>
      </section>
      <footer>
        <button className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="primary danger-primary" disabled={busy} onClick={onConfirm}>
          <Trash2 size={16} /> {busy ? 'Deleting...' : 'Delete task'}
        </button>
      </footer>
    </dialog>
  </div>
}

/** Confirmation dialog for permanently deleting a personal todo; disables actions while `busy`. */
function TodoDeleteDialog({
  todo,
  busy,
  onClose,
  onConfirm,
}: {
  todo: PersonalTodo
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}) {
  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="danger-dialog" aria-labelledby="todo-delete-title">
      <header>
        <div>
          <p className="eyebrow">Permanent delete</p>
          <h2 id="todo-delete-title">Delete {todo.title}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="danger-dialog-body">
        <div className="danger-dialog-icon"><AlertTriangle /></div>
        <div>
          <p>This will permanently delete the todo and its notes.</p>
          <strong>This action cannot be rolled back.</strong>
        </div>
      </section>
      <footer>
        <button className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="primary danger-primary" disabled={busy} onClick={onConfirm}>
          <Trash2 size={16} /> {busy ? 'Deleting...' : 'Delete todo'}
        </button>
      </footer>
    </dialog>
  </div>
}

/**
 * Confirmation dialog for permanently deleting a project. Requires the user to type the
 * project's exact name into a confirmation field before the delete button is enabled.
 */
function ProjectDeleteDialog({
  project,
  busy,
  onClose,
  onConfirm,
}: {
  project: ProjectDetails
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}) {
  const [confirmText, setConfirmText] = useState('')
  const confirmed = confirmText === project.name
  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="danger-dialog" aria-labelledby="project-delete-title">
      <header>
        <div>
          <p className="eyebrow">Permanent delete</p>
          <h2 id="project-delete-title">Delete {project.name}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="danger-dialog-body">
        <div className="danger-dialog-icon"><AlertTriangle /></div>
        <div>
          <p>
            This will permanently delete the project, its sprints, tasks,
            notes, tags, activity history, and board data.
          </p>
          <strong>This action cannot be rolled back.</strong>
        </div>
      </section>
      <label className="danger-confirm-field">
        Type <strong>{project.name}</strong> to confirm
        <input
          value={confirmText}
          onChange={(event) => setConfirmText(event.target.value)}
          disabled={busy}
          autoComplete="off"
          autoFocus
        />
      </label>
      <footer>
        <button className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="primary danger-primary" disabled={busy || !confirmed} onClick={onConfirm}>
          <Trash2 size={16} /> {busy ? 'Deleting...' : 'Delete project'}
        </button>
      </footer>
    </dialog>
  </div>
}

/**
 * Confirmation dialog for permanently deleting a workspace. Requires the user to type the
 * workspace's exact name into a confirmation field before the delete button is enabled.
 */
function WorkspaceDeleteDialog({
  workspace,
  busy,
  onClose,
  onConfirm,
}: {
  workspace: Workspace
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}) {
  const [confirmText, setConfirmText] = useState('')
  const confirmed = confirmText === workspace.name
  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="danger-dialog" aria-labelledby="workspace-delete-title">
      <header>
        <div>
          <p className="eyebrow">Permanent delete</p>
          <h2 id="workspace-delete-title">Delete {workspace.name}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="danger-dialog-body">
        <div className="danger-dialog-icon"><AlertTriangle /></div>
        <div>
          <p>
            This will permanently delete the workspace, all projects, sprints,
            tasks, notes, tags, activity history, members, and pending invites.
          </p>
          <strong>You must have another workspace available. This action cannot be rolled back.</strong>
        </div>
      </section>
      <label className="danger-confirm-field">
        Type <strong>{workspace.name}</strong> to confirm
        <input
          value={confirmText}
          onChange={(event) => setConfirmText(event.target.value)}
          disabled={busy}
          autoComplete="off"
          autoFocus
        />
      </label>
      <footer>
        <button className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="primary danger-primary" disabled={busy || !confirmed} onClick={onConfirm}>
          <Trash2 size={16} /> {busy ? 'Deleting...' : 'Delete workspace'}
        </button>
      </footer>
    </dialog>
  </div>
}

/**
 * Grid view of all projects in the workspace with pagination, pin toggling, and a
 * details dialog (`ProjectDetailsDialog`) opened by clicking a card via `selectedProject`.
 */
function ProjectsPage({
  workspaceId,
  projects,
  selectedProjectId,
  workspaceRole,
  pinnedProjectIds,
  onSwitch,
  onOpenTasks,
  onOpenSprints,
  onTogglePin,
  onCreate,
  onUpdate,
  onArchive,
  onDelete,
}: {
  workspaceId: string
  projects: ProjectDetails[]
  selectedProjectId: string
  workspaceRole: Workspace['role'] | null
  pinnedProjectIds: Set<string>
  onSwitch: (projectId: string) => void
  onOpenTasks: (projectId: string) => void
  onOpenSprints: (projectId: string) => void
  onTogglePin: (projectId: string) => void
  onCreate: (
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onUpdate: (
    projectId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onArchive: (projectId: string) => Promise<void>
  onDelete: (projectId: string) => void
}) {
  const projectPageSize = 6
  const [projectPage, setProjectPage] = useState(1)
  const [selectedProject, setSelectedProject] = useState<ProjectDetails | null>(null)
  const totalProjectPages = Math.max(1, Math.ceil(projects.length / projectPageSize))
  const visibleProjects = projects.slice(
    (projectPage - 1) * projectPageSize,
    projectPage * projectPageSize)

  // Clamps the current page back within range if the project list shrinks (e.g. after a delete).
  useEffect(() => {
    if (projectPage > totalProjectPages) {
      setProjectPage(totalProjectPages)
    }
  }, [projectPage, totalProjectPages])
  // Closes the details dialog if its project was removed from the list (e.g. deleted elsewhere).
  useEffect(() => {
    if (selectedProject && !projects.some((project) => project.id === selectedProject.id)) {
      setSelectedProject(null)
    }
  }, [projects, selectedProject])

  const stopProjectAction = (event: MouseEvent) => {
    event.stopPropagation()
  }

  return <section className="projects-page">
    <ProjectBar
      projects={projects}
      selectedProjectId={selectedProjectId}
      workspaceRole={workspaceRole}
      pinnedProjectIds={pinnedProjectIds}
      onSwitch={onSwitch}
      onTogglePin={onTogglePin}
      onCreate={onCreate}
      onUpdate={onUpdate}
      onArchive={onArchive}
      onDelete={onDelete}
    />
    {projects.length
      ? <>
          <div className="project-grid" aria-label="Workspace projects">
            {visibleProjects.map((project) => {
              const delivery = deliveryStatus(project.targetDate)
              const pinned = pinnedProjectIds.has(project.id)
              return <article
                className={`project-card clickable ${project.id === selectedProjectId ? 'selected' : ''}`}
                key={project.id}
                role="button"
                tabIndex={0}
                onClick={() => setSelectedProject(project)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault()
                    setSelectedProject(project)
                  }
                }}
                aria-label={`Open ${project.name} project details`}
              >
                <header>
                  <div>
                    <p className="eyebrow">Project</p>
                    <h2>{project.name}</h2>
                  </div>
                  <button
                    className={`pin-button ${pinned ? 'active' : ''}`}
                    onClick={(event) => {
                      stopProjectAction(event)
                      onTogglePin(project.id)
                    }}
                    aria-label={pinned ? `Unpin ${project.name}` : `Pin ${project.name}`}
                    title={pinned ? 'Unpin project' : 'Pin project'}
                  ><Pin /></button>
                </header>
                <p>{project.description || 'No description recorded yet.'}</p>
                <div className="project-card-meta">
                  <span><CalendarDays size={14} /> {project.targetDate ? formatDate(project.targetDate) : 'No delivery date'}</span>
                  {delivery && <span className={`delivery-badge ${delivery.tone}`}>{delivery.label}</span>}
                </div>
                <footer>
                  <button className="secondary" onClick={(event) => {
                    stopProjectAction(event)
                    onSwitch(project.id)
                  }}>Select</button>
                  <button className="secondary sprint-action" onClick={(event) => {
                    stopProjectAction(event)
                    onOpenSprints(project.id)
                  }}><Clock3 size={16} /> Plan sprints</button>
                  <button className="primary" onClick={(event) => {
                    stopProjectAction(event)
                    onOpenTasks(project.id)
                  }}><LayoutList size={16} /> Open tasks</button>
                  {canManageProjects(workspaceRole) && <button
                    className="secondary danger-action"
                    onClick={(event) => {
                      stopProjectAction(event)
                      onDelete(project.id)
                    }}
                  ><Trash2 size={16} /> Delete</button>}
                </footer>
              </article>
            })}
          </div>
          <Pagination
            pageNumber={projectPage}
            pageSize={projectPageSize}
            totalCount={projects.length}
            totalPages={totalProjectPages}
            onPageChange={setProjectPage}
          />
        </>
      : <div className="empty"><FolderPlus /><h2>No project yet</h2><p>Create a project before adding delivery tasks.</p></div>}
    {selectedProject && <ProjectDetailsDialog
      workspaceId={workspaceId}
      project={selectedProject}
      onClose={() => setSelectedProject(null)}
    />}
  </section>
}

/**
 * Read-only drill-down dialog for one project: delivery date, task status counts, its
 * sprints, and its full task list. Fetches all of the project's tasks itself on mount
 * (independent of the parent's paginated/filtered task list).
 */
function ProjectDetailsDialog({
  workspaceId,
  project,
  onClose,
}: {
  workspaceId: string
  project: ProjectDetails
  onClose: () => void
}) {
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // Loads every task for this project (across all statuses/sprints) when the dialog opens.
  useEffect(() => {
    let mounted = true
    setLoading(true)
    setError('')
    loadBoardTasks(workspaceId, '', project.id, '')
      .then((page) => {
        if (mounted) setTasks(page.items)
      })
      .catch((reason) => {
        if (mounted) setError(reason instanceof Error ? reason.message : 'Project tasks could not be loaded.')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })

    return () => {
      mounted = false
    }
  }, [workspaceId, project.id])

  const orderedSprints = [...project.sprints].sort((left, right) =>
    left.startDate.localeCompare(right.startDate) || left.name.localeCompare(right.name))
  const sprintNames = new Map(project.sprints.map((sprint) => [sprint.id, sprint.name]))
  const statusCounts = tasks.reduce<Record<TaskStatus, number>>((counts, task) => {
    counts[task.status] += 1
    return counts
  }, { Backlog: 0, Ready: 0, InProgress: 0, Blocked: 0, Completed: 0 })
  const activeSprints = project.sprints.filter((sprint) => sprint.status === 'Active').length
  const plannedSprints = project.sprints.filter((sprint) => sprint.status === 'Planned').length
  const openTasks = tasks.length - statusCounts.Completed
  const orderedTasks = [...tasks].sort((left, right) =>
    compareBacklogDueDates(left, right) ||
    statusLabels[left.status].localeCompare(statusLabels[right.status]) ||
    compareCreatedAtDesc(left, right))

  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="project-details-dialog" aria-labelledby="project-detail-title">
      <header>
        <div>
          <p className="eyebrow">Project details</p>
          <h2 id="project-detail-title">{project.name}</h2>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="sprint-detail-body">
        <div className="sprint-detail-grid">
          <article>
            <span>Delivery date</span>
            <strong>{project.targetDate ? formatDate(project.targetDate) : 'Not set'}</strong>
          </article>
          <article>
            <span>Open work</span>
            <strong>{openTasks}</strong>
          </article>
          <article>
            <span>Sprints</span>
            <strong>{project.sprints.length}</strong>
          </article>
          <article>
            <span>Active cycles</span>
            <strong>{activeSprints || plannedSprints ? `${activeSprints} active, ${plannedSprints} planned` : 'None'}</strong>
          </article>
        </div>
        <section className="sprint-goal">
          <span>Goal</span>
          <p>{project.description || 'No project goal recorded yet.'}</p>
        </section>
        <section className="sprint-status-summary" aria-label="Project task status summary">
          {Object.entries(statusCounts).map(([status, count]) =>
            <span key={status}><i className={`status-dot ${status.toLowerCase()}`} /> {statusLabels[status as TaskStatus]} <strong>{count}</strong></span>)}
        </section>
        <section className="project-sprint-summary">
          <header>
            <h3>Sprints under this project</h3>
            <span>{project.sprints.length} sprint{project.sprints.length === 1 ? '' : 's'}</span>
          </header>
          {orderedSprints.length
            ? <div className="project-sprint-list">
                {orderedSprints.map((sprint, index) => <article key={sprint.id}>
                  <span className="sprint-number">Sprint {index + 1}</span>
                  <div>
                    <strong>{sprint.name}</strong>
                    <small>{sprint.goal || 'No sprint goal recorded yet.'}</small>
                  </div>
                  <span className={`sprint-status ${sprint.status.toLowerCase()}`}>{sprint.status}</span>
                  <small>{formatDate(sprint.startDate)} - {formatDate(sprint.endDate)}</small>
                </article>)}
              </div>
            : <div className="empty compact"><Clock3 /><h2>No sprints yet</h2><p>Create sprints to group this project into delivery cycles.</p></div>}
        </section>
        <section className="sprint-task-table">
          <header>
            <h3>Tasks under this project</h3>
            <span>{tasks.length} task{tasks.length === 1 ? '' : 's'}</span>
          </header>
          {loading && <div className="loading compact">Loading project tasks...</div>}
          {error && <p className="field-error">{error}</p>}
          {!loading && !error && (orderedTasks.length
            ? <div className="sprint-task-list">
                {orderedTasks.map((task) => <article key={task.id}>
                  <div>
                    <strong>{task.title}</strong>
                    <span>{task.sprintId ? sprintNames.get(task.sprintId) ?? 'Sprint unavailable' : 'No sprint'}{task.tags.length ? ` - ${task.tags.join(', ')}` : ''}</span>
                  </div>
                  <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
                  <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ? `Due ${formatDate(task.dueDate)}` : 'No deadline'}</span>
                </article>)}
              </div>
            : <div className="empty compact"><LayoutList /><h2>No tasks under this project</h2><p>Create tasks from the Tasks or Board page after selecting this project.</p></div>)}
        </section>
      </section>
    </dialog>
  </div>
}

/**
 * Sprint planning page: project selector/bar plus a `SprintPanel` for the active project's
 * sprints, letting users create, edit, change status, or delete sprints.
 */
function SprintsPage({
  workspaceId,
  projects,
  selectedProjectId,
  workspaceRole,
  pinnedProjectIds,
  onSwitch,
  onOpenTasks,
  onTogglePin,
  onCreate,
  onUpdate,
  onArchive,
  onDelete,
  onCreateSprint,
  onUpdateSprint,
  onChangeSprintStatus,
  onDeleteSprint,
}: {
  workspaceId: string
  projects: ProjectDetails[]
  selectedProjectId: string
  workspaceRole: Workspace['role'] | null
  pinnedProjectIds: Set<string>
  onSwitch: (projectId: string) => void
  onOpenTasks: (projectId: string) => void
  onTogglePin: (projectId: string) => void
  onCreate: (
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onUpdate: (
    projectId: string,
    name: string,
    description: string,
    deliveryDate: string,
  ) => Promise<void>
  onArchive: (projectId: string) => Promise<void>
  onDelete: (projectId: string) => void
  onCreateSprint: (
    projectId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => Promise<void>
  onUpdateSprint: (
    projectId: string,
    sprintId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => Promise<void>
  onChangeSprintStatus: (
    projectId: string,
    sprintId: string,
    action: 'start' | 'complete' | 'cancel',
  ) => Promise<void>
  onDeleteSprint: (projectId: string, sprintId: string) => Promise<void>
}) {
  const [sprintComposerToken, setSprintComposerToken] = useState(0)
  const selectedProject = projects.find((item) => item.id === selectedProjectId) ?? projects[0] ?? null
  const sprintTotals = projects.reduce((totals, item) => {
    for (const sprint of item.sprints) {
      totals.total += 1
      if (sprint.status === 'Active') totals.active += 1
      if (sprint.status === 'Planned') totals.planned += 1
      if (sprint.status === 'Completed') totals.completed += 1
    }
    return totals
  }, { total: 0, active: 0, planned: 0, completed: 0 })

  return <section className="sprints-page">
    <section className="home-heading">
      <div>
        <p className="eyebrow">Sprint planning</p>
        <h2>Plan short delivery cycles inside each project</h2>
      </div>
      <span>{sprintTotals.total} sprint{sprintTotals.total === 1 ? '' : 's'} across workspace projects</span>
    </section>
    <section className="metrics sprint-metrics" aria-label="Sprint summary">
      <Metric label="Total sprints" value={sprintTotals.total} detail={`${projects.length} project${projects.length === 1 ? '' : 's'}`} icon={<Clock3 />} />
      <Metric label="Active" value={sprintTotals.active} detail="Currently being delivered" icon={<Activity />} />
      <Metric label="Planned" value={sprintTotals.planned} detail="Ready for future cycles" icon={<CalendarDays />} />
      <Metric label="Completed" value={sprintTotals.completed} detail="Closed delivery cycles" icon={<CheckCircle2 />} />
    </section>
    <ProjectBar
      projects={projects}
      selectedProjectId={selectedProject?.id ?? selectedProjectId}
      workspaceRole={workspaceRole}
      pinnedProjectIds={pinnedProjectIds}
      onSwitch={onSwitch}
      onTogglePin={onTogglePin}
      onAddSprint={() => setSprintComposerToken((token) => token + 1)}
      onCreate={onCreate}
      onUpdate={onUpdate}
      onArchive={onArchive}
      onDelete={onDelete}
    />
    {selectedProject
      ? <>
          <SprintPanel
            workspaceId={workspaceId}
            project={selectedProject}
            workspaceRole={workspaceRole}
            composerToken={sprintComposerToken}
            onCreate={onCreateSprint}
            onUpdate={onUpdateSprint}
            onChangeStatus={onChangeSprintStatus}
            onDelete={onDeleteSprint}
          />
          <div className="sprint-page-actions">
            <button className="primary" onClick={() => onOpenTasks(selectedProject.id)}><LayoutList size={16} /> Open project tasks</button>
          </div>
        </>
      : <div className="empty"><FolderPlus /><h2>No project yet</h2><p>Create a project first, then create sprints inside that project.</p></div>}
  </section>
}

/**
 * Manages the list of sprints for one project: create/edit form, status transitions
 * (start/complete/cancel), deletion, and a details dialog per sprint. `composerToken`
 * is bumped by the parent to force-open the create form (e.g. from a toolbar button).
 */
function SprintPanel({
  workspaceId,
  project,
  workspaceRole,
  composerToken,
  onCreate,
  onUpdate,
  onChangeStatus,
  onDelete,
}: {
  workspaceId: string
  project: ProjectDetails
  workspaceRole: Workspace['role'] | null
  composerToken?: number
  onCreate: (
    projectId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => Promise<void>
  onUpdate: (
    projectId: string,
    sprintId: string,
    name: string,
    goal: string,
    startDate: string,
    endDate: string,
  ) => Promise<void>
  onChangeStatus: (
    projectId: string,
    sprintId: string,
    action: 'start' | 'complete' | 'cancel',
  ) => Promise<void>
  onDelete: (projectId: string, sprintId: string) => Promise<void>
}) {
  const canManage = workspaceRole === 'Owner' || workspaceRole === 'Manager'
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<Sprint | null>(null)
  const [selectedSprint, setSelectedSprint] = useState<{ sprint: Sprint; number: number } | null>(null)
  const [deleteCandidate, setDeleteCandidate] = useState<Sprint | null>(null)
  const [deleteBusy, setDeleteBusy] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const orderedSprints = [...project.sprints].sort((left, right) =>
    left.startDate.localeCompare(right.startDate) || left.name.localeCompare(right.name))
  const activeSprint = project.sprints.find((sprint) => sprint.status === 'Active')
  const plannedCount = project.sprints.filter((sprint) => sprint.status === 'Planned').length

  // Opens the create-sprint form whenever the parent bumps composerToken (e.g. "New sprint" button
  // in ProjectBar), regardless of what value it changes to.
  useEffect(() => {
    if (composerToken) {
      setCreating(true)
      setEditing(null)
    }
  }, [composerToken])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    setBusy(true)
    setError('')
    const data = new FormData(form)
    const name = String(data.get('name')).trim()
    const goal = String(data.get('goal') ?? '').trim()
    const startDate = String(data.get('startDate'))
    const endDate = String(data.get('endDate'))

    try {
      if (editing) {
        await onUpdate(project.id, editing.id, name, goal, startDate, endDate)
        form.reset()
        setEditing(null)
      } else {
        await onCreate(project.id, name, goal, startDate, endDate)
        form.reset()
        setCreating(false)
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  const changeStatus = async (
    sprint: Sprint,
    action: 'start' | 'complete' | 'cancel',
  ) => {
    setBusy(true)
    setError('')
    try {
      await onChangeStatus(project.id, sprint.id, action)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint status could not be changed.')
    } finally {
      setBusy(false)
    }
  }

  const confirmDelete = async () => {
    if (!deleteCandidate) return
    setDeleteBusy(true)
    setError('')
    try {
      await onDelete(project.id, deleteCandidate.id)
      setDeleteCandidate(null)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sprint could not be deleted.')
    } finally {
      setDeleteBusy(false)
    }
  }

  return <section className="sprint-panel panel-page" aria-label="Sprint planning">
    <header>
      <div>
        <p className="eyebrow">Sprint planning</p>
        <h2>{activeSprint ? activeSprint.name : 'No active sprint'}</h2>
        <p>{plannedCount} planned sprint{plannedCount === 1 ? '' : 's'} for {project.name}.</p>
      </div>
      {canManage && <button className="primary" onClick={() => {
        setCreating(true)
        setEditing(null)
      }}><Plus size={16} /> New sprint</button>}
    </header>
    {error && <p className="field-error">{error}</p>}
    {(creating || editing) && <form className="sprint-form" onSubmit={(event) => void submit(event)}>
      <label>Sprint name<input name="name" required maxLength={160} defaultValue={editing?.name ?? ''} autoFocus /></label>
      <label>Goal<input name="goal" maxLength={1000} defaultValue={editing?.goal ?? ''} placeholder="What should be true at the end of this sprint?" /></label>
      <label>Start date<input name="startDate" type="date" required defaultValue={editing?.startDate ?? todayInput} /></label>
      <label>End date<input name="endDate" type="date" required defaultValue={editing?.endDate ?? nextWeekInput()} /></label>
      <footer>
        <button type="button" className="secondary" disabled={busy} onClick={() => {
          setCreating(false)
          setEditing(null)
        }}>Cancel</button>
        <button className="primary" disabled={busy}>{busy ? 'Saving...' : editing ? 'Save sprint' : 'Create sprint'}</button>
      </footer>
    </form>}
    {orderedSprints.length
      ? <div className="sprint-list">
          {orderedSprints.map((sprint, index) => <article className={`sprint-card ${sprint.status.toLowerCase()}`} key={sprint.id}>
            <button type="button" className="sprint-card-open" onClick={() => setSelectedSprint({ sprint, number: index + 1 })}>
              <header>
                <div>
                  <span className="sprint-number">Sprint {index + 1}</span>
                  <strong>{sprint.name}</strong>
                  <span className={`sprint-status ${sprint.status.toLowerCase()}`}>{sprint.status}</span>
                </div>
                <small>{formatDate(sprint.startDate)} - {formatDate(sprint.endDate)}</small>
              </header>
              <p>{sprint.goal || 'No sprint goal recorded yet.'}</p>
            </button>
            {canManage && <footer>
              {sprint.status === 'Planned' && <button className="secondary" disabled={busy} onClick={() => setEditing(sprint)}><Pencil size={15} /> Edit</button>}
              {sprint.status === 'Planned' && <button className="primary" disabled={busy} onClick={() => void changeStatus(sprint, 'start')}><Clock3 size={15} /> Start</button>}
              {sprint.status === 'Active' && <button className="primary" disabled={busy} onClick={() => void changeStatus(sprint, 'complete')}><CheckCircle2 size={15} /> Complete</button>}
              {(sprint.status === 'Planned' || sprint.status === 'Active') && <button className="secondary danger-action" disabled={busy} onClick={() => void changeStatus(sprint, 'cancel')}><X size={15} /> Cancel</button>}
              <button className="secondary danger-action" disabled={busy} onClick={() => setDeleteCandidate(sprint)}><Trash2 size={15} /> Delete</button>
            </footer>}
          </article>)}
        </div>
      : <div className="empty compact"><Clock3 /><h2>No sprints yet</h2><p>Create a sprint to group tasks into short delivery cycles.</p>{canManage && <button className="primary" onClick={() => setCreating(true)}><Plus size={16} /> Add first sprint</button>}</div>}
    {selectedSprint && <SprintDetailsDialog
      workspaceId={workspaceId}
      project={project}
      sprint={selectedSprint.sprint}
      sprintNumber={selectedSprint.number}
      onClose={() => setSelectedSprint(null)}
    />}
    {deleteCandidate && <SprintDeleteDialog
      sprint={deleteCandidate}
      busy={deleteBusy}
      onClose={() => {
        if (!deleteBusy) setDeleteCandidate(null)
      }}
      onConfirm={() => void confirmDelete()}
    />}
  </section>
}

/** Confirmation dialog for permanently deleting a sprint (its tasks are unassigned, not deleted). */
function SprintDeleteDialog({
  sprint,
  busy,
  onClose,
  onConfirm,
}: {
  sprint: Sprint
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}) {
  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="danger-dialog" aria-labelledby="sprint-delete-title">
      <header>
        <div>
          <p className="eyebrow">Permanent delete</p>
          <h2 id="sprint-delete-title">Delete {sprint.name}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="danger-dialog-body">
        <div className="danger-dialog-icon"><AlertTriangle /></div>
        <div>
          <p>
            This will permanently delete the sprint. Any tasks assigned to
            it will be unassigned and moved back to no sprint - they will
            not be deleted.
          </p>
          <strong>This action cannot be rolled back.</strong>
        </div>
      </section>
      <footer>
        <button className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
        <button className="primary danger-primary" disabled={busy} onClick={onConfirm}>
          <Trash2 size={16} /> {busy ? 'Deleting...' : 'Delete sprint'}
        </button>
      </footer>
    </dialog>
  </div>
}

/**
 * Read-only drill-down dialog for one sprint: window/status, task status counts, and the
 * full list of tasks assigned to it. Fetches the sprint's tasks itself on mount.
 */
function SprintDetailsDialog({
  workspaceId,
  project,
  sprint,
  sprintNumber,
  onClose,
}: {
  workspaceId: string
  project: ProjectDetails
  sprint: Sprint
  sprintNumber: number
  onClose: () => void
}) {
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // Loads the tasks assigned to this specific sprint when the dialog opens.
  useEffect(() => {
    let mounted = true
    setLoading(true)
    setError('')
    api.tasks(workspaceId, '', 1, 100, project.id, sprint.id)
      .then((page) => {
        if (mounted) setTasks(page.items)
      })
      .catch((reason) => {
        if (mounted) setError(reason instanceof Error ? reason.message : 'Sprint tasks could not be loaded.')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })

    return () => {
      mounted = false
    }
  }, [workspaceId, project.id, sprint.id])

  const statusCounts = tasks.reduce<Record<TaskStatus, number>>((counts, task) => {
    counts[task.status] += 1
    return counts
  }, { Backlog: 0, Ready: 0, InProgress: 0, Blocked: 0, Completed: 0 })
  const openTasks = tasks.length - statusCounts.Completed

  return <div className="dialog-backdrop" role="presentation">
    <dialog open className="sprint-details-dialog" aria-labelledby="sprint-detail-title">
      <header>
        <div>
          <p className="eyebrow">{project.name}</p>
          <h2 id="sprint-detail-title">Sprint {sprintNumber}: {sprint.name}</h2>
        </div>
        <button className="icon-button" onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <section className="sprint-detail-body">
        <div className="sprint-detail-grid">
          <article>
            <span>Project delivery</span>
            <strong>{project.targetDate ? formatDate(project.targetDate) : 'Not set'}</strong>
          </article>
          <article>
            <span>Sprint window</span>
            <strong>{formatDate(sprint.startDate)} - {formatDate(sprint.endDate)}</strong>
          </article>
          <article>
            <span>Sprint status</span>
            <strong>{sprint.status}</strong>
          </article>
          <article>
            <span>Open sprint work</span>
            <strong>{openTasks}</strong>
          </article>
        </div>
        <section className="sprint-goal">
          <span>Goal</span>
          <p>{sprint.goal || 'No sprint goal recorded yet.'}</p>
        </section>
        <section className="sprint-status-summary" aria-label="Sprint task status summary">
          {Object.entries(statusCounts).map(([status, count]) =>
            <span key={status}><i className={`status-dot ${status.toLowerCase()}`} /> {statusLabels[status as TaskStatus]} <strong>{count}</strong></span>)}
        </section>
        <section className="sprint-task-table">
          <header>
            <h3>Tasks under this sprint</h3>
            <span>{tasks.length} task{tasks.length === 1 ? '' : 's'}</span>
          </header>
          {loading && <div className="loading compact">Loading sprint tasks...</div>}
          {error && <p className="field-error">{error}</p>}
          {!loading && !error && (tasks.length
            ? <div className="sprint-task-list">
                {tasks.map((task) => <article key={task.id}>
                  <div>
                    <strong>{task.title}</strong>
                    <span>{task.tags.length ? task.tags.join(', ') : 'No tags'}</span>
                  </div>
                  <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
                  <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ? `Due ${formatDate(task.dueDate)}` : 'No deadline'}</span>
                </article>)}
              </div>
            : <div className="empty compact"><LayoutList /><h2>No tasks in this sprint</h2><p>Assign tasks to this sprint from the task create or edit form.</p></div>)}
        </section>
      </section>
    </dialog>
  </div>
}

// Returns today's date plus 7 days as a yyyy-mm-dd input value, used as a default sprint end date.
function nextWeekInput() {
  return new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10)
}

type CalendarSource = 'projects' | 'tasks' | 'sprints' | 'myday'
type CalendarTone = 'healthy' | 'warning' | 'critical'

interface CalendarEntry {
  id: string
  date: string
  title: string
  type: string
  tone: CalendarTone
  source: CalendarSource
}

interface CalendarSourceMeta {
  label: string
  singular: string
  plural: string
  dot: string
  fg: string
  bg: string
}

const calendarSourceMeta: Record<CalendarSource, CalendarSourceMeta> = {
  projects: { label: 'Project delivery dates', singular: 'project', plural: 'projects', dot: '#147d68', fg: '#17624f', bg: '#e3f4ef' },
  tasks: { label: 'Task due dates', singular: 'task', plural: 'tasks', dot: '#2f6fb0', fg: '#295f8f', bg: '#e6f0fb' },
  sprints: { label: 'Sprint dates', singular: 'sprint', plural: 'sprints', dot: '#8b5cf6', fg: '#6d28d9', bg: '#f1e9fe' },
  myday: { label: 'My Day to-dos', singular: 'to-do', plural: 'to-dos', dot: '#c78911', fg: '#94620b', bg: '#fff1d7' },
}

// Formats a Date as a local yyyy-mm-dd string (avoids UTC-shift bugs from toISOString()).
function toIsoDate(date: Date) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

type CalendarComposerMode = 'choose' | 'todo' | 'task' | 'task-full'

interface CalendarComposerState {
  date: string
  mode: CalendarComposerMode
  projectId?: string
}

/**
 * Month-grid calendar combining project delivery dates, task due dates, sprint start/end
 * dates, and My Day to-dos as togglable overlay layers (`sources`). Clicking a day shows
 * its entries and offers a composer to quickly create a to-do or task on that date.
 */
function CalendarPage({
  workspaceId,
  projects,
  members,
  isMember,
}: {
  workspaceId: string
  projects: ProjectDetails[]
  members: WorkspaceMember[]
  isMember: boolean
}) {
  const today = useMemo(() => new Date(), [])
  const [cursor, setCursor] = useState(() => new Date(today.getFullYear(), today.getMonth(), 1))
  const [sources, setSources] = useState<Record<CalendarSource, boolean>>({
    projects: true, tasks: false, sprints: false, myday: false,
  })
  const [workspaceTasks, setWorkspaceTasks] = useState<TaskItem[]>([])
  const [tasksLoaded, setTasksLoaded] = useState(false)
  const [tasksLoading, setTasksLoading] = useState(false)
  const [myDayTodos, setMyDayTodos] = useState<PersonalTodo[]>([])
  const [myDayLoading, setMyDayLoading] = useState(false)
  const [myDayRangeKey, setMyDayRangeKey] = useState('')
  const [error, setError] = useState('')
  const [selectedDate, setSelectedDate] = useState<string | null>(null)
  const [composer, setComposer] = useState<CalendarComposerState | null>(null)
  const [composerSaving, setComposerSaving] = useState(false)
  const [composerError, setComposerError] = useState('')
  const [composerNewCategories, setComposerNewCategories] = useState<ProjectCategory[]>([])

  const year = cursor.getFullYear()
  const month = cursor.getMonth()

  useEffect(() => {
    if (!sources.tasks || tasksLoaded || !workspaceId) return
    let cancelled = false
    setTasksLoading(true)
    api.tasks(workspaceId, '', 1, 100)
      .then((page) => {
        if (cancelled) return
        setWorkspaceTasks(page.items)
        setTasksLoaded(true)
      })
      .catch((reason) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Unable to load task due dates.')
      })
      .finally(() => { if (!cancelled) setTasksLoading(false) })
    return () => { cancelled = true }
    // tasksLoading is intentionally excluded: including it here re-runs this
    // effect right after setTasksLoading(true), cancelling the in-flight
    // fetch before its response can ever clear the loading flag.
  }, [sources.tasks, tasksLoaded, workspaceId])

  // Loads My Day todos for the visible month when that layer is enabled, re-fetching only
  // when the computed date range actually changes (tracked via myDayRangeKey).
  useEffect(() => {
    if (!sources.myday) return
    const from = toIsoDate(new Date(year, month, 1))
    const to = toIsoDate(new Date(year, month + 1, 0))
    const key = `${from}_${to}`
    if (key === myDayRangeKey) return
    let cancelled = false
    setMyDayLoading(true)
    api.todosRange(from, to)
      .then((items) => {
        if (cancelled) return
        setMyDayTodos(items)
        setMyDayRangeKey(key)
      })
      .catch((reason) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Unable to load My Day to-dos.')
      })
      .finally(() => { if (!cancelled) setMyDayLoading(false) })
    return () => { cancelled = true }
  }, [sources.myday, year, month, myDayRangeKey])

  // Builds the flat list of calendar entries from whichever sources are currently enabled.
  const entries = useMemo(() => {
    const list: CalendarEntry[] = []
    if (sources.projects) {
      for (const proj of projects) {
        if (!proj.targetDate) continue
        list.push({
          id: `project-${proj.id}`,
          date: proj.targetDate,
          title: proj.name,
          type: 'Project delivery',
          tone: (deliveryStatus(proj.targetDate)?.tone as CalendarTone) ?? 'healthy',
          source: 'projects',
        })
      }
    }
    if (sources.tasks) {
      for (const task of workspaceTasks) {
        if (!task.dueDate) continue
        list.push({
          id: `task-${task.id}`,
          date: task.dueDate,
          title: task.title,
          type: statusLabels[task.status],
          tone: task.deadlineHealth === 'Overdue' ? 'critical' : task.deadlineHealth === 'AtRisk' ? 'warning' : 'healthy',
          source: 'tasks',
        })
      }
    }
    if (sources.sprints) {
      for (const proj of projects) {
        for (const sprint of proj.sprints) {
          list.push({
            id: `sprint-start-${sprint.id}`,
            date: sprint.startDate,
            title: `${sprint.name} starts`,
            type: `Sprint - ${proj.name}`,
            tone: 'healthy',
            source: 'sprints',
          })
          list.push({
            id: `sprint-end-${sprint.id}`,
            date: sprint.endDate,
            title: `${sprint.name} ends`,
            type: `Sprint - ${proj.name}`,
            tone: sprint.status === 'Active' ? 'warning' : 'healthy',
            source: 'sprints',
          })
        }
      }
    }
    if (sources.myday) {
      for (const todo of myDayTodos) {
        if (todo.isCompleted) continue
        list.push({
          id: `todo-${todo.id}`,
          date: todo.todoDate,
          title: todo.title,
          type: 'My Day',
          tone: 'healthy',
          source: 'myday',
        })
      }
    }
    return list
  }, [sources, projects, workspaceTasks, myDayTodos])

  // Indexes entries by ISO date for O(1) lookup per calendar cell.
  const entriesByDate = useMemo(() => {
    const map = new Map<string, CalendarEntry[]>()
    for (const entry of entries) {
      const list = map.get(entry.date) ?? []
      list.push(entry)
      map.set(entry.date, list)
    }
    return map
  }, [entries])

  // Builds the 42-cell (6-week) grid of dates for the visible month, padded with the
  // trailing days of the previous month so the grid always starts on a Sunday.
  const days = useMemo(() => {
    const firstOfMonth = new Date(year, month, 1)
    const gridStart = new Date(year, month, 1 - firstOfMonth.getDay())
    return Array.from({ length: 42 }, (_, index) => {
      const date = new Date(gridStart)
      date.setDate(gridStart.getDate() + index)
      return date
    })
  }, [year, month])

  const todayIso = toIsoDate(today)
  const selectedEntries = selectedDate ? (entriesByDate.get(selectedDate) ?? []) : []

  const toggleSource = (key: CalendarSource) => {
    setSources((current) => ({ ...current, [key]: !current[key] }))
  }

  const closeComposer = () => {
    setComposer(null)
    setComposerError('')
    setComposerNewCategories([])
  }

  return <section className="calendar-page">
    <div className="calendar-hero panel-page">
      <div>
        <p className="eyebrow">Delivery calendar</p>
        <h2>{cursor.toLocaleDateString(undefined, { month: 'long', year: 'numeric' })}</h2>
        <p>Project delivery dates show by default. Turn on task due dates, sprint dates, or My Day to see more.</p>
      </div>
      <div className="calendar-nav">
        <button type="button" className="icon-button" onClick={() => setCursor(new Date(year, month - 1, 1))} aria-label="Previous month"><ChevronLeft /></button>
        <button type="button" className="secondary" onClick={() => setCursor(new Date(today.getFullYear(), today.getMonth(), 1))}>Today</button>
        <button type="button" className="icon-button" onClick={() => setCursor(new Date(year, month + 1, 1))} aria-label="Next month"><ChevronRight /></button>
      </div>
    </div>

    <div className="calendar-sources">
      {(Object.keys(calendarSourceMeta) as CalendarSource[]).map((key) => {
        const meta = calendarSourceMeta[key]
        const active = sources[key]
        return <label
          key={key}
          style={active ? { borderColor: meta.dot, background: meta.bg, color: meta.fg } : undefined}
        >
          <input
            type="checkbox"
            checked={active}
            onChange={() => toggleSource(key)}
            style={{ accentColor: meta.dot }}
          />
          <span className="calendar-source-dot" style={{ background: meta.dot }} />
          {meta.label}
          {key === 'tasks' && tasksLoading && active ? ' (loading...)' : ''}
          {key === 'myday' && myDayLoading && active ? ' (loading...)' : ''}
        </label>
      })}
    </div>

    {error && <div className="error-state"><AlertTriangle /><span>{error}</span><button type="button" onClick={() => setError('')}>Dismiss</button></div>}

    <div className="calendar-month-grid">
      <div className="calendar-weekday-row">
        {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((day) => <span key={day}>{day}</span>)}
      </div>
      <div className="calendar-weeks">
        {days.map((date) => {
          const iso = toIsoDate(date)
          const dayEntries = entriesByDate.get(iso) ?? []
          const isCurrentMonth = date.getMonth() === month
          const isToday = iso === todayIso
          const worstTone: CalendarTone | null = dayEntries.some((entry) => entry.tone === 'critical')
            ? 'critical'
            : dayEntries.some((entry) => entry.tone === 'warning')
              ? 'warning'
              : null
          const bySource = new Map<CalendarSource, CalendarEntry[]>()
          for (const entry of dayEntries) {
            const list = bySource.get(entry.source) ?? []
            list.push(entry)
            bySource.set(entry.source, list)
          }
          return <div
            key={iso}
            role="button"
            tabIndex={0}
            className={[
              'calendar-day',
              isCurrentMonth ? '' : 'outside',
              isToday ? 'today' : '',
              selectedDate === iso ? 'selected' : '',
            ].filter(Boolean).join(' ')}
            onClick={() => setSelectedDate(iso === selectedDate ? null : iso)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault()
                setSelectedDate(iso === selectedDate ? null : iso)
              }
            }}
          >
            {worstTone && <span className={`calendar-day-tone-dot ${worstTone}`} title={worstTone === 'critical' ? 'Overdue or due today' : 'Due soon'} />}
            <span className="calendar-day-number">{date.getDate()}</span>
            <button
              type="button"
              className="calendar-day-add"
              aria-label={`Add to ${iso}`}
              title="Add to this day"
              onClick={(event) => {
                event.stopPropagation()
                setComposer({ date: iso, mode: 'choose' })
              }}
            >
              <Plus size={13} />
            </button>
            {bySource.size > 0 && <div className="calendar-day-pills">
              {Array.from(bySource.entries()).map(([source, items]) => {
                const meta = calendarSourceMeta[source]
                const label = items.length === 1 ? meta.singular : meta.plural
                return <span
                  key={source}
                  className="calendar-day-pill"
                  style={{ background: meta.bg, color: meta.fg }}
                  title={items.map((item) => `${item.type}: ${item.title}`).join('\n')}
                >
                  {items.length} {label}
                </span>
              })}
            </div>}
          </div>
        })}
      </div>
    </div>

    {selectedDate && <div className="calendar-day-detail">
      <header>
        <strong>{new Date(`${selectedDate}T00:00:00`).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}</strong>
        <button type="button" className="icon-button" onClick={() => setSelectedDate(null)} aria-label="Close"><X /></button>
      </header>
      {selectedEntries.length
        ? <ul>{selectedEntries.map((entry) => <li key={entry.id} className={entry.tone}>
            <strong>{entry.title}</strong>
            <small><span className="calendar-source-dot" style={{ background: calendarSourceMeta[entry.source].dot }} /> {entry.type}</small>
          </li>)}</ul>
        : <p className="empty compact"><CalendarDays /><span>No scheduled items on this day.</span></p>}
    </div>}

    {composer && (() => {
      const composerProject = projects.find((proj) => proj.id === composer.projectId) ?? null
      const dateLabel = new Date(`${composer.date}T00:00:00`).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })

      if (composer.mode === 'task-full' && composerProject) {
        return <TaskDialog
          projectId={composerProject.id}
          isMember={isMember}
          members={members}
          categories={[...composerProject.categories, ...composerNewCategories]}
          sprints={composerProject.sprints}
          initialDueDate={composer.date}
          onCategoryCreated={(category) => setComposerNewCategories((items) => [...items, category])}
          onClose={closeComposer}
          onCreated={() => {
            closeComposer()
            setTasksLoaded(false)
            setSources((current) => ({ ...current, tasks: true }))
          }}
        />
      }

      return <div className="dialog-backdrop" role="presentation">
        <dialog open aria-labelledby="calendar-composer-title">
          <header>
            <div>
              <p className="eyebrow">{dateLabel}</p>
              <h2 id="calendar-composer-title">
                {composer.mode === 'choose' ? 'Add to this day' : composer.mode === 'todo' ? 'Add a to-do' : 'Add a task'}
              </h2>
            </div>
            <button className="icon-button" onClick={closeComposer} aria-label="Close"><X /></button>
          </header>

          {composer.mode === 'choose' && <div className="calendar-composer-choices">
            <button type="button" className="secondary" onClick={() => setComposer({ date: composer.date, mode: 'todo' })}>
              <ListChecks size={16} /> Add to-do
            </button>
            <button type="button" className="secondary" onClick={() => setComposer({ date: composer.date, mode: 'task' })}>
              <LayoutList size={16} /> Add task
            </button>
          </div>}

          {composer.mode === 'todo' && <form onSubmit={async (event) => {
            event.preventDefault()
            setComposerSaving(true)
            setComposerError('')
            try {
              const data = new FormData(event.currentTarget)
              const created = await api.createTodo(
                String(data.get('title')),
                composer.date,
                String(data.get('notes') ?? ''),
                (data.get('priority') as TodoPriority) || 'Medium',
              )
              setMyDayTodos((items) => [...items, created])
              setSources((current) => ({ ...current, myday: true }))
              closeComposer()
            } catch (reason) {
              setComposerError(reason instanceof Error ? reason.message : 'To-do could not be created.')
            } finally {
              setComposerSaving(false)
            }
          }}>
            {composerError && <div className="error-state compact-error"><AlertTriangle /><span>{composerError}</span></div>}
            <label>Title<input name="title" required maxLength={240} autoFocus /></label>
            <label className="note-field">Notes<textarea name="notes" maxLength={2000} rows={3} /></label>
            <label>Priority<select name="priority" defaultValue="Medium"><option value="Low">Low</option><option value="Medium">Medium</option><option value="High">High</option></select><ChevronDown /></label>
            <footer className="editor-footer">
              <div><button type="button" className="secondary" disabled={composerSaving} onClick={() => setComposer({ date: composer.date, mode: 'choose' })}>Back</button></div>
              <div><button type="button" className="secondary" disabled={composerSaving} onClick={closeComposer}>Cancel</button><button className="primary" disabled={composerSaving}>{composerSaving ? 'Adding...' : 'Add to-do'}</button></div>
            </footer>
          </form>}

          {composer.mode === 'task' && <form onSubmit={async (event) => {
            event.preventDefault()
            setComposerError('')
            const data = new FormData(event.currentTarget)
            const projectId = String(data.get('projectId') ?? '')
            if (!projectId) {
              setComposerError('Choose a project.')
              return
            }
            setComposerSaving(true)
            try {
              const created = await api.createTask(
                projectId,
                String(data.get('title')),
                String(data.get('dueDate') || composer.date),
                Number(data.get('effort')),
                3, 3, 3,
              )
              setWorkspaceTasks((items) => [...items, created])
              setSources((current) => ({ ...current, tasks: true }))
              closeComposer()
            } catch (reason) {
              setComposerError(reason instanceof Error ? reason.message : 'Task could not be created.')
            } finally {
              setComposerSaving(false)
            }
          }}>
            {composerError && <div className="error-state compact-error"><AlertTriangle /><span>{composerError}</span></div>}
            <label>Project<select
              name="projectId"
              required
              defaultValue=""
              onChange={(event) => setComposer((current) => current ? { ...current, projectId: event.target.value } : current)}
            >
              <option value="" disabled>Choose a project</option>
              {projects.filter((proj) => !proj.isArchived).map((proj) => <option key={proj.id} value={proj.id}>{proj.name}</option>)}
            </select><ChevronDown /></label>
            <label>Task title<input name="title" required maxLength={240} /></label>
            <div className="form-grid">
              <label>Due date<input name="dueDate" type="date" defaultValue={composer.date} /></label>
              <label>Effort<select name="effort" defaultValue="3">{effortOptions.map((value) => <option key={value}>{value}</option>)}</select><ChevronDown /></label>
            </div>
            <footer className="editor-footer">
              <div>
                <button type="button" className="secondary" disabled={composerSaving} onClick={() => setComposer({ date: composer.date, mode: 'choose' })}>Back</button>
                {composer.projectId && <button type="button" className="secondary" disabled={composerSaving} onClick={() => setComposer((current) => current ? { ...current, mode: 'task-full' } : current)}>Open full form</button>}
              </div>
              <div><button type="button" className="secondary" disabled={composerSaving} onClick={closeComposer}>Cancel</button><button className="primary" disabled={composerSaving}>{composerSaving ? 'Adding...' : 'Add task'}</button></div>
            </footer>
          </form>}
        </dialog>
      </div>
    })()}
  </section>
}

const drilldownLabels: Record<TaskDrilldown, string> = {
  all: 'All work',
  active: 'Active work',
  critical: 'Critical',
  blocked: 'Blocked',
  overdue: 'Overdue',
}

// Filters a task list down to the subset matching a dashboard drilldown filter (active/critical/blocked/overdue).
function filterTasksByDrilldown(tasks: TaskItem[], drilldown: TaskDrilldown) {
  if (drilldown === 'active') return tasks.filter((task) => task.status !== 'Completed')
  if (drilldown === 'critical') return tasks.filter((task) => task.priorityBand === 'Critical')
  if (drilldown === 'blocked') return tasks.filter((task) => task.status === 'Blocked' || task.isBlocked)
  if (drilldown === 'overdue') return tasks.filter((task) => task.deadlineHealth === 'Overdue')
  return tasks
}

// Recomputes dashboard counts/breakdowns locally to reflect a task's status change, so the
// dashboard updates instantly (optimistically) without waiting for a full reload.
function applyTaskMoveToDashboard(
  current: Dashboard,
  task: TaskItem,
  target: TaskStatus,
): Dashboard {
  if (task.status === target) return current

  const oldActive = task.status !== 'Completed'
  const newActive = target !== 'Completed'
  const oldBlocked = task.status === 'Blocked' || task.isBlocked
  const newBlocked = target === 'Blocked'
  const oldCompleted = task.status === 'Completed'
  const newCompleted = target === 'Completed'
  const oldCritical = task.priorityBand === 'Critical' && oldActive
  const newCritical = task.priorityBand === 'Critical' && newActive
  const oldOverdue = task.deadlineHealth === 'Overdue' && oldActive
  const newOverdue = task.deadlineHealth === 'Overdue' && newActive
  const completedTasks = Math.max(
    0,
    current.projectProgress.completedTasks +
      (newCompleted ? 1 : 0) -
      (oldCompleted ? 1 : 0),
  )
  const totalTasks = current.projectProgress.totalTasks
  const completionPercentage = totalTasks === 0
    ? 0
    : Math.round(completedTasks * 100 / totalTasks)

  return {
    ...current,
    activeTaskCount: Math.max(
      0,
      current.activeTaskCount + (newActive ? 1 : 0) - (oldActive ? 1 : 0),
    ),
    blockedTaskCount: Math.max(
      0,
      current.blockedTaskCount + (newBlocked ? 1 : 0) - (oldBlocked ? 1 : 0),
    ),
    criticalTaskCount: Math.max(
      0,
      current.criticalTaskCount + (newCritical ? 1 : 0) - (oldCritical ? 1 : 0),
    ),
    overdueTaskCount: Math.max(
      0,
      current.overdueTaskCount + (newOverdue ? 1 : 0) - (oldOverdue ? 1 : 0),
    ),
    statusBreakdown: moveBreakdownCount(
      current.statusBreakdown,
      task.status,
      target,
    ),
    priorityBreakdown: task.priorityBand === 'Critical' && oldCritical !== newCritical
      ? adjustBreakdownCount(
          current.priorityBreakdown,
          'Critical',
          newCritical ? 1 : -1,
        )
      : current.priorityBreakdown,
    deadlineBreakdown: task.deadlineHealth === 'Overdue' && oldOverdue !== newOverdue
      ? adjustBreakdownCount(
          current.deadlineBreakdown,
          'Overdue',
          newOverdue ? 1 : -1,
        )
      : current.deadlineBreakdown,
    projectProgress: {
      ...current.projectProgress,
      completedTasks,
      completionPercentage,
    },
  }
}

// Shifts one count from the `from` status bucket to the `to` bucket in a breakdown list.
function moveBreakdownCount(
  items: DashboardBreakdownItem[],
  from: TaskStatus,
  to: TaskStatus,
) {
  return items.map((item) => {
    if (item.label === from) {
      return { ...item, count: Math.max(0, item.count - 1) }
    }

    if (item.label === to) {
      return { ...item, count: item.count + 1 }
    }

    return item
  })
}

// Adjusts a single labeled breakdown bucket's count by `delta`, clamped to zero.
function adjustBreakdownCount(
  items: DashboardBreakdownItem[],
  label: string,
  delta: number,
) {
  return items.map((item) => item.label === label
    ? { ...item, count: Math.max(0, item.count + delta) }
    : item)
}

/** A single dashboard stat card; renders as a clickable button when `onClick` is provided, otherwise a static div. */
function Metric({
  label,
  value,
  detail,
  icon,
  tone = '',
  selected = false,
  onClick,
}: {
  label: string
  value: number | string
  detail?: string
  icon: ReactNode
  tone?: string
  selected?: boolean
  onClick?: () => void
}) {
  const className = `metric ${tone} ${onClick ? 'interactive' : ''} ${selected ? 'selected' : ''}`
  const content = <>
    <span className="metric-icon">{icon}</span>
    <div><span className="metric-label">{label}</span><strong>{value}</strong>{detail && <small>{detail}</small>}</div>
  </>
  return onClick
    ? <button className={className} onClick={onClick} aria-pressed={selected} type="button">{content}</button>
    : <div className={className}>{content}</div>
}

const statusChartColors: Record<string, string> = {
  Backlog: '#687483',
  Ready: '#b7791f',
  InProgress: '#2875a5',
  Blocked: '#b23b30',
  Completed: '#287258',
}

const priorityChartColors: Record<string, string> = {
  Low: '#4d9b78',
  Medium: '#4388c7',
  High: '#e49625',
  Critical: '#c33f35',
}

/** Groups the home dashboard's three charts: status donut, weekly flow, and workload by member. */
function DashboardAnalytics({
  dashboard,
  report,
  members,
}: {
  dashboard: Dashboard
  report: WorkspaceReport | null
  members: WorkspaceMember[]
}) {
  return <section className="analytics-grid" aria-label="Dashboard analytics">
    <DonutChart title="Task Progress" items={dashboard.statusBreakdown} colors={statusChartColors} />
    <WeeklyFlowChart tasks={report?.tasks ?? []} />
    <WorkloadChart tasks={report?.tasks ?? []} members={members} />
  </section>
}

/**
 * Topbar bell button that opens a dropdown panel of notifications, with unread/critical
 * counts, mark-as-read actions, and a "view all" link. Closes on outside click or Escape.
 */
function NotificationBell({
  notifications,
  open,
  onToggle,
  onClose,
  onMarkRead,
  onMarkAllRead,
  onViewAll,
}: {
  notifications: NotificationItem[]
  open: boolean
  onToggle: () => void
  onClose: () => void
  onMarkRead: (id: string) => void
  onMarkAllRead: () => void
  onViewAll: () => void
}) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const unreadCount = notifications.filter((notification) => !notification.read).length
  const criticalCount = notifications.filter((notification) => !notification.read && notification.severity === 'critical').length

  // Wires up outside-click and Escape-key listeners only while the panel is open, so it
  // behaves like a typical dropdown/popover.
  useEffect(() => {
    if (!open) return

    const closeOnOutsidePointer = (event: PointerEvent) => {
      if (containerRef.current?.contains(event.target as Node)) return
      onClose()
    }
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }

    document.addEventListener('pointerdown', closeOnOutsidePointer)
    document.addEventListener('keydown', closeOnEscape)

    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [open, onClose])

  return <div className="notification-center" ref={containerRef}>
    <button
      className={`icon-button notification-button ${unreadCount ? 'has-alerts' : ''}`}
      onClick={onToggle}
      aria-label={`Notifications ${unreadCount}`}
      aria-expanded={open}
    >
      <Bell />
      {unreadCount > 0 && <span>{unreadCount}</span>}
    </button>
    {open && <section className="notification-panel" aria-label="In-app notifications">
      <header>
        <div>
          <h2>Notifications</h2>
          <p>{unreadCount
            ? `${unreadCount} unread${criticalCount ? `, ${criticalCount} critical` : ''}`
            : 'All caught up'}</p>
        </div>
        <div className="notification-actions">
          <button
            className="link-button"
            disabled={!unreadCount}
            onClick={onMarkAllRead}
          >
            Mark all as read
          </button>
          <button className="icon-button notification-close" onClick={onClose} aria-label="Close notifications">
            <X />
          </button>
        </div>
      </header>
      {notifications.length
        ? <div className="notification-list">
          {notifications.map((notification) => <button
            type="button"
            className={`notification-item ${notification.severity} ${notification.read ? 'read' : 'unread'}`}
            key={notification.id}
            onClick={() => onMarkRead(notification.id)}
          >
            <span className="notification-status">{notification.read ? <CheckCircle2 size={16} /> : <AlertTriangle size={16} />}</span>
            <div>
              <strong>{notification.title}</strong>
              <p>{notification.message}</p>
              {notification.dueDate && <small>{formatDate(notification.dueDate)}</small>}
            </div>
          </button>)}
        </div>
        : <div className="empty compact"><Bell /><h2>No notifications</h2><p>Due-date and delivery reminders will appear here in real time.</p></div>}
      <footer>
        <button className="link-button" onClick={onViewAll}>View all notifications</button>
      </footer>
    </section>}
  </div>
}

/**
 * My Day page: date-scoped list of personal todos with search, pagination, inline create
 * form, inline edit, complete/reopen toggling, comments, and delete confirmation handoff.
 */
function TodoPage({
  todos,
  totalCount,
  selectedDate,
  search,
  pageNumber,
  pageSize,
  loading,
  error,
  onDateChange,
  onSearchChange,
  onPageChange,
  onReload,
  onCreate,
  onUpdate,
  onToggle,
  onDelete,
  onAddComment,
}: {
  todos: PersonalTodo[]
  totalCount: number
  selectedDate: string
  search: string
  pageNumber: number
  pageSize: number
  loading: boolean
  error: string
  onDateChange: (date: string) => void
  onSearchChange: (search: string) => void
  onPageChange: (page: number) => void
  onReload: () => void
  onCreate: (title: string, date: string, notes: string, priority: TodoPriority) => Promise<void>
  onUpdate: (todo: PersonalTodo, title: string, date: string, notes: string, priority: TodoPriority) => Promise<void>
  onToggle: (todo: PersonalTodo) => Promise<void>
  onDelete: (todo: PersonalTodo) => void
  onAddComment: (todo: PersonalTodo, body: string) => Promise<void>
}) {
  const [title, setTitle] = useState('')
  const [notes, setNotes] = useState('')
  const [priority, setPriority] = useState<TodoPriority>('Medium')
  const [saving, setSaving] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editTitle, setEditTitle] = useState('')
  const [editNotes, setEditNotes] = useState('')
  const [editDate, setEditDate] = useState(selectedDate)
  const [editPriority, setEditPriority] = useState<TodoPriority>('Medium')
  const [busyId, setBusyId] = useState<string | null>(null)
  const [expandedComments, setExpandedComments] = useState<Set<string>>(new Set())
  const [commentBusyId, setCommentBusyId] = useState<string | null>(null)
  const completedCount = todos.filter((todo) => todo.isCompleted).length
  const openCount = todos.length - completedCount
  const totalPages = totalCount === 0
    ? 0
    : Math.ceil(totalCount / pageSize)

  const create = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!title.trim()) return
    setSaving(true)
    try {
      await onCreate(title.trim(), selectedDate, notes.trim(), priority)
      setTitle('')
      setNotes('')
      setPriority('Medium')
    } finally {
      setSaving(false)
    }
  }

  const startEdit = (todo: PersonalTodo) => {
    setEditingId(todo.id)
    setEditTitle(todo.title)
    setEditNotes(todo.notes ?? '')
    setEditDate(todo.todoDate)
    setEditPriority(todo.priority)
  }

  const saveEdit = async (todo: PersonalTodo) => {
    if (!editTitle.trim()) return
    setBusyId(todo.id)
    try {
      await onUpdate(todo, editTitle.trim(), editDate, editNotes.trim(), editPriority)
      setEditingId(null)
    } finally {
      setBusyId(null)
    }
  }

  const runItemAction = async (
    todo: PersonalTodo,
    action: (todo: PersonalTodo) => Promise<void>,
  ) => {
    setBusyId(todo.id)
    try {
      await action(todo)
    } finally {
      setBusyId(null)
    }
  }

  const toggleComments = (todoId: string) => {
    setExpandedComments((current) => {
      const next = new Set(current)
      if (next.has(todoId)) next.delete(todoId)
      else next.add(todoId)
      return next
    })
  }

  const submitComment = async (todo: PersonalTodo, event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    const data = new FormData(form)
    const body = String(data.get('body') ?? '').trim()
    if (!body) return
    setCommentBusyId(todo.id)
    try {
      await onAddComment(todo, body)
      form.reset()
    } finally {
      setCommentBusyId(null)
    }
  }

  return <section className="todo-page">
    <div className="todo-hero">
      <div>
        <p className="eyebrow">Personal checklist</p>
        <h2>Plan the day without creating project work.</h2>
        <p>Use this for private reminders, calls, errands, and small follow-ups that do not belong on the team board.</p>
      </div>
      <label><span>Date</span><input type="date" value={selectedDate} onChange={(event) => onDateChange(event.target.value)} /></label>
    </div>

    <section className="todo-summary" aria-label="Todo summary">
      <Metric label="Open todos" value={openCount} icon={<ListChecks />} />
      <Metric label="Completed" value={completedCount} icon={<CheckCircle2 />} />
      <Metric label="Total matches" value={totalCount} icon={<Search />} />
      <Metric label="Selected date" value={new Date(`${selectedDate}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} icon={<CalendarDays />} />
    </section>

    <form className="todo-create" onSubmit={(event) => void create(event)}>
      <label>Todo title<input value={title} onChange={(event) => setTitle(event.target.value)} maxLength={160} placeholder="Call client, review PR, buy domain..." /></label>
      <label>Notes<textarea value={notes} onChange={(event) => setNotes(event.target.value)} maxLength={1000} rows={2} placeholder="Optional context or checklist note." /></label>
      <label>Priority<select value={priority} onChange={(event) => setPriority(event.target.value as TodoPriority)}>{todoPriorities.map((item) => <option key={item}>{item}</option>)}</select></label>
      <button className="primary" disabled={saving || !title.trim()}><Plus size={17} /> {saving ? 'Adding...' : 'Add todo'}</button>
    </form>

    <section className="todo-list-panel">
      <header>
        <div><h2>Todos for {new Date(`${selectedDate}T00:00:00`).toLocaleDateString()}</h2><p>{openCount} open, {completedCount} completed</p></div>
        <div className="todo-toolbar">
          <label className="search"><Search size={17} /><input value={search} onChange={(event) => onSearchChange(event.target.value)} placeholder="Search todos" aria-label="Search todos" /></label>
          <button className="secondary" onClick={onReload} disabled={loading}><Search size={16} /> Refresh</button>
        </div>
      </header>
      {error && <div className="error-state compact-error"><AlertTriangle /> <span>{error}</span></div>}
      {loading ? <div className="loading">Loading todos...</div> :
        todos.length
          ? <div className="todo-list">
            {todos.map((todo) => {
              const editing = editingId === todo.id
              return <article className={`todo-item ${todo.isCompleted ? 'completed' : ''}`} key={todo.id}>
                <button
                  className={`todo-check ${todo.isCompleted ? 'done' : ''}`}
                  onClick={() => void runItemAction(todo, onToggle)}
                  disabled={busyId === todo.id}
                  aria-label={todo.isCompleted ? 'Reopen todo' : 'Complete todo'}
                ><CheckCircle2 size={18} /></button>
                <div className="todo-content">
                  {editing
                    ? <>
                        <input value={editTitle} onChange={(event) => setEditTitle(event.target.value)} maxLength={160} />
                        <textarea value={editNotes} onChange={(event) => setEditNotes(event.target.value)} rows={2} maxLength={1000} />
                        <input type="date" value={editDate} onChange={(event) => setEditDate(event.target.value)} />
                        <select value={editPriority} onChange={(event) => setEditPriority(event.target.value as TodoPriority)}>{todoPriorities.map((item) => <option key={item}>{item}</option>)}</select>
                      </>
                    : <>
                        <div className="todo-title-row">
                          <strong>{todo.title}</strong>
                          <PriorityBadge priority={todo.priority} />
                          {todo.isGeneratedFromDailyRoutine && <span className="routine-badge">Daily routine</span>}
                        </div>
                        {todo.carriedOverFromDate && <span className="carryover-badge">Carry over from {formatDate(todo.originalTodoDate)}</span>}
                        {todo.notes && <p>{todo.notes}</p>}
                        <small>{todo.isCompleted && todo.completedAt ? `Completed ${new Date(todo.completedAt).toLocaleString()}` : `Updated ${new Date(todo.updatedAt).toLocaleString()}`}</small>
                        <button
                          type="button"
                          className="link-button todo-comments-toggle"
                          onClick={() => toggleComments(todo.id)}
                        >
                          <MessageSquare size={14} />
                          {todo.comments.length ? `${todo.comments.length} comment${todo.comments.length === 1 ? '' : 's'}` : 'Add comment'}
                          {expandedComments.has(todo.id) ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                        </button>
                        {expandedComments.has(todo.id) && <div className="todo-comments">
                          {todo.comments.length > 0 && <div className="note-list">
                            {todo.comments.map((comment) => <article key={comment.id}>
                              <MessageSquare size={15} />
                              <p>{comment.body}</p>
                              <small>{new Date(comment.createdAt).toLocaleString()}</small>
                            </article>)}
                          </div>}
                          <form className="todo-comment-form" onSubmit={(event) => void submitComment(todo, event)}>
                            <textarea
                              name="body"
                              rows={2}
                              maxLength={2000}
                              placeholder="Add a comment..."
                              disabled={commentBusyId === todo.id}
                            />
                            <button className="secondary" disabled={commentBusyId === todo.id}>
                              {commentBusyId === todo.id ? 'Adding...' : 'Comment'}
                            </button>
                          </form>
                        </div>}
                      </>}
                </div>
                <div className="todo-actions">
                  {editing
                    ? <>
                        <button className="icon-button" onClick={() => void saveEdit(todo)} disabled={busyId === todo.id || !editTitle.trim()} aria-label="Save todo"><Save /></button>
                        <button className="icon-button" onClick={() => setEditingId(null)} aria-label="Cancel edit"><X /></button>
                      </>
                    : <>
                        <button className="icon-button" onClick={() => startEdit(todo)} aria-label={`Edit ${todo.title}`}><Pencil /></button>
                        <button className="icon-button danger-action" onClick={() => onDelete(todo)} disabled={busyId === todo.id} aria-label={`Delete ${todo.title}`}><Trash2 /></button>
                      </>}
                </div>
              </article>
            })}
          </div>
          : <div className="empty"><ListChecks /><h2>No todos for this date</h2><p>Add a personal todo for calls, reminders, errands, or quick follow-ups.</p></div>}
      {!loading && <Pagination
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        totalPages={totalPages}
        onPageChange={onPageChange}
      />}
    </section>
  </section>
}

/**
 * Manages recurring daily routines (which auto-generate My Day todos): list, pagination,
 * inline create form, inline edit (including active/inactive toggle), and delete.
 */
function DailyRoutinesPage({
  dailyRoutines,
  totalCount,
  pageNumber,
  pageSize,
  loading,
  error,
  onDailyRoutinePageChange,
  onDailyRoutineReload,
  onCreateDailyRoutine,
  onUpdateDailyRoutine,
  onDeleteDailyRoutine,
}: {
  dailyRoutines: DailyRoutine[]
  totalCount: number
  pageNumber: number
  pageSize: number
  loading: boolean
  error: string
  onDailyRoutinePageChange: (page: number) => void
  onDailyRoutineReload: () => void
  onCreateDailyRoutine: (title: string, notes: string, priority: TodoPriority, startDate: string, endDate: string) => Promise<void>
  onUpdateDailyRoutine: (routine: DailyRoutine, title: string, notes: string, priority: TodoPriority, startDate: string, endDate: string, isActive: boolean) => Promise<void>
  onDeleteDailyRoutine: (routine: DailyRoutine) => Promise<void>
}) {
  const [routineTitle, setRoutineTitle] = useState('')
  const [routineNotes, setRoutineNotes] = useState('')
  const [routinePriority, setRoutinePriority] = useState<TodoPriority>('High')
  const [routineStartDate, setRoutineStartDate] = useState(todayInput)
  const [routineEndDate, setRoutineEndDate] = useState('')
  const [routineHasEndDate, setRoutineHasEndDate] = useState(false)
  const [routineSaving, setRoutineSaving] = useState(false)
  const [editingRoutineId, setEditingRoutineId] = useState<string | null>(null)
  const [editRoutineTitle, setEditRoutineTitle] = useState('')
  const [editRoutineNotes, setEditRoutineNotes] = useState('')
  const [editRoutinePriority, setEditRoutinePriority] = useState<TodoPriority>('High')
  const [editRoutineStartDate, setEditRoutineStartDate] = useState(todayInput)
  const [editRoutineEndDate, setEditRoutineEndDate] = useState('')
  const [editRoutineHasEndDate, setEditRoutineHasEndDate] = useState(false)
  const [editRoutineActive, setEditRoutineActive] = useState(true)
  const [routineBusyId, setRoutineBusyId] = useState<string | null>(null)
  const totalPages = totalCount === 0 ? 0 : Math.ceil(totalCount / pageSize)
  const activeCount = dailyRoutines.filter((routine) => routine.isActive).length

  const createRoutine = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!routineTitle.trim()) return
    setRoutineSaving(true)
    try {
      await onCreateDailyRoutine(
        routineTitle.trim(),
        routineNotes.trim(),
        routinePriority,
        routineStartDate,
        routineHasEndDate ? routineEndDate : '')
      setRoutineTitle('')
      setRoutineNotes('')
      setRoutinePriority('High')
      setRoutineStartDate(todayInput)
      setRoutineEndDate('')
      setRoutineHasEndDate(false)
    } finally {
      setRoutineSaving(false)
    }
  }

  const startRoutineEdit = (routine: DailyRoutine) => {
    setEditingRoutineId(routine.id)
    setEditRoutineTitle(routine.title)
    setEditRoutineNotes(routine.notes ?? '')
    setEditRoutinePriority(routine.priority)
    setEditRoutineStartDate(routine.startDate)
    setEditRoutineEndDate(routine.endDate ?? '')
    setEditRoutineHasEndDate(Boolean(routine.endDate))
    setEditRoutineActive(routine.isActive)
  }

  const saveRoutineEdit = async (routine: DailyRoutine) => {
    if (!editRoutineTitle.trim()) return
    setRoutineBusyId(routine.id)
    try {
      await onUpdateDailyRoutine(
        routine,
        editRoutineTitle.trim(),
        editRoutineNotes.trim(),
        editRoutinePriority,
        editRoutineStartDate,
        editRoutineHasEndDate ? editRoutineEndDate : '',
        editRoutineActive)
      setEditingRoutineId(null)
    } finally {
      setRoutineBusyId(null)
    }
  }

  return <section className="todo-page routines-page">
    <div className="todo-hero routine-hero">
      <div>
        <p className="eyebrow">Daily Routines</p>
        <h2>Automate repeated My Day work.</h2>
        <p>Create reusable routines once and Taskora will add them to My Day for each business date. Use pause when the routine is no longer needed temporarily.</p>
      </div>
      <div className="routine-hero-stats">
        <Metric label="Active" value={activeCount} icon={<Clock3 />} />
        <Metric label="Total routines" value={totalCount} icon={<ListChecks />} />
      </div>
    </div>

    <section className="todo-list-panel routine-panel">
      <header>
        <div><h2>Create routine</h2><p>High priority is selected by default so repeated work stands out.</p></div>
      </header>
      <form className="routine-create" onSubmit={(event) => void createRoutine(event)}>
        <label>Routine title<input value={routineTitle} onChange={(event) => setRoutineTitle(event.target.value)} maxLength={160} placeholder="Review inbox, publish update, check blockers..." /></label>
        <label>Notes<textarea value={routineNotes} onChange={(event) => setRoutineNotes(event.target.value)} maxLength={1000} rows={2} placeholder="Optional context for every generated todo." /></label>
        <label>Priority<select value={routinePriority} onChange={(event) => setRoutinePriority(event.target.value as TodoPriority)}>{todoPriorities.map((item) => <option key={item}>{item}</option>)}</select></label>
        <label>Start date<input type="date" value={routineStartDate} onChange={(event) => setRoutineStartDate(event.target.value)} /></label>
        <div className="routine-date-option">
          <label className="inline-check"><input type="checkbox" checked={routineHasEndDate} onChange={(event) => setRoutineHasEndDate(event.target.checked)} /> Set end date</label>
          <input type="date" value={routineEndDate} onChange={(event) => setRoutineEndDate(event.target.value)} disabled={!routineHasEndDate} aria-label="Routine end date" />
        </div>
        <button className="primary routine-submit" disabled={routineSaving || !routineTitle.trim()}><Plus size={17} /> {routineSaving ? 'Saving...' : 'Add routine'}</button>
      </form>
    </section>

    <section className="todo-list-panel routine-panel">
      <header>
        <div><h2>Manage routines</h2><p>{activeCount} active, {Math.max(0, totalCount - activeCount)} paused or inactive</p></div>
        <button className="secondary" onClick={onDailyRoutineReload} disabled={loading}><Search size={16} /> Refresh</button>
      </header>
      {error && <div className="error-state compact-error"><AlertTriangle /> <span>{error}</span></div>}
      {loading ? <div className="loading">Loading routines...</div> :
        dailyRoutines.length
          ? <div className="routine-list">
            {dailyRoutines.map((routine) => {
              const editing = editingRoutineId === routine.id
              return <article className={`routine-item ${routine.isActive ? '' : 'paused'}`} key={routine.id}>
                <div className="routine-content">
                  {editing
                    ? <>
                        <input value={editRoutineTitle} onChange={(event) => setEditRoutineTitle(event.target.value)} maxLength={160} />
                        <textarea value={editRoutineNotes} onChange={(event) => setEditRoutineNotes(event.target.value)} rows={2} maxLength={1000} />
                        <div className="routine-edit-grid">
                          <select value={editRoutinePriority} onChange={(event) => setEditRoutinePriority(event.target.value as TodoPriority)}>{todoPriorities.map((item) => <option key={item}>{item}</option>)}</select>
                          <input type="date" value={editRoutineStartDate} onChange={(event) => setEditRoutineStartDate(event.target.value)} />
                          <div className="routine-date-option compact">
                            <label className="inline-check"><input type="checkbox" checked={editRoutineHasEndDate} onChange={(event) => setEditRoutineHasEndDate(event.target.checked)} /> End date</label>
                            <input type="date" value={editRoutineEndDate} onChange={(event) => setEditRoutineEndDate(event.target.value)} disabled={!editRoutineHasEndDate} aria-label="Routine edit end date" />
                          </div>
                          <label className="inline-check"><input type="checkbox" checked={editRoutineActive} onChange={(event) => setEditRoutineActive(event.target.checked)} /> Active</label>
                        </div>
                      </>
                    : <>
                        <div className="routine-title-row">
                          <strong>{routine.title}</strong>
                          <PriorityBadge priority={routine.priority} />
                          <span className={`routine-state ${routine.isActive ? 'active' : 'paused'}`}>{routine.isActive ? 'Active' : 'Paused'}</span>
                        </div>
                        {routine.notes && <p>{routine.notes}</p>}
                        <small>Starts {formatDate(routine.startDate)}{routine.endDate ? `, ends ${formatDate(routine.endDate)}` : ', no end date'}{routine.lastGeneratedDate ? `, last added ${formatDate(routine.lastGeneratedDate)}` : ''}</small>
                      </>}
                </div>
                <div className="todo-actions">
                  {editing
                    ? <>
                        <button className="icon-button" onClick={() => void saveRoutineEdit(routine)} disabled={routineBusyId === routine.id || !editRoutineTitle.trim()} aria-label="Save routine"><Save /></button>
                        <button className="icon-button" onClick={() => setEditingRoutineId(null)} aria-label="Cancel routine edit"><X /></button>
                      </>
                    : <>
                        <button className="icon-button" onClick={() => startRoutineEdit(routine)} aria-label={`Edit ${routine.title}`}><Pencil /></button>
                        <button className="icon-button danger-action" onClick={() => void onDeleteDailyRoutine(routine)} disabled={routineBusyId === routine.id} aria-label={`Delete ${routine.title}`}><Trash2 /></button>
                      </>}
                </div>
              </article>
            })}
          </div>
          : <div className="empty"><Clock3 /><h2>No daily routines yet</h2><p>Add routines for repeated personal work that should appear in My Day automatically.</p></div>}
      {!loading && <Pagination
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={totalCount}
        totalPages={totalPages}
        onPageChange={onDailyRoutinePageChange}
      />}
    </section>
  </section>
}

/** Small colored pill labeling a todo/routine's priority. */
function PriorityBadge({ priority }: { priority: TodoPriority }) {
  return <span className={`priority-badge ${priority.toLowerCase()}`}>
    {priority}
  </span>
}

/**
 * Date-range workspace report: summary metrics, governance section, charts, and a
 * paginated task table with CSV export and print-to-PDF support.
 */
function ReportsPage({
  dashboard,
  report,
  loading,
  from,
  to,
  onFromChange,
  onToChange,
}: {
  dashboard: Dashboard
  report: WorkspaceReport | null
  loading: boolean
  from: string
  to: string
  onFromChange: (value: string) => void
  onToChange: (value: string) => void
}) {
  const [pageNumber, setPageNumber] = useState(1)
  const pageSize = 8
  // Resets to the first page whenever the report's date range changes.
  useEffect(() => setPageNumber(1), [from, to])

  const tasks = report?.tasks ?? []
  const projects = report?.projects ?? []
  const totalPages = Math.max(1, Math.ceil(tasks.length / pageSize))
  const pageTasks = tasks.slice((pageNumber - 1) * pageSize, pageNumber * pageSize)

  const applyPreset = (days: number) => {
    onFromChange(new Date(Date.now() - days * 86400000).toISOString().slice(0, 10))
    onToChange(new Date().toISOString().slice(0, 10))
  }
  const applyThisMonth = () => {
    const now = new Date()
    onFromChange(new Date(now.getFullYear(), now.getMonth(), 1).toISOString().slice(0, 10))
    onToChange(new Date().toISOString().slice(0, 10))
  }
  const exportCsv = () => {
    downloadTextFile(
      `workspace-report_${from}_to_${to}.csv`,
      tasksToCsv(tasks),
      'text/csv;charset=utf-8;',
    )
  }

  return <section className="panel-page reports-page" aria-label="Reports">
    <div className="report-toolbar">
      <div>
        <h2>Workspace reports</h2>
        <p>Workspace-wide task and project delivery activity for the selected date range.</p>
      </div>
      <div className="report-toolbar-actions">
        <div className="date-presets">
          <button type="button" className="secondary" onClick={() => applyPreset(7)}>Last 7 days</button>
          <button type="button" className="secondary" onClick={() => applyPreset(30)}>Last 30 days</button>
          <button type="button" className="secondary" onClick={() => applyPreset(90)}>Last 90 days</button>
          <button type="button" className="secondary" onClick={applyThisMonth}>This month</button>
        </div>
        <div className="date-range">
          <label><span>From</span><input type="date" value={from} onChange={(event) => onFromChange(event.target.value)} /></label>
          <label><span>To</span><input type="date" value={to} onChange={(event) => onToChange(event.target.value)} /></label>
        </div>
        <div className="report-export-actions">
          <button type="button" className="secondary" onClick={exportCsv} disabled={!tasks.length} title="Download the task list as a CSV file, opens in Excel or Sheets">
            <Download size={16} /> Export CSV
          </button>
          <button type="button" className="secondary" onClick={() => window.print()}>
            <Printer size={16} /> Print / Save as PDF
          </button>
        </div>
      </div>
    </div>

    <section className="report-metrics" aria-label="Report summary">
      <Metric label="Tasks in range" value={report?.totalTasks ?? 0} icon={<Clock3 />} />
      <Metric label="Completed tasks" value={report?.completedTasks ?? 0} icon={<CheckCircle2 />} />
      <Metric label="Closed projects" value={report?.archivedProjects ?? 0} icon={<FolderPlus />} />
      <Metric label="Critical" value={report?.criticalTasks ?? 0} icon={<AlertTriangle />} tone="danger" />
    </section>

    <section className="report-grid">
      <BarChart title="Report status" items={report?.statusBreakdown ?? []} colors={statusChartColors} />
      <BarChart title="Report priority" items={report?.priorityBreakdown ?? []} colors={priorityChartColors} />
      <article className="report-card">
        <header><h2>Project delivery</h2><span>{report?.projectsDeliveredInRange ?? 0} in range</span></header>
        <div className="report-project-list">
          {projects.map((project) => {
            const delivery = deliveryStatus(project.deliveryDate)
            return <article key={project.id}>
              <strong>{project.name}</strong>
              <span>{project.deliveryDate ? formatDate(project.deliveryDate) : 'No delivery date'}</span>
              {delivery && <small className={`delivery-badge ${delivery.tone}`}>{delivery.label}</small>}
              <span>{project.completedTasks}/{project.totalTasks} tasks complete, {project.completionPercentage}% done</span>
              {project.isArchived && <small className="delivery-badge healthy">Closed project</small>}
            </article>
          })}
          {!projects.length && <p className="muted">No project delivery or archive date falls inside this range.</p>}
        </div>
      </article>
      <article className="report-card">
        <header><h2>Executive summary</h2><span>{dashboard.projectProgress.completionPercentage}% complete</span></header>
        <p>{report
          ? `${report.completedTasks} completed task${report.completedTasks === 1 ? '' : 's'} across ${report.totalProjects} project${report.totalProjects === 1 ? '' : 's'}, with ${report.overdueTasks} overdue due-date signal${report.overdueTasks === 1 ? '' : 's'} and ${report.blockedTasks} blocked task${report.blockedTasks === 1 ? '' : 's'} in this report range.`
          : 'Reports become richer after projects and tasks are created.'}</p>
      </article>
    </section>

    <section className="work-area report-table screen-only">
      <div className="table-head report-head"><span>Task</span><span>Status</span><span>Due</span><span>Priority</span></div>
      {loading ? <div className="loading">Loading reports...</div> : pageTasks.length
        ? pageTasks.map((task) => <article className="task-row report-row" key={task.id}>
          <div className="task-name"><span className={`priority-line ${task.priorityBand?.toLowerCase() ?? 'low'}`} /><div><strong>{task.title}</strong><small>{task.tags.length ? task.tags.join(', ') : 'No tags'}</small></div></div>
          <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
          <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ? formatDate(task.dueDate) : task.completedAt ? `Done ${new Date(task.completedAt).toLocaleDateString()}` : 'No date'}</span>
          <span className="score"><strong>{task.priorityBand ?? 'Unscored'}</strong><small>{task.priorityScore ?? 0}</small></span>
          <div className="metadata-line"><span className="category-pill">{task.projectName}</span></div>
        </article>)
        : <div className="empty compact"><ChartBar /><h2>No report rows</h2><p>No task activity matches this range for the selected workspace.</p></div>}
      <Pagination
        pageNumber={pageNumber}
        pageSize={pageSize}
        totalCount={tasks.length}
        totalPages={totalPages}
        onPageChange={setPageNumber}
      />
    </section>

    <section className="work-area report-table print-only" aria-hidden="true">
      <div className="table-head report-head"><span>Task</span><span>Status</span><span>Due</span><span>Priority</span></div>
      {tasks.map((task) => <article className="task-row report-row" key={task.id}>
        <div className="task-name"><span className={`priority-line ${task.priorityBand?.toLowerCase() ?? 'low'}`} /><div><strong>{task.title}</strong><small>{task.tags.length ? task.tags.join(', ') : 'No tags'}</small></div></div>
        <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
        <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ? formatDate(task.dueDate) : task.completedAt ? `Done ${new Date(task.completedAt).toLocaleDateString()}` : 'No date'}</span>
        <span className="score"><strong>{task.priorityBand ?? 'Unscored'}</strong><small>{task.priorityScore ?? 0}</small></span>
        <div className="metadata-line"><span className="category-pill">{task.projectName}</span></div>
      </article>)}
    </section>
  </section>
}

/**
 * Home-page governance panel: a derived health score, a risk register, suggested
 * decisions, and a release-readiness checklist, all computed client-side from the
 * current dashboard snapshot and visible tasks (see `buildRiskRegister`/`buildDecisionSuggestions`).
 */
function ProjectGovernance({
  dashboard,
  project,
  tasks,
}: {
  dashboard: Dashboard
  project: ProjectDetails | null
  tasks: TaskItem[]
}) {
  const delivery = project ? deliveryStatus(project.targetDate) : null
  const completion = dashboard.projectProgress.completionPercentage
  const openTasks = Math.max(0, dashboard.projectProgress.totalTasks - dashboard.projectProgress.completedTasks)
  const hasOverdue = dashboard.overdueTaskCount > 0
  const hasBlocked = dashboard.blockedTaskCount > 0
  const healthScore = Math.max(0, Math.min(100,
    100 -
    dashboard.overdueTaskCount * 18 -
    dashboard.blockedTaskCount * 12 -
    dashboard.criticalTaskCount * 6 -
    (delivery?.tone === 'critical' ? 20 : delivery?.tone === 'warning' ? 10 : 0) +
    Math.round(completion / 5),
  ))
  const healthTone = healthScore >= 80 ? 'healthy' : healthScore >= 55 ? 'warning' : 'critical'
  const healthLabel = healthTone === 'healthy'
    ? 'Healthy'
    : healthTone === 'warning'
      ? 'Needs attention'
      : 'At risk'
  const readiness = [
    { readyLabel: 'Active project selected', blockedLabel: 'Select an active project', ready: !!project },
    { readyLabel: 'Delivery date confirmed', blockedLabel: 'Delivery date missing', ready: !!project?.targetDate },
    { readyLabel: 'No overdue tasks', blockedLabel: `${dashboard.overdueTaskCount} overdue task${dashboard.overdueTaskCount === 1 ? '' : 's'} remaining`, ready: !hasOverdue },
    { readyLabel: 'No blocked work', blockedLabel: `${dashboard.blockedTaskCount} blocked task${dashboard.blockedTaskCount === 1 ? '' : 's'} remaining`, ready: !hasBlocked },
    { readyLabel: 'Completion above 80%', blockedLabel: `${completion}% complete; ${openTasks} open task${openTasks === 1 ? '' : 's'} remaining`, ready: completion >= 80 || openTasks === 0 },
    { readyLabel: 'No critical dashboard warnings', blockedLabel: 'Critical dashboard warning needs review', ready: dashboard.warnings.every((warning) => warning.severity !== 'critical') },
  ]
  const risks = buildRiskRegister(dashboard, delivery?.label, tasks)
  const activeRiskCount = risks.filter((risk) => risk.tone !== 'healthy').length
  const decisions = buildDecisionSuggestions(activeRiskCount, completion, openTasks)

  return <section className="governance-grid" aria-label="Project governance">
    <article className={`governance-card health-card ${healthTone}`}>
      <header><h2>Project health</h2><span>{healthLabel}</span></header>
      <div className="health-score"><strong>{healthScore}</strong><small>/100</small></div>
      <div className="progress-track governance-progress" aria-label={`Project health score ${healthScore} out of 100`}>
        <i style={{ width: `${healthScore}%` }} />
      </div>
      <p>{project ? `${project.name} is ${completion}% complete with ${openTasks} open tasks.` : 'Create a project to start governance tracking.'}</p>
      {delivery && <span className={`governance-delivery-badge ${delivery.tone}`}>{delivery.label}</span>}
    </article>

    <article className="governance-card">
      <header><h2>Risk register</h2><span>{activeRiskCount} signals</span></header>
      <ul className="governance-list risk-list">
        {risks.map((risk) => <li key={risk.title}>
          <span className={`risk-dot ${risk.tone}`} />
          <div><strong>{risk.title}</strong><p>{risk.detail}</p></div>
        </li>)}
      </ul>
    </article>

    <article className="governance-card">
      <header><h2>Decision log</h2><span>Suggested</span></header>
      <ul className="governance-list decision-list">
        {decisions.map((decision) => <li key={decision}>
          <MessageSquare size={15} />
          <span>{decision}</span>
        </li>)}
      </ul>
    </article>

    <article className="governance-card">
      <header><h2>Release readiness</h2><span>{readiness.filter((item) => item.ready).length}/{readiness.length}</span></header>
      <ul className="readiness-list">
        {readiness.map((item) => <li className={item.ready ? 'ready' : 'not-ready'} key={item.readyLabel}>
          <CheckCircle2 size={16} />
          <span>{item.ready ? item.readyLabel : item.blockedLabel}</span>
        </li>)}
      </ul>
    </article>
  </section>
}

// Derives a list of risk entries (title/detail/tone) from overdue/blocked/critical counts,
// delivery-date proximity, and unassigned tasks; falls back to a single "no risks" entry.
function buildRiskRegister(
  dashboard: Dashboard,
  deliveryLabel: string | undefined,
  tasks: TaskItem[],
) {
  const risks: { title: string; detail: string; tone: 'healthy' | 'warning' | 'critical' }[] = []
  if (dashboard.overdueTaskCount > 0) {
    risks.push({
      title: 'Deadline risk',
      detail: `${dashboard.overdueTaskCount} overdue task${dashboard.overdueTaskCount === 1 ? '' : 's'} should be reviewed before delivery.`,
      tone: 'critical',
    })
  }
  if (dashboard.blockedTaskCount > 0) {
    risks.push({
      title: 'Dependency risk',
      detail: `${dashboard.blockedTaskCount} blocked task${dashboard.blockedTaskCount === 1 ? '' : 's'} may need owner escalation.`,
      tone: 'warning',
    })
  }
  if (dashboard.criticalTaskCount > 0) {
    risks.push({
      title: 'Priority concentration',
      detail: `${dashboard.criticalTaskCount} critical task${dashboard.criticalTaskCount === 1 ? '' : 's'} need focused delivery attention.`,
      tone: 'warning',
    })
  }
  if (deliveryLabel?.includes('left') || deliveryLabel === 'Due today') {
    risks.push({
      title: 'Delivery window',
      detail: `Project delivery is ${deliveryLabel.toLowerCase()} with ${dashboard.activeTaskCount} active task${dashboard.activeTaskCount === 1 ? '' : 's'}.`,
      tone: deliveryLabel === 'Due today' ? 'critical' : 'warning',
    })
  }
  const unassigned = tasks.filter((task) => !task.assignedUserId).length
  if (unassigned > 0) {
    risks.push({
      title: 'Ownership gap',
      detail: `${unassigned} visible task${unassigned === 1 ? '' : 's'} do not have an assignee.`,
      tone: 'warning',
    })
  }
  if (!risks.length) {
    risks.push({
      title: 'No active risks',
      detail: 'Current project signals are within the expected delivery range.',
      tone: 'healthy',
    })
  }
  return risks
}

// Suggests a short list of next decisions based on active risk count, completion percentage, and open task count.
function buildDecisionSuggestions(riskCount: number, completion: number, openTasks: number) {
  if (riskCount === 0 && openTasks === 0) return ['Approve release closure and archive completed work.']
  const decisions = ['Confirm the next delivery checkpoint with the workspace team.']
  if (riskCount > 1) decisions.push('Escalate blockers and deadline risks in the next stand-up.')
  if (completion >= 80) decisions.push('Start release readiness review and final validation.')
  if (openTasks > 0) decisions.push('Assign owners to remaining open work before the next review.')
  return decisions
}

/** SVG donut chart for a breakdown (e.g. task status), with hover/focus tooltips per segment. */
function DonutChart({
  title,
  items,
  colors,
}: {
  title: string
  items: DashboardBreakdownItem[]
  colors: Record<string, string>
}) {
  const [activeSegment, setActiveSegment] = useState<string | null>(null)
  const total = items.reduce((sum, item) => sum + item.count, 0)
  const radius = 42
  const circumference = 2 * Math.PI * radius
  let offset = 0

  return <article className="analytics-card donut-card">
    <header><h2>{title}</h2><span>{total} tasks</span></header>
    <div className="donut-layout">
      <div className="donut-wrap">
        <svg className="donut-chart" viewBox="0 0 120 120" role="img" aria-label={`${title}: ${total} tasks`}>
          <circle cx="60" cy="60" r={radius} className="donut-track" />
          {total > 0 && items.filter((item) => item.count > 0).map((item) => {
            const length = item.count / total * circumference
            const percentage = chartPercentage(item.count, total)
            const detail = `${friendlyChartLabel(item.label)}: ${item.count} task${item.count === 1 ? '' : 's'} (${percentage}%)`
            const segment = <circle
              key={item.label}
              cx="60"
              cy="60"
              r={radius}
              className="donut-segment"
              aria-label={`${friendlyChartLabel(item.label)}: ${item.count} task${item.count === 1 ? '' : 's'}, ${percentage}% of ${title.toLowerCase()}`}
              tabIndex={0}
              onMouseEnter={() => setActiveSegment(detail)}
              onFocus={() => setActiveSegment(detail)}
              onMouseLeave={() => setActiveSegment(null)}
              onBlur={() => setActiveSegment(null)}
              stroke={colors[item.label] ?? '#73808d'}
              strokeDasharray={`${length} ${circumference - length}`}
              strokeDashoffset={-offset}
            >
              <title>{detail}</title>
            </circle>
            offset += length
            return segment
          })}
        </svg>
        {activeSegment && <div className="donut-tooltip" role="status">{activeSegment}</div>}
        <div className="donut-center"><strong>{total}</strong><span>Total</span></div>
      </div>
      <ChartLegend items={items} colors={colors} showPercent />
    </div>
  </article>
}

/** Horizontal bar chart for a breakdown (e.g. report status/priority), with per-row hover tooltips. */
function BarChart({
  title,
  items,
  colors,
}: {
  title: string
  items: DashboardBreakdownItem[]
  colors: Record<string, string>
}) {
  const max = Math.max(1, ...items.map((item) => item.count))
  const total = items.reduce((sum, item) => sum + item.count, 0)
  return <article className="analytics-card">
    <header><h2>{title}</h2><span>{total} total</span></header>
    <div className="bar-chart">
      {items.map((item) => <div
        className="bar-row chart-hover-target"
        data-tooltip={`${friendlyChartLabel(item.label)}: ${item.count} task${item.count === 1 ? '' : 's'} (${chartPercentage(item.count, total)}%)`}
        title={`${friendlyChartLabel(item.label)}: ${item.count} task${item.count === 1 ? '' : 's'} (${chartPercentage(item.count, total)}%)`}
        key={item.label}
      >
        <span>{friendlyChartLabel(item.label)}</span>
        <div className="bar-track"><i style={{
          width: `${item.count / max * 100}%`,
          backgroundColor: colors[item.label] ?? '#73808d',
        }} /></div>
        <strong>{item.count}</strong>
      </div>)}
    </div>
  </article>
}

/** Grouped bar chart of tasks created vs. completed per week over the last 6 weeks. */
function WeeklyFlowChart({ tasks }: { tasks: WorkspaceReport['tasks'] }) {
  const weeks = buildWeeklyFlowPoints(tasks)
  const max = Math.max(1, ...weeks.flatMap((week) => [week.created, week.completed]))
  const yAxis = buildFlowYAxis(max)
  const axisMax = yAxis[0]
  const totals = weeks.reduce(
    (summary, week) => ({
      created: summary.created + week.created,
      completed: summary.completed + week.completed,
    }),
    { created: 0, completed: 0 },
  )
  return <article className="analytics-card flow-card">
    <header>
      <h2>Weekly Flow</h2>
      <span>{totals.created} created</span>
    </header>
    <div className="flow-chart" role="img" aria-label="Weekly created and completed tasks">
      <div className="flow-y-axis" aria-hidden="true">
        {yAxis.map((tick) => <span key={tick}>{tick}</span>)}
      </div>
      <div className="flow-plot">
        <div className="flow-grid" aria-hidden="true">
          {yAxis.map((tick) => <span key={tick} />)}
        </div>
        {weeks.map((week) => <div
          className="flow-group chart-hover-target"
          data-tooltip={`${week.label}: ${week.created} created, ${week.completed} completed`}
          title={`${week.label}: ${week.created} created, ${week.completed} completed`}
          key={week.key}
        >
          <div className="flow-bars">
            <span
              className="flow-bar completed"
              style={{ height: `${flowBarHeight(week.completed, axisMax)}%` }}
            ><strong>{week.completed}</strong></span>
            <span
              className="flow-bar created"
              style={{ height: `${flowBarHeight(week.created, axisMax)}%` }}
            ><strong>{week.created}</strong></span>
          </div>
          <small>{week.label}</small>
        </div>)}
      </div>
    </div>
    <div className="chart-legend compact">
      <span><i style={{ backgroundColor: '#159b74' }} /> Completed <strong>{totals.completed}</strong></span>
      <span><i style={{ backgroundColor: '#2875d1' }} /> Created <strong>{totals.created}</strong></span>
    </div>
  </article>
}

/** Bar chart of assigned task counts per workspace member (including 0-count members), top 8. */
function WorkloadChart({
  tasks,
  members,
}: {
  tasks: WorkspaceReport['tasks']
  members: WorkspaceMember[]
}) {
  const items = buildWorkloadItems(tasks, members)
  const max = Math.max(1, ...items.map((item) => item.count))
  const total = items.reduce((sum, item) => sum + item.count, 0)
  return <article className="analytics-card workload-card">
    <header><h2>Workload</h2><span>{total} assigned</span></header>
    <div className="workload-list">
      {items.map((item) => <div
        className="workload-row chart-hover-target"
        data-tooltip={`${item.name}: ${item.count} assigned task${item.count === 1 ? '' : 's'}, ${item.completed} completed`}
        title={`${item.name}: ${item.count} assigned task${item.count === 1 ? '' : 's'}, ${item.completed} completed`}
        key={item.key}
      >
        <span>{item.name}</span>
        <div className="bar-track"><i style={{ width: `${item.count / max * 100}%` }} /></div>
        <strong>{item.count}</strong>
      </div>)}
    </div>
  </article>
}

/** Color-key legend row for a breakdown chart; optionally shows each item's percent of the total. */
function ChartLegend({
  items,
  colors,
  showPercent = false,
}: {
  items: DashboardBreakdownItem[]
  colors: Record<string, string>
  showPercent?: boolean
}) {
  const total = items.reduce((sum, item) => sum + item.count, 0)
  return <div className="chart-legend">
    {items.map((item) => <span key={item.label}>
      <i style={{ backgroundColor: colors[item.label] ?? '#73808d' }} />
      <span>{friendlyChartLabel(item.label)}</span>
      <strong>{item.count}{showPercent ? ` (${chartPercentage(item.count, total)}%)` : ''}</strong>
    </span>)}
  </div>
}

// Converts a raw breakdown label (e.g. the enum value 'InProgress') into a display-friendly string.
function friendlyChartLabel(label: string) {
  return label === 'InProgress' ? 'In progress' : label
}

// Rounds count/total to a whole-number percentage, guarding against divide-by-zero.
function chartPercentage(count: number, total: number) {
  return total > 0 ? Math.round(count * 100 / total) : 0
}

// Buckets tasks into 6 trailing 7-day windows and counts how many were created/completed in each,
// for the Weekly Flow chart.
function buildWeeklyFlowPoints(tasks: WorkspaceReport['tasks']) {
  const today = new Date()
  return Array.from({ length: 6 }, (_, index) => {
    const end = new Date(today)
    end.setDate(today.getDate() - (5 - index) * 7)
    const start = new Date(end)
    start.setDate(end.getDate() - 6)
    const startKey = start.toISOString().slice(0, 10)
    const endKey = end.toISOString().slice(0, 10)
    return {
      key: endKey,
      label: end.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      created: tasks.filter((task) => task.createdAt.slice(0, 10) >= startKey && task.createdAt.slice(0, 10) <= endKey).length,
      completed: tasks.filter((task) => task.completedAt && task.completedAt.slice(0, 10) >= startKey && task.completedAt.slice(0, 10) <= endKey).length,
    }
  })
}

// Converts a flow bar's value into a percentage bar height, with a small nonzero floor so
// zero-value bars are still visible and nonzero bars are never too short to read.
function flowBarHeight(value: number, max: number) {
  return value === 0 ? 4 : Math.max(18, value / max * 100)
}

// Picks 5 evenly-spaced y-axis tick values (top to 0) for the Weekly Flow chart, rounding
// the step up to a whole number so the axis reads cleanly.
function buildFlowYAxis(max: number) {
  const step = max <= 4 ? 1 : Math.ceil(max / 4)
  const top = Math.max(step * 4, max)
  return [top, top - step, top - step * 2, top - step * 3, 0]
}

// Groups tasks by assignee (including an "Unassigned" bucket) into workload chart rows,
// ensuring every workspace member appears even with zero assigned tasks, sorted by count.
function buildWorkloadItems(tasks: WorkspaceReport['tasks'], members: WorkspaceMember[]) {
  const memberNames = new Map(members.map((member) => [member.userId, member.displayName]))
  const grouped = new Map<string, { key: string; name: string; count: number; completed: number }>()
  tasks.forEach((task) => {
    const key = task.assignedUserId ?? 'unassigned'
    const current = grouped.get(key) ?? {
      key,
      name: key === 'unassigned' ? 'Unassigned' : memberNames.get(key) ?? 'Team member',
      count: 0,
      completed: 0,
    }
    current.count += 1
    if (task.status === 'Completed') current.completed += 1
    grouped.set(key, current)
  })
  members.forEach((member) => {
    if (!grouped.has(member.userId)) {
      grouped.set(member.userId, { key: member.userId, name: member.displayName, count: 0, completed: 0 })
    }
  })
  return [...grouped.values()]
    .sort((left, right) => right.count - left.count || left.name.localeCompare(right.name))
    .slice(0, 8)
}

/**
 * Workspace activity feed with a type filter, a notification digest, "load more" pagination,
 * and a couple of early-return empty states depending on whether activity/notifications exist.
 */
function ActivityPage({
  activity,
  tasks,
  notifications,
  loading,
  selectedType,
  onTypeChange,
  totalCount,
  hasMore,
  loadingMore,
  onLoadMore,
  members,
  currentUserId,
  onMarkNotificationRead,
}: {
  activity: WorkspaceActivity[]
  tasks: TaskItem[]
  notifications: NotificationItem[]
  loading: boolean
  selectedType: (typeof activityTypes)[number]
  onTypeChange: (type: (typeof activityTypes)[number]) => void
  totalCount: number
  hasMore: boolean
  loadingMore: boolean
  onLoadMore: () => void
  members: WorkspaceMember[]
  currentUserId: string
  onMarkNotificationRead: (id: string) => void
}) {
  if (loading) return <section className="work-area"><div className="loading">Loading activity...</div></section>
  const controls = <ActivityControls
    selectedType={selectedType}
    onTypeChange={onTypeChange}
  />
  const notificationSummary = <NotificationDigest
    notifications={notifications}
    onMarkRead={onMarkNotificationRead}
  />

  if (!activity.length) {
    return <section className="panel-page activity-page">
      {controls}
      {notificationSummary}
      <div className="empty compact"><Activity /><h2>No recorded activity yet</h2><p>Move tasks across the board or edit work items to build the timeline.</p></div>
      {!!tasks.length && <div className="activity-snapshot" aria-label="Current task snapshot">
        <h2>Current task snapshot</h2>
        {tasks.map((task) => <article className="activity-item" key={task.id}>
          <span className="activity-icon"><CircleGauge /></span>
          <div>
            <strong>{task.title}</strong>
            <p>{statusLabels[task.status]} - {task.deadlineHealth} - priority {task.priorityScore?.toFixed(1) ?? 'unscored'}</p>
            <small>{task.dueDate ? `Due ${task.dueDate}` : 'No due date'}</small>
          </div>
        </article>)}
      </div>}
    </section>
  }

  return <section className="panel-page activity-page">
    {controls}
    {notificationSummary}
    <section className="activity-notifications" aria-label="Activity timeline">
      <header>
        <div>
          <p className="eyebrow">Timeline</p>
          <h2>Activity timeline</h2>
        </div>
        <span>{`${totalCount} total`}</span>
      </header>
      <div
        className="activity-timeline-scroll"
        onScroll={(event) => {
          if (!hasMore || loadingMore) return
          const target = event.currentTarget
          if (target.scrollTop + target.clientHeight >= target.scrollHeight - 120) {
            onLoadMore()
          }
        }}
      >
        <div className="activity-timeline">
          {activity.map((item) => <article className="activity-item" key={item.sequence}>
          <span className="activity-icon">{activityIcon(item.action)}</span>
          <div>
            <strong>{item.taskTitle}</strong>
            <p>{activityMessage(item, members, currentUserId)}</p>
            <small>{item.projectName} - {new Date(item.occurredAt).toLocaleString()}</small>
          </div>
        </article>)}
        </div>
        <div className="activity-timeline-footer">
          {loadingMore
            ? <span>Loading more...</span>
            : hasMore
              ? <button type="button" className="secondary" onClick={onLoadMore}>Load more</button>
              : <span>{`Showing all ${totalCount} activity item${totalCount === 1 ? '' : 's'}.`}</span>}
        </div>
      </div>
    </section>
  </section>
}

/** Header row for the Activity page with a title and the activity-type filter dropdown. */
function ActivityControls({
  selectedType,
  onTypeChange,
}: {
  selectedType: (typeof activityTypes)[number]
  onTypeChange: (type: (typeof activityTypes)[number]) => void
}) {
  return <div className="activity-toolbar">
    <div>
      <p className="eyebrow">Audit trail</p>
      <h2>Workspace activity</h2>
    </div>
    <label>
      <span>Filter</span>
      <select
        value={selectedType}
        onChange={(event) => onTypeChange(event.target.value as (typeof activityTypes)[number])}
      >
        {activityTypes.map((type) =>
          <option value={type} key={type}>{activityTypeLabel(type)}</option>)}
      </select>
      <ChevronDown />
    </label>
  </div>
}

/** Full list of notifications shown inline on the Activity page; renders nothing if there are none. */
function NotificationDigest({
  notifications,
  onMarkRead,
}: {
  notifications: NotificationItem[]
  onMarkRead: (id: string) => void
}) {
  if (!notifications.length) return null

  const unreadCount = notifications.filter((item) => !item.read).length

  return <section className="activity-notifications" aria-label="All notifications">
    <header>
      <div>
        <p className="eyebrow">Notifications</p>
        <h2>Workspace reminders</h2>
      </div>
      <span>{unreadCount ? `${unreadCount} unread` : 'All read'}</span>
    </header>
    <div className="notification-list full">
      {notifications.map((notification) => <button
        type="button"
        className={`notification-item ${notification.severity} ${notification.read ? 'read' : 'unread'}`}
        key={notification.id}
        onClick={() => onMarkRead(notification.id)}
      >
        <span className="notification-status">{notification.read ? <CheckCircle2 size={16} /> : <AlertTriangle size={16} />}</span>
        <div>
          <strong>{notification.title}</strong>
          <p>{notification.message}</p>
          {notification.dueDate && <small>{formatDate(notification.dueDate)}</small>}
        </div>
      </button>)}
    </div>
  </section>
}

/**
 * Super-admin-only operations dashboard: health checks, runtime configuration, the
 * background reminder scheduler's status, and recent application logs.
 */
function OperationsPage({ summary }: { summary: OperationsSummary }) {
  const check = (name: string) =>
    summary.healthChecks.find((item) =>
      item.name.toLowerCase() === name.toLowerCase())
  const apiHealth = check('API running')
  const database = check('Database')
  const email = check('Email notifications')
  const reminders = check('Due date reminder runner')
  const statusText = (item?: OperationHealthCheck) =>
    item?.description ?? item?.status ?? 'Not reported'

  return <section className="panel-page operations-page" aria-label="Operations health and logs">
    <div className="activity-toolbar">
      <div>
        <p className="eyebrow">Super admin</p>
        <h2>Application monitoring</h2>
      </div>
      <span className={`status ${summary.overallHealth.toLowerCase()}`}>{summary.overallHealth}</span>
    </div>

    <div className="operations-overview">
      <OperationCard title="API running" value={apiHealth?.status ?? 'Unknown'} detail={statusText(apiHealth)} icon={<Activity />} />
      <OperationCard title="Database" value={database?.status ?? 'Unknown'} detail={statusText(database)} icon={<CircleGauge />} />
      <OperationCard title="Email" value={email?.status ?? 'Unknown'} detail={statusText(email)} icon={<Bell />} />
      <OperationCard title="Reminders" value={summary.reminderScheduler.status} detail={statusText(reminders)} icon={<Clock3 />} />
    </div>

    <div className="operations-grid">
      <article className="profile-card">
        <div className="profile-heading">
          <span className="metric-icon"><ShieldCheck /></span>
          <div>
            <h2>Service health</h2>
            <p>Generated {new Date(summary.generatedAt).toLocaleString()}.</p>
          </div>
        </div>
        <div className="health-check-list">
          {summary.healthChecks.map((check) => <article className="health-check-row" key={check.name}>
            <span className={`health-dot ${check.status.toLowerCase()}`} />
            <div>
              <strong>{check.name}</strong>
              <p>{check.description ?? `${check.status} in ${Math.round(check.durationMilliseconds)}ms`}</p>
            </div>
            <span>{Math.round(check.durationMilliseconds)}ms</span>
          </article>)}
        </div>
      </article>

      <article className="profile-card">
        <div className="profile-heading">
          <span className="metric-icon"><Settings2 /></span>
          <div>
            <h2>Runtime configuration</h2>
            <p>Deployment settings currently used by the API.</p>
          </div>
        </div>
        <div className="log-summary">
          <span><strong>Environment</strong>{summary.runtime.environment}</span>
          <span><strong>Database</strong>{summary.runtime.databaseProvider}</span>
          <span><strong>Frontend URL</strong>{summary.runtime.publicBaseUrl}</span>
          <span><strong>Business timezone</strong>{summary.runtime.timeZoneId}</span>
          <span><strong>Email mode</strong>{summary.runtime.emailMode}</span>
          <span><strong>CORS</strong>{summary.runtime.corsAllowedOrigins.length ? summary.runtime.corsAllowedOrigins.join(', ') : 'No origins configured'}</span>
          <span><strong>Reminder scheduler</strong>{summary.runtime.reminderSchedulerEnabled ? `Every ${summary.reminderScheduler.intervalMinutes} min` : 'Disabled'}</span>
          <span><strong>Log retention</strong>{`${summary.runtime.logRetentionDays} days / ${summary.runtime.logMaxEntries} entries`}</span>
          <span><strong>File logs</strong>{summary.runtime.logFileEnabled ? summary.runtime.logDirectory : 'Disabled'}</span>
        </div>
      </article>

      <article className="profile-card">
        <div className="profile-heading">
          <span className="metric-icon"><Clock3 /></span>
          <div>
            <h2>Automatic notifications</h2>
            <p>Background deadline reminders and My Day carryover alerts.</p>
          </div>
        </div>
        <div className="log-summary">
          <span><strong>Status</strong>{summary.reminderScheduler.status}</span>
          <span><strong>Interval</strong>{summary.reminderScheduler.intervalMinutes ? `${summary.reminderScheduler.intervalMinutes} minutes` : 'Not configured'}</span>
          <span><strong>Last run</strong>{summary.reminderScheduler.lastRunCompletedAt ? new Date(summary.reminderScheduler.lastRunCompletedAt).toLocaleString() : 'Not run yet'}</span>
          <span><strong>Next run</strong>{summary.reminderScheduler.nextRunAt ? new Date(summary.reminderScheduler.nextRunAt).toLocaleString() : 'Pending'}</span>
          <span><strong>Task reminders</strong>{summary.reminderScheduler.lastTaskReminderCount}</span>
          <span><strong>Project reminders</strong>{summary.reminderScheduler.lastProjectReminderCount}</span>
          <span><strong>Todo carryovers</strong>{summary.reminderScheduler.lastTodoCarryOverCount}</span>
          <span><strong>Emails sent</strong>{summary.reminderScheduler.lastEmailCount}</span>
          <span><strong>Last error</strong>{summary.reminderScheduler.lastError ?? 'None'}</span>
        </div>
      </article>

      <article className="profile-card operations-wide">
        <div className="profile-heading">
          <span className="metric-icon"><LayoutList /></span>
          <div>
            <h2>Recent application logs</h2>
            <p>Newest API log entries captured from the running application.</p>
          </div>
        </div>
        <div className="operation-log-list">
          {summary.recentLogs.length
            ? summary.recentLogs.map((entry, index) => <article className={`operation-log-row ${entry.level.toLowerCase()}`} key={`${entry.timestamp}-${index}`}>
                <span className="log-level">{entry.level}</span>
                <div>
                  <strong>{entry.message}</strong>
                  <small>{entry.category} - {new Date(entry.timestamp).toLocaleString()}{entry.eventId ? ` - event ${entry.eventId}` : ''}{entry.correlationId ? ` - trace ${entry.correlationId}` : ''}</small>
                  {entry.exception && <p>{entry.exception}</p>}
                </div>
              </article>)
            : <div className="empty compact"><Activity /><h2>No logs captured yet</h2><p>Use the application or run a reminder/email action to generate log entries.</p></div>}
        </div>
      </article>
    </div>
  </section>
}

/**
 * Super-admin-only page for triggering and downloading database backups; loads the
 * existing backup file list on mount.
 */
function DatabaseBackupsPage({ summary }: { summary: OperationsSummary }) {
  const [backups, setBackups] = useState<DatabaseBackupFile[]>([])
  const [backupBusy, setBackupBusy] = useState(false)
  const [backupError, setBackupError] = useState('')

  // Loads the current list of database backup files when the page mounts.
  useEffect(() => {
    let mounted = true
    api.operationBackups()
      .then((items) => {
        if (mounted) setBackups(items)
      })
      .catch((reason: unknown) => {
        if (mounted) setBackupError(
          reason instanceof Error ? reason.message : 'Backup files could not be loaded.')
      })

    return () => {
      mounted = false
    }
  }, [summary.generatedAt])

  const createBackup = async () => {
    try {
      setBackupBusy(true)
      setBackupError('')
      const created = await api.createOperationBackup()
      setBackups((items) => [
        created,
        ...items.filter((item) => item.fileName !== created.fileName),
      ])
    } catch (reason: unknown) {
      setBackupError(reason instanceof Error ? reason.message : 'Backup could not be created.')
    } finally {
      setBackupBusy(false)
    }
  }

  const downloadBackup = async (fileName: string) => {
    try {
      setBackupBusy(true)
      setBackupError('')
      const blob = await api.downloadOperationBackup(fileName)
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      document.body.appendChild(link)
      link.click()
      link.remove()
      URL.revokeObjectURL(url)
    } catch (reason: unknown) {
      setBackupError(reason instanceof Error ? reason.message : 'Backup could not be downloaded.')
    } finally {
      setBackupBusy(false)
    }
  }

  return <section className="panel-page operations-page" aria-label="Database backup operations">
    <div className="activity-toolbar">
      <div>
        <p className="eyebrow">Super admin</p>
        <h2>Database backups</h2>
        <p>Automatic snapshots with 7-day retention by default. On Render free tier, files are local to the running container unless persistent storage is attached.</p>
      </div>
      <button className="primary" disabled={backupBusy} onClick={() => void createBackup()}>
        <Save size={16} /> {backupBusy ? 'Working...' : 'Create backup now'}
      </button>
    </div>

    <div className="operations-overview">
      <OperationCard title="Status" value={summary.databaseBackups.status} detail={summary.databaseBackups.enabled ? 'Scheduler enabled' : 'Scheduler disabled'} icon={<Save />} />
      <OperationCard title="Interval" value={summary.databaseBackups.intervalHours ? `${summary.databaseBackups.intervalHours}h` : 'Unset'} detail="Automatic backup cadence" icon={<Clock3 />} />
      <OperationCard title="Last backup" value={summary.databaseBackups.lastBackupFileName ? 'Created' : 'None'} detail={summary.databaseBackups.lastBackupFileName ?? 'Not run yet'} icon={<CheckCircle2 />} />
      <OperationCard title="Files" value={String(backups.length)} detail="Available backup downloads" icon={<LayoutList />} />
    </div>

    <div className="operations-grid">
      <article className="profile-card">
        <div className="profile-heading">
          <span className="metric-icon"><Settings2 /></span>
          <div>
            <h2>Backup scheduler</h2>
            <p>Current automatic backup runtime state.</p>
          </div>
        </div>
        <div className="log-summary">
          <span><strong>Status</strong>{summary.databaseBackups.status}</span>
          <span><strong>Interval</strong>{summary.databaseBackups.intervalHours ? `${summary.databaseBackups.intervalHours} hours` : 'Not configured'}</span>
          <span><strong>Last backup</strong>{summary.databaseBackups.lastBackupFileName ?? 'Not run yet'}</span>
          <span><strong>Last run</strong>{summary.databaseBackups.lastRunCompletedAt ? new Date(summary.databaseBackups.lastRunCompletedAt).toLocaleString() : 'Not run yet'}</span>
          <span><strong>Next run</strong>{summary.databaseBackups.nextRunAt ? new Date(summary.databaseBackups.nextRunAt).toLocaleString() : 'Pending'}</span>
          <span><strong>Last error</strong>{summary.databaseBackups.lastError ?? 'None'}</span>
        </div>
      </article>

      <article className="profile-card operations-wide">
        <div className="profile-heading">
          <span className="metric-icon"><Save /></span>
          <div>
            <h2>Backup files</h2>
            <p>Download JSON database snapshots for recovery or audit review.</p>
          </div>
        </div>
        {backupError && <p className="field-error">{backupError}</p>}
        <div className="operation-log-list compact">
          {backups.length
            ? backups.map((backup) => <article className="operation-log-row backup-row" key={backup.fileName}>
                <span className="log-level">JSON</span>
                <div>
                  <strong>{backup.fileName}</strong>
                  <small>{formatBytes(backup.sizeBytes)} - created {new Date(backup.createdAt).toLocaleString()}</small>
                </div>
                <button className="secondary" disabled={backupBusy} onClick={() => void downloadBackup(backup.fileName)}>Download</button>
              </article>)
            : <div className="empty compact"><Save /><h2>No backups yet</h2><p>Create one manually or wait for the startup scheduler to complete.</p></div>}
        </div>
      </article>
    </div>
  </section>
}

/** Super-admin dialog for suspending a workspace (revocable), with an optional reason note. */
function SuspendWorkspaceDialog({
  workspaceName,
  busy,
  onClose,
  onConfirm,
}: {
  workspaceName: string
  busy: boolean
  onClose: () => void
  onConfirm: (reason: string) => void
}) {
  const [reason, setReason] = useState('')
  return <div className="dialog-backdrop" role="presentation">
    <dialog open aria-labelledby="suspend-workspace-title">
      <header>
        <div>
          <p className="eyebrow">Suspend workspace</p>
          <h2 id="suspend-workspace-title">Suspend {workspaceName}?</h2>
        </div>
        <button className="icon-button" disabled={busy} onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <form onSubmit={(event) => { event.preventDefault(); onConfirm(reason) }}>
        <p className="field-help">
          Members lose access immediately and the workspace disappears from their workspace switcher.
          Nothing is deleted, and you can reactivate it at any time.
        </p>
        <label>Reason (optional)
          <textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            maxLength={500}
            rows={3}
            autoFocus
          />
        </label>
        <footer>
          <button type="button" className="secondary" disabled={busy} onClick={onClose}>Cancel</button>
          <button className="primary danger-primary" disabled={busy}>{busy ? 'Suspending...' : 'Suspend workspace'}</button>
        </footer>
      </form>
    </dialog>
  </div>
}

/**
 * Super-admin platform console: lists every workspace on the instance with the ability
 * to drill into a workspace's detail, suspend/reactivate it, or delete a workspace/project.
 */
function PlatformPage() {
  const [summaries, setSummaries] = useState<PlatformWorkspaceSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string | null>(null)
  const [detail, setDetail] = useState<PlatformWorkspaceDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [busyWorkspaceId, setBusyWorkspaceId] = useState<string | null>(null)
  const [suspendTarget, setSuspendTarget] = useState<PlatformWorkspaceSummary | null>(null)
  const [deleteWorkspaceTarget, setDeleteWorkspaceTarget] = useState<PlatformWorkspaceSummary | null>(null)
  const [deleteProjectTarget, setDeleteProjectTarget] = useState<PlatformProjectSummary | null>(null)

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setSummaries(await api.platformWorkspaces())
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load platform data.')
    } finally {
      setLoading(false)
    }
  }

  // Loads the platform-wide workspace list once on mount.
  useEffect(() => { void load() }, [])

  const openDetail = async (workspaceId: string) => {
    setSelectedWorkspaceId(workspaceId)
    setDetailLoading(true)
    setDetail(null)
    try {
      setDetail(await api.platformWorkspaceDetail(workspaceId))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load workspace detail.')
    } finally {
      setDetailLoading(false)
    }
  }

  const closeDetail = () => {
    setSelectedWorkspaceId(null)
    setDetail(null)
  }

  const suspend = async (reason: string) => {
    if (!suspendTarget) return
    const target = suspendTarget
    setBusyWorkspaceId(target.workspaceId)
    try {
      await api.suspendWorkspace(target.workspaceId, reason)
      setNotice(`${target.workspaceName} suspended.`)
      setSuspendTarget(null)
      await load()
      if (selectedWorkspaceId === target.workspaceId) await openDetail(target.workspaceId)
    } catch (reason2) {
      setError(reason2 instanceof Error ? reason2.message : 'Unable to suspend workspace.')
    } finally {
      setBusyWorkspaceId(null)
    }
  }

  const reactivate = async (summary: PlatformWorkspaceSummary) => {
    setBusyWorkspaceId(summary.workspaceId)
    try {
      await api.reactivateWorkspace(summary.workspaceId)
      setNotice(`${summary.workspaceName} reactivated.`)
      await load()
      if (selectedWorkspaceId === summary.workspaceId) await openDetail(summary.workspaceId)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to reactivate workspace.')
    } finally {
      setBusyWorkspaceId(null)
    }
  }

  const confirmDeleteWorkspace = async () => {
    if (!deleteWorkspaceTarget) return
    const target = deleteWorkspaceTarget
    setBusyWorkspaceId(target.workspaceId)
    try {
      await api.deleteWorkspace(target.workspaceId)
      setNotice(`${target.workspaceName} deleted.`)
      setDeleteWorkspaceTarget(null)
      if (selectedWorkspaceId === target.workspaceId) closeDetail()
      await load()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to delete workspace.')
    } finally {
      setBusyWorkspaceId(null)
    }
  }

  const confirmDeleteProject = async () => {
    if (!deleteProjectTarget || !selectedWorkspaceId) return
    const workspaceId = selectedWorkspaceId
    setBusyWorkspaceId(workspaceId)
    try {
      await api.deleteProject(deleteProjectTarget.projectId)
      setNotice(`${deleteProjectTarget.projectName} deleted.`)
      setDeleteProjectTarget(null)
      await openDetail(workspaceId)
      await load()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to delete project.')
    } finally {
      setBusyWorkspaceId(null)
    }
  }

  const taskChartItems = [...summaries]
    .sort((left, right) => right.taskCount - left.taskCount)
    .slice(0, 10)
    .map((summary) => ({ label: summary.workspaceName, count: summary.taskCount }))

  return <section className="panel-page platform-page" aria-label="Platform administration">
    <div className="activity-toolbar">
      <div>
        <p className="eyebrow">Super admin</p>
        <h2>Platform workspaces</h2>
        <p>{summaries.length} workspace{summaries.length === 1 ? '' : 's'} across the platform, one per owner.</p>
      </div>
    </div>

    {notice && <div className="success-state"><CheckCircle2 /><span>{notice}</span><button type="button" onClick={() => setNotice('')}>Dismiss</button></div>}
    {error && <div className="error-state"><AlertTriangle /><span>{error}</span><button type="button" onClick={() => setError('')}>Dismiss</button></div>}

    {loading
      ? <div className="loading">Loading platform data...</div>
      : summaries.length === 0
        ? <div className="empty compact"><UserRound /><h2>No workspaces yet</h2></div>
        : <>
          {taskChartItems.some((item) => item.count > 0) &&
            <BarChart title="Tasks per workspace" items={taskChartItems} colors={{}} />}
          <div className="report-table">
            <div className="table-head platform-head">
              <span>Owner / workspace</span>
              <span>Roles</span>
              <span>Projects</span>
              <span>Sprints</span>
              <span>Tasks</span>
              <span>Status</span>
              <span>Actions</span>
            </div>
            {summaries.map((summary) => <article className="task-row platform-row" key={summary.workspaceId}>
              <div className="platform-owner">
                <strong>{summary.workspaceName}</strong>
                <small>{summary.ownerName} - {summary.ownerEmail}</small>
              </div>
              <span>{summary.managerCount} mgr - {summary.memberCount} mem</span>
              <span>{summary.projectCount}</span>
              <span>{summary.sprintCount}</span>
              <span>{summary.taskCount}</span>
              <span className={`platform-status-badge ${summary.isSuspended ? 'suspended' : 'active'}`}>
                {summary.isSuspended ? 'Suspended' : 'Active'}
              </span>
              <div className="platform-row-actions">
                <button type="button" className="secondary" onClick={() => void openDetail(summary.workspaceId)}>View</button>
                {summary.isSuspended
                  ? <button type="button" className="secondary" disabled={busyWorkspaceId === summary.workspaceId} onClick={() => void reactivate(summary)}>Reactivate</button>
                  : <button type="button" className="secondary" disabled={busyWorkspaceId === summary.workspaceId} onClick={() => setSuspendTarget(summary)}>Suspend</button>}
                <button type="button" className="secondary danger-action" disabled={busyWorkspaceId === summary.workspaceId} onClick={() => setDeleteWorkspaceTarget(summary)}>Delete</button>
              </div>
            </article>)}
          </div>
        </>}

    {selectedWorkspaceId && <div className="platform-detail panel-page">
      <div className="platform-detail-header">
        <div>
          <p className="eyebrow">Workspace detail</p>
          <h2>{detail?.workspaceName ?? 'Loading...'}</h2>
          {detail?.isSuspended && <p className="field-error">Suspended{detail.suspendedReason ? `: ${detail.suspendedReason}` : ''}</p>}
        </div>
        <button className="icon-button" onClick={closeDetail} aria-label="Close detail"><X /></button>
      </div>

      {detailLoading || !detail
        ? <div className="loading">Loading...</div>
        : <>
          <div className="platform-detail-grid">
            <DonutChart title="Task status" items={detail.dashboard.statusBreakdown} colors={statusChartColors} />
            <BarChart title="Task priority" items={detail.dashboard.priorityBreakdown} colors={priorityChartColors} />
          </div>

          <h3>Members ({detail.members.length})</h3>
          <div className="member-list">
            {detail.members.map((member) => <article className="member-row" key={member.userId}>
              <span className="avatar">{member.displayName.slice(0, 1).toUpperCase()}</span>
              <div><strong>{member.displayName}</strong><small>{member.email}</small></div>
              <span className={`role-pill ${member.role.toLowerCase()}`}>{member.role}</span>
            </article>)}
          </div>

          <h3>Projects ({detail.projects.length})</h3>
          <div className="platform-project-list">
            {detail.projects.length
              ? detail.projects.map((project) => <article className="platform-project-row" key={project.projectId}>
                  <div>
                    <strong>{project.projectName}</strong>
                    {project.isArchived && <small> - Archived</small>}
                  </div>
                  <span>{project.sprintCount} sprint{project.sprintCount === 1 ? '' : 's'}</span>
                  <span>{project.taskCount} task{project.taskCount === 1 ? '' : 's'}</span>
                  <button type="button" className="secondary danger-action" onClick={() => setDeleteProjectTarget(project)}>Delete</button>
                </article>)
              : <p className="empty compact">No projects yet.</p>}
          </div>
        </>}
    </div>}

    {suspendTarget && <SuspendWorkspaceDialog
      workspaceName={suspendTarget.workspaceName}
      busy={busyWorkspaceId === suspendTarget.workspaceId}
      onClose={() => setSuspendTarget(null)}
      onConfirm={(reason) => void suspend(reason)}
    />}

    {deleteWorkspaceTarget && <WorkspaceDeleteDialog
      workspace={{ id: deleteWorkspaceTarget.workspaceId, name: deleteWorkspaceTarget.workspaceName, role: 'Owner' }}
      busy={busyWorkspaceId === deleteWorkspaceTarget.workspaceId}
      onClose={() => setDeleteWorkspaceTarget(null)}
      onConfirm={() => void confirmDeleteWorkspace()}
    />}

    {deleteProjectTarget && <ProjectDeleteDialog
      project={{
        id: deleteProjectTarget.projectId,
        name: deleteProjectTarget.projectName,
        description: null,
        targetDate: null,
        isArchived: deleteProjectTarget.isArchived,
        archivedAt: null,
        categories: [],
        sprints: [],
      }}
      busy={busyWorkspaceId === selectedWorkspaceId}
      onClose={() => setDeleteProjectTarget(null)}
      onConfirm={() => void confirmDeleteProject()}
    />}
  </section>
}

/** Small status card used on the Operations page (health checks, backup status, etc.). */
function OperationCard({ title, value, detail, icon }: { title: string; value: string; detail: string; icon: ReactNode }) {
  return <article className={`operation-card ${value.toLowerCase()}`}>
    <span className="metric-icon">{icon}</span>
    <div><strong>{title}</strong><b>{value}</b><small>{detail}</small></div>
  </article>
}

// Builds a human-readable sentence describing one activity log entry, based on its action type.
function activityMessage(item: WorkspaceActivity, members: WorkspaceMember[] = [], currentUserId = '') {
  const actor = displayActor(item.actor, members, currentUserId)
  const previous = item.previousValue || 'none'
  const current = item.currentValue || 'none'
  switch (item.action) {
    case 'TaskCreated':
      return `${actor} created this task.`
    case 'TaskRenamed':
      return `${actor} renamed the task from ${previous} to ${current}.`
    case 'StatusChanged':
      return `${actor} moved the task from ${friendlyChartLabel(previous)} to ${friendlyChartLabel(current)}.`
    case 'DueDateChanged':
      return `${actor} changed the due date from ${formatActivityValue(previous)} to ${formatActivityValue(current)}.`
    case 'EffortChanged':
      return `${actor} changed the effort estimate from ${previous || 'none'} to ${current || 'none'}.`
    case 'AssignmentChanged':
      return `${actor} changed the assignee from ${displayActor(previous, members, currentUserId)} to ${displayActor(current, members, currentUserId)}.`
    case 'CategoryChanged':
      return `${actor} changed the category from ${previous} to ${current}.`
    case 'TagAdded':
      return `${actor} added tag #${current}.`
    case 'TagRemoved':
      return `${actor} removed tag #${previous}.`
    case 'NoteAdded':
      return `${actor} added a note: ${current}`
    default:
      return `${actor} changed ${activityLabel(item)} from ${previous} to ${current}.`
  }
}

// Resolves an activity log actor id into a display name ('You', 'System', a member's name, or the raw value).
function displayActor(value: string, members: WorkspaceMember[], currentUserId: string) {
  if (!value || value === 'none') return 'none'
  if (value === 'system') return 'System'
  if (value === currentUserId) return 'You'
  return members.find((member) => member.userId === value)?.displayName ?? value
}

// Picks a representative icon for an activity log entry based on its action type.
function activityIcon(action: string) {
  switch (action) {
    case 'TaskCreated':
      return <Plus />
    case 'StatusChanged':
      return <Activity />
    case 'TagAdded':
    case 'TagRemoved':
      return <Tags />
    case 'NoteAdded':
      return <MessageSquare />
    case 'AssignmentChanged':
      return <UserPlus />
    default:
      return <CircleGauge />
  }
}

// Converts a PascalCase activity type into a spaced display label for the filter dropdown.
function activityTypeLabel(type: string) {
  return type === 'All'
    ? 'All activity'
    : type.replace(/([A-Z])/g, ' $1').trim()
}

// Formats an activity value as a localized date if it parses as one, otherwise returns it as-is.
function formatActivityValue(value: string) {
  if (!value) return 'none'
  const date = new Date(`${value}T00:00:00`)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString()
}

// Converts a PascalCase activity action into a lowercase spaced label for generic messages.
function activityLabel(item: Pick<WorkspaceActivity, 'action'>) {
  return item.action
    .replace(/([A-Z])/g, ' $1')
    .trim()
    .toLowerCase()
}

/** Generic pagination footer; collapses to a plain item count when everything fits on one page. */
function Pagination({
  pageNumber,
  pageSize,
  totalCount,
  totalPages,
  itemLabel = 'task',
  onPageChange,
}: {
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  itemLabel?: string
  onPageChange: (page: number) => void
}) {
  if (totalCount <= pageSize) {
    return <footer className="pagination"><span>{totalCount} {itemLabel}{totalCount === 1 ? '' : 's'}</span></footer>
  }

  const start = (pageNumber - 1) * pageSize + 1
  const end = Math.min(pageNumber * pageSize, totalCount)

  return <footer className="pagination" aria-label={`${itemLabel} pagination`}>
    <span>Showing {start}-{end} of {totalCount}</span>
    <div>
      <button className="secondary" disabled={pageNumber === 1} onClick={() => onPageChange(pageNumber - 1)}>Previous</button>
      <strong>Page {pageNumber} of {totalPages}</strong>
      <button className="secondary" disabled={pageNumber === totalPages} onClick={() => onPageChange(pageNumber + 1)}>Next</button>
    </div>
  </footer>
}

/**
 * Team management page: member list with role changes/removal, and (Owner-only) an
 * invitation form plus pending invitation list with cancel support.
 */
function TeamPage({
  workspace,
  members,
  invitations,
  currentUserId,
  onMemberRemoved,
  onRoleChanged,
  onInvited,
  onInvitationCancelled,
}: {
  workspace: Workspace | null
  members: WorkspaceMember[]
  invitations: WorkspaceInvitation[]
  currentUserId?: string
  onMemberRemoved: (userId: string) => Promise<void>
  onRoleChanged: (userId: string, role: 'Manager' | 'Member') => Promise<void>
  onInvited: (fullName: string, email: string, role: 'Manager' | 'Member') => Promise<void>
  onInvitationCancelled: (invitationId: string) => Promise<void>
}) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const canManage = workspace?.role === 'Owner'
  const invite = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const form = event.currentTarget
    setBusy(true)
    setError('')
    const data = new FormData(form)
    try {
      await onInvited(
        String(data.get('fullName')),
        String(data.get('email')),
        String(data.get('role')) as 'Manager' | 'Member',
      )
      form.reset()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Invitation could not be created.')
    } finally {
      setBusy(false)
    }
  }

  return <section className="settings-grid">
    <section className="panel-page member-admin">
      <div className="settings-section compact-heading">
        <div>
          <h2>Team members</h2>
          <p>{workspace ? `${workspace.name} access and roles.` : 'Workspace access and roles.'}</p>
        </div>
      </div>
      <div className="member-list">
        {members.map((member) => <article className="member-row" key={member.userId}>
          <span className="avatar">{initials(member.displayName)}</span>
          <div><strong>{member.displayName}</strong><small>{member.email}</small></div>
          {canManage && member.role !== 'Owner'
            ? <label className="role-select" aria-label={`${member.displayName} role`}>
                <select value={member.role} disabled={busy} onChange={(event) => {
                  setBusy(true)
                  onRoleChanged(
                    member.userId,
                    event.target.value as 'Manager' | 'Member',
                  ).finally(() => setBusy(false))
                }}>
                  <option value="Member">Member</option>
                  <option value="Manager">Manager</option>
                </select>
                <ChevronDown />
              </label>
            : <span className={`role-pill ${member.role.toLowerCase()}`}>{member.role}</span>}
          {canManage && member.role !== 'Owner' && member.userId !== currentUserId &&
            <button className="icon-button danger-action" disabled={busy} onClick={() => {
              setBusy(true)
              onMemberRemoved(member.userId).finally(() => setBusy(false))
            }} aria-label={`Remove ${member.displayName}`} title="Remove member"><Trash2 /></button>}
        </article>)}
      </div>
      {!canManage && <p className="muted">Only the workspace owner can invite or remove members.</p>}
    </section>

    {canManage && <section className="panel-page invite-admin">
      <div className="settings-section compact-heading">
        <div>
          <h2>Invite people</h2>
          <p>Create one-time invitation links. Invitees appear as members only after they accept.</p>
        </div>
      </div>
      <form className="invite-form" onSubmit={(event) => void invite(event)}>
        <label>Full name<input name="fullName" required maxLength={120} /></label>
        <label>Email<input name="email" type="email" required maxLength={180} /></label>
        <label>Role<select name="role" defaultValue="Member"><option value="Member">Member</option><option value="Manager">Manager</option></select><ChevronDown /></label>
        {error && <p className="field-error">{error}</p>}
        <footer><button className="primary" disabled={busy}><UserPlus size={16} /> Send invite</button></footer>
      </form>
      <div className="invitation-list">
        {!!invitations.length && <h3>Pending invitations</h3>}
        {invitations.map((invitation) => <article className="invitation-row" key={invitation.id}>
          <div><strong>{invitation.fullName}</strong><small>{invitation.email} - {invitation.status}</small></div>
          {invitation.inviteLink
            ? <a href={invitation.inviteLink}>{invitation.inviteLink}</a>
            : <code>No active link</code>}
          {invitation.status === 'Pending' && <button className="secondary" disabled={busy} onClick={() => {
            setBusy(true)
            onInvitationCancelled(invitation.id).finally(() => setBusy(false))
          }}>Cancel</button>}
        </article>)}
        {!invitations.length && <p className="muted">No pending invitations for this workspace.</p>}
      </div>
    </section>}
  </section>
}

/** Account settings page: profile (display name/email) form and a separate change-password form. */
function ProfilePage({
  profile,
  account,
  role,
  onSave,
  onPasswordChanged,
  onLogout,
}: {
  profile: UserProfile
  account: AccountSession | null
  role: Workspace['role'] | null
  onSave: (profile: UserProfile) => Promise<void>
  onPasswordChanged: (
    currentPassword: string,
    newPassword: string,
  ) => Promise<void>
  onLogout: () => void
}) {
  const [draft, setDraft] = useState(profile)
  const [password, setPassword] = useState({ current: '', next: '', confirm: '' })
  const [passwordError, setPasswordError] = useState('')
  const [profileError, setProfileError] = useState('')
  const [savingProfile, setSavingProfile] = useState(false)
  const [savingPassword, setSavingPassword] = useState(false)
  // Resyncs the editable draft whenever the saved profile changes (e.g. after a successful save).
  useEffect(() => setDraft(profile), [profile])

  return <section className="profile-grid">
    <form className="panel-page profile-card" onSubmit={(event) => {
      event.preventDefault()
      setSavingProfile(true)
      setProfileError('')
      Promise.resolve(onSave(draft))
        .catch((reason) => setProfileError(
          reason instanceof Error ? reason.message : 'Profile could not be updated.'))
        .finally(() => setSavingProfile(false))
    }}>
      <div className="profile-heading"><span className="avatar large">{initials(draft.displayName)}</span><div><h2>Personal details</h2><p>{account ? `Signed in as ${account.email}.` : 'Using the development workspace session.'}</p></div></div>
      <label>Full name<input value={draft.displayName} readOnly /></label>
      <label>Email<input type="email" value={draft.email} onChange={(event) => setDraft({ ...draft, email: event.target.value })} required maxLength={180} /></label>
      <label>Workspace role<input value={role ?? 'Member'} readOnly /></label>
      {profileError && <p className="field-error">{profileError}</p>}
      <footer><button className="primary" disabled={savingProfile}><Save size={16} /> {savingProfile ? 'Saving...' : 'Save profile'}</button></footer>
    </form>

    <form className="panel-page profile-card" onSubmit={(event) => {
      event.preventDefault()
      setPasswordError('')
      if (password.next.length < 8) {
        setPasswordError('Use at least 8 characters.')
        return
      }
      if (password.next !== password.confirm) {
        setPasswordError('New password and confirmation must match.')
        return
      }
      setSavingPassword(true)
      onPasswordChanged(password.current, password.next)
        .then(() => setPassword({ current: '', next: '', confirm: '' }))
        .catch((reason) => setPasswordError(
          reason instanceof Error ? reason.message : 'Password could not be changed.'))
        .finally(() => setSavingPassword(false))
    }}>
      <div className="profile-heading"><span className="metric-icon"><ShieldCheck /></span><div><h2>Password</h2><p>Change the password used for this account.</p></div></div>
      <label>Current password<input type="password" value={password.current} required onChange={(event) => setPassword({ ...password, current: event.target.value })} autoComplete="current-password" /></label>
      <label>New password<input type="password" value={password.next} required onChange={(event) => setPassword({ ...password, next: event.target.value })} autoComplete="new-password" /></label>
      <label>Confirm password<input type="password" value={password.confirm} required onChange={(event) => setPassword({ ...password, confirm: event.target.value })} autoComplete="new-password" /></label>
      {passwordError && <p className="field-error">{passwordError}</p>}
      <footer><button className="secondary" type="button" onClick={onLogout}><LogOut size={16} /> Logout</button><button className="primary" disabled={savingPassword}>{savingPassword ? 'Changing...' : 'Change password'}</button></footer>
    </form>
  </section>
}

/** Flat table view of tasks (used on the Tasks page) with status, deadline, and priority score columns. */
function TaskList({ tasks, categories, sprints, members, currentUserId, onEdit, onDelete }: { tasks: TaskItem[]; categories: ProjectCategory[]; sprints: Sprint[]; members: WorkspaceMember[]; currentUserId: string; onEdit: (task: TaskItem) => void; onDelete: (task: TaskItem) => void }) {
  const categoryNames = new Map(categories.map((category) => [category.id, category.name]))
  const sprintNames = new Map(sprints.map((sprint) => [sprint.id, sprint.name]))
  const memberNames = new Map(members.map((member) => [member.userId, member.displayName]))
  if (!tasks.length) return <div className="empty"><Search /><h2>No matching work</h2><p>Try a different search term.</p></div>
  return <div className="task-table"><div className="table-head"><span>Task</span><span>Status</span><span>Created</span><span>Deadline</span><span>Priority</span><span /></div>
    {tasks.map((task) => <article className="task-row" key={task.id}>
      <div className="task-name"><span className={`priority-line ${task.priorityBand?.toLowerCase()}`} /><div><strong>{task.title}</strong><small>{task.priorityExplanation ? `Value ${task.priorityExplanation.businessValueContribution} · Urgency ${task.priorityExplanation.urgencyContribution} · Risk ${task.priorityExplanation.riskReductionContribution}` : 'Planning factors not set'}</small></div></div>
      <TaskMetadataLine task={task} categoryName={task.categoryId ? categoryNames.get(task.categoryId) : undefined} sprintName={task.sprintId ? sprintNames.get(task.sprintId) : undefined} assigneeName={task.assignedUserId ? memberNames.get(task.assignedUserId) : undefined} currentUserId={currentUserId} />
      <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
      <span className="created-date">{formatDate(task.createdAt)}</span>
      <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ?? 'Not scheduled'}</span>
      <span className="score"><strong>{task.priorityScore?.toFixed(1) ?? '—'}</strong><small>{task.priorityBand ?? 'Unscored'}</small></span>
      <div className="row-actions"><button className="icon-button" onClick={() => onEdit(task)} aria-label={`Edit ${task.title}`} title="Edit task"><Pencil /></button>{task.createdByUserId === currentUserId && <button className="icon-button danger-action" onClick={() => onDelete(task)} aria-label={`Delete ${task.title}`} title="Delete task"><Trash2 /></button>}</div>
    </article>)}
  </div>
}

/** Row of small pills (assignee, sprint, category, up to 3 tags) shown under a task's title. */
function TaskMetadataLine({ task, categoryName, sprintName, assigneeName, currentUserId }: { task: TaskItem; categoryName?: string; sprintName?: string; assigneeName?: string; currentUserId?: string }) {
  const assigneeLabel = task.assignedUserId
    ? task.assignedUserId === currentUserId
      ? 'Assigned to you'
      : `Assigned to ${assigneeName ?? 'workspace member'}`
    : 'Unassigned'
  if (!categoryName && !sprintName && !task.tags.length && !assigneeLabel) return null
  return <span className="metadata-line">
    <span className={`assignee-pill ${task.assignedUserId ? 'assigned' : 'unassigned'}`}>{assigneeLabel}</span>
    {sprintName && <span className="sprint-pill"><Clock3 size={12} /> {sprintName}</span>}
    {categoryName && <span className="category-pill"><FolderPlus size={12} /> {categoryName}</span>}
    {task.tags.slice(0, 3).map((tag) => <span className="tag-chip" key={tag}><Tags size={12} /> {tag}</span>)}
  </span>
}

/**
 * Kanban board: renders one `BoardColumn` per status and wires up dnd-kit drag-and-drop
 * so cards can be dragged between columns, calling `onMove` (which itself enforces the
 * allowed-transition/permission rules) on drop. Includes a `DragOverlay` clone of the
 * dragged card and a manual-recovery effect for a known dnd-kit pointerup timing issue.
 */
function Board({
  tasks,
  categories,
  sprints,
  members,
  currentUserId,
  pinnedTaskIds,
  onEdit,
  onDelete,
  onMove,
  onNote,
  onTogglePin,
}: {
  tasks: TaskItem[]
  categories: ProjectCategory[]
  sprints: Sprint[]
  members: WorkspaceMember[]
  currentUserId: string
  pinnedTaskIds: Set<string>
  onEdit: (task: TaskItem) => void
  onDelete: (task: TaskItem) => void
  onMove: (task: TaskItem, target: TaskStatus) => Promise<void>
  onNote: (task: TaskItem) => void
  onTogglePin: (taskId: string) => void
}) {
  const columns: TaskStatus[] = ['Backlog', 'Ready', 'InProgress', 'Blocked', 'Completed']
  const [activeTask, setActiveTask] = useState<TaskItem | null>(null)
  const [moving, setMoving] = useState(false)
  const activeTaskRef = useRef<TaskItem | null>(null)
  const lastOverRef = useRef<TaskStatus | null>(null)
  activeTaskRef.current = activeTask
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 8 } }),
    useSensor(KeyboardSensor),
  )
  const validTargets = activeTask
    ? new Set(columns.filter((status) => status !== activeTask.status))
    : new Set<string>()

  const boardCollisionDetection: CollisionDetection = (args) =>
    args.pointerCoordinates ? pointerWithin(args) : closestCenter(args)

  const attemptMove = async (task: TaskItem, target: TaskStatus) => {
    setMoving(true)
    try {
      await onMove(task, target)
    } finally {
      setMoving(false)
    }
  }

  const finishDrag = ({ active, over }: DragEndEvent) => {
    const task = active.data.current?.task as TaskItem | undefined
    const target = over?.id as TaskStatus | undefined
    setActiveTask(null)
    if (!task || !target) return
    void attemptMove(task, target)
  }

  // dnd-kit occasionally fails to fire onDragEnd/onDragCancel after a real
  // pointerup (a known upstream timing issue), leaving the drag stuck. This
  // watches for the raw browser release event and finishes the drag itself
  // using the last hover target if dnd-kit's own callback never arrives.
  useEffect(() => {
    if (!activeTask) return
    const task = activeTask
    const recover = () => {
      window.setTimeout(() => {
        if (activeTaskRef.current?.id !== task.id) return
        const target = lastOverRef.current
        setActiveTask(null)
        if (target) void attemptMove(task, target)
      }, 150)
    }
    window.addEventListener('mouseup', recover)
    window.addEventListener('touchend', recover)
    window.addEventListener('pointerup', recover)
    return () => {
      window.removeEventListener('mouseup', recover)
      window.removeEventListener('touchend', recover)
      window.removeEventListener('pointerup', recover)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTask])

  return <DndContext
    sensors={sensors}
    collisionDetection={boardCollisionDetection}
    onDragStart={({ active }: DragStartEvent) => {
      lastOverRef.current = null
      setActiveTask(active.data.current?.task as TaskItem)
    }}
    onDragCancel={() => setActiveTask(null)}
    onDragEnd={finishDrag}
    onDragMove={({ over }) => { lastOverRef.current = (over?.id as TaskStatus) ?? null }}
  >
    <div className={`board ${moving ? 'moving' : ''}`}>
      {columns.map((status) => <BoardColumn
        key={status}
        status={status}
        categories={categories}
        sprints={sprints}
        members={members}
        pinnedTaskIds={pinnedTaskIds}
        tasks={tasks.filter((task) => task.status === status)}
        currentUserId={currentUserId}
        onEdit={onEdit}
        onDelete={onDelete}
        onNote={onNote}
        onTogglePin={onTogglePin}
        dragActive={activeTask !== null}
        validTarget={validTargets.has(status)}
      />)}
    </div>
    <DragOverlay>
      {activeTask ? <BoardCard task={activeTask} categories={categories} sprints={sprints} members={members} currentUserId={currentUserId} pinned={pinnedTaskIds.has(activeTask.id)} onEdit={onEdit} onNote={onNote} onTogglePin={onTogglePin} overlay /> : null}
    </DragOverlay>
  </DndContext>
}

/**
 * One droppable column of the Kanban board for a single status. Orders its cards (Backlog
 * sorts by due date first, then pinned-first, then newest-first) and reflects drag state
 * (drag-active/valid-target/drag-over) via CSS classes driven by dnd-kit's `useDroppable`.
 */
function BoardColumn({
  status,
  tasks,
  categories,
  sprints,
  members,
  pinnedTaskIds,
  currentUserId,
  onEdit,
  onDelete,
  onNote,
  onTogglePin,
  dragActive,
  validTarget,
}: {
  status: TaskStatus
  tasks: TaskItem[]
  categories: ProjectCategory[]
  sprints: Sprint[]
  members: WorkspaceMember[]
  pinnedTaskIds: Set<string>
  currentUserId: string
  onEdit: (task: TaskItem) => void
  onDelete: (task: TaskItem) => void
  onNote: (task: TaskItem) => void
  onTogglePin: (taskId: string) => void
  dragActive: boolean
  validTarget: boolean
}) {
  const orderedTasks = [...tasks].sort((left, right) => {
    if (status === 'Backlog') {
      const dueDateOrder = compareBacklogDueDates(left, right)
      if (dueDateOrder !== 0) return dueDateOrder
    }

    const leftPinned = pinnedTaskIds.has(left.id) ? 0 : 1
    const rightPinned = pinnedTaskIds.has(right.id) ? 0 : 1
    const pinnedOrder = leftPinned - rightPinned
    if (pinnedOrder !== 0) return pinnedOrder

    return compareCreatedAtDesc(left, right)
  })
  const { isOver, setNodeRef } = useDroppable({
    id: status,
    disabled: dragActive && !validTarget,
  })

  return <section
    ref={setNodeRef}
    className={`board-column ${status.toLowerCase()} ${dragActive ? 'drag-active' : ''} ${validTarget ? 'valid-target' : ''} ${isOver ? 'drag-over' : ''}`}
    aria-label={`${statusLabels[status]} tasks`}
  >
    <header><span>{statusLabels[status]}</span><small>{tasks.length}</small></header>
    <div className="board-column-body">
      {orderedTasks.map((task) =>
        <BoardCard task={task} categories={categories} sprints={sprints} members={members} currentUserId={currentUserId} pinned={pinnedTaskIds.has(task.id)} onEdit={onEdit} onDelete={onDelete} onNote={onNote} onTogglePin={onTogglePin} key={task.id} />)}
      {!tasks.length && <span className="column-empty">No tasks</span>}
    </div>
  </section>
}

// Sorts Backlog cards by due date ascending, with tasks having no due date sorted last.
function compareBacklogDueDates(left: TaskItem, right: TaskItem) {
  return taskDueDateKey(left.dueDate) - taskDueDateKey(right.dueDate)
}

// Parses a due date string (ISO yyyy-mm-dd, d/m/yyyy, or any Date-parseable format) into a
// sortable timestamp; missing/unparseable dates sort to +Infinity (last).
function taskDueDateKey(dueDate: string | null) {
  if (!dueDate) return Number.POSITIVE_INFINITY

  const isoDate = dueDate.match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (isoDate) {
    const [, year, month, day] = isoDate
    return Date.UTC(Number(year), Number(month) - 1, Number(day))
  }

  const dayFirstDate = dueDate.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/)
  if (dayFirstDate) {
    const [, day, month, year] = dayFirstDate
    return Date.UTC(Number(year), Number(month) - 1, Number(day))
  }

  const parsed = Date.parse(dueDate)
  return Number.isNaN(parsed) ? Number.POSITIVE_INFINITY : parsed
}

// Sorts tasks newest-created-first, breaking ties alphabetically by title.
function compareCreatedAtDesc(left: TaskItem, right: TaskItem) {
  const createdOrder = right.createdAt.localeCompare(left.createdAt)
  if (createdOrder !== 0) return createdOrder

  return left.title.localeCompare(right.title)
}

/**
 * A single draggable task card on the Kanban board (or its `DragOverlay` clone when `overlay`
 * is true, which disables dragging on the clone itself).
 */
function BoardCard({
  task,
  categories,
  sprints = [],
  members = [],
  currentUserId,
  pinned = false,
  onEdit,
  onDelete,
  onNote,
  onTogglePin,
  overlay = false,
}: {
  task: TaskItem
  categories?: ProjectCategory[]
  sprints?: Sprint[]
  members?: WorkspaceMember[]
  currentUserId?: string
  pinned?: boolean
  onEdit: (task: TaskItem) => void
  onDelete?: (task: TaskItem) => void
  onNote: (task: TaskItem) => void
  onTogglePin: (taskId: string) => void
  overlay?: boolean
}) {
  const memberNames = new Map(members.map((member) => [member.userId, member.displayName]))
  const sprintNames = new Map(sprints.map((sprint) => [sprint.id, sprint.name]))
  const { attributes, isDragging, listeners, setNodeRef, transform } = useDraggable({
    id: task.id,
    data: { task },
    disabled: overlay,
  })
  const style = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` }
    : undefined
  const stopDragActivation = (event: { stopPropagation: () => void }) => event.stopPropagation()

  return <article
    ref={setNodeRef}
    style={style}
    className={`board-task ${task.status.toLowerCase()} ${pinned ? 'pinned' : ''} ${isDragging ? 'dragging' : ''} ${overlay ? 'overlay' : ''}`}
    title="Drag to move"
    {...attributes}
    {...listeners}
  >
    <div className="board-task-heading">
      <div className="board-task-toolbar">
        <button
          className={`pin-button ${pinned ? 'active' : ''}`}
          onClick={(event) => {
            event.stopPropagation()
            onTogglePin(task.id)
          }}
          onKeyDown={(event) => event.stopPropagation()}
          onPointerDown={stopDragActivation}
          onMouseDown={stopDragActivation}
          aria-label={pinned ? `Unpin ${task.title}` : `Pin ${task.title}`}
          title={pinned ? 'Unpin task' : 'Pin task'}
        ><Pin /></button>
        <button
          className="icon-button note-action"
          onClick={(event) => {
            event.stopPropagation()
            onNote(task)
          }}
          onKeyDown={(event) => event.stopPropagation()}
          onPointerDown={stopDragActivation}
          onMouseDown={stopDragActivation}
          aria-label={`Open notes for ${task.title}`}
          title="Task notes"
        ><MessageSquare /></button>
        <button
          className="icon-button"
          onClick={(event) => {
            event.stopPropagation()
            onEdit(task)
          }}
          onKeyDown={(event) => event.stopPropagation()}
          onPointerDown={stopDragActivation}
          onMouseDown={stopDragActivation}
          aria-label={`Edit ${task.title}`}
          title="Edit task"
        ><Pencil /></button>
        {!overlay && onDelete && task.createdByUserId === currentUserId && <button
          className="icon-button danger-action"
          onClick={(event) => {
            event.stopPropagation()
            onDelete(task)
          }}
          onKeyDown={(event) => event.stopPropagation()}
          onPointerDown={stopDragActivation}
          onMouseDown={stopDragActivation}
          aria-label={`Delete ${task.title}`}
          title="Delete task"
        ><Trash2 /></button>}
      </div>
      <strong>{task.title}</strong>
    </div>
    <div className="board-task-meta">
      <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>
        {task.deadlineHealth}
      </span>
      <b>{task.priorityScore?.toFixed(1) ?? '—'}</b>
    </div>
    <TaskMetadataLine
      task={task}
      categoryName={task.categoryId ? categories?.find((category) => category.id === task.categoryId)?.name : undefined}
      sprintName={task.sprintId ? sprintNames.get(task.sprintId) : undefined}
      assigneeName={task.assignedUserId ? memberNames.get(task.assignedUserId) : undefined}
      currentUserId={currentUserId}
    />
  </article>
}

const nextActions: Partial<Record<TaskStatus, { label: string; action: string }[]>> = {
  Backlog: [{ label: 'Move to ready', action: 'ready' }],
  Ready: [{ label: 'Start task', action: 'start' }],
  InProgress: [{ label: 'Complete task', action: 'complete' }],
  Blocked: [
    { label: 'Resume work', action: 'resume' },
    { label: 'Move to ready', action: 'unblock' },
  ],
  Completed: [{ label: 'Reopen task', action: 'reopen' }],
}

const actionTargets: Record<string, TaskStatus> = {
  ready: 'Ready',
  start: 'InProgress',
  block: 'Blocked',
  complete: 'Completed',
  unblock: 'Ready',
  resume: 'InProgress',
  reopen: 'Ready',
}

const effortOptions = [1, 2, 3, 5, 8]

/** Inline help text explaining the 1-5 priority input scale, with notes for unscored/read-only cases. */
function PriorityInputGuide({ readOnly = false, unscored = false }: { readOnly?: boolean; unscored?: boolean }) {
  return <p className="field-help">
    Use 1-5 scores: 1 is low impact or urgency, 3 is normal delivery value, and 5 is high business impact, urgent deadline pressure, or major risk reduction.
    {unscored ? ' This task is currently unscored; the values shown are starter defaults until you save them.' : ''}
    {readOnly ? ' Only the task creator can change these priority inputs.' : ''}
  </p>
}

/** Lightweight dialog for adding a note to a task and reviewing its existing notes, without opening the full task editor. */
function QuickNoteDialog({ task, members, currentUserId, onClose, onSaved }: { task: TaskItem; members: WorkspaceMember[]; currentUserId: string; onClose: () => void; onSaved: (taskId: string) => void }) {
  const [body, setBody] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const notes = task.notes ?? []
  const noteAuthor = (authorId: string) =>
    displayActor(authorId, members, currentUserId)
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!body.trim()) return
    setSaving(true)
    setError('')
    try {
      await api.addNote(task.id, body.trim())
      setBody('')
      onSaved(task.id)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Note could not be added.')
    } finally {
      setSaving(false)
    }
  }

  return <div className="dialog-backdrop" role="presentation">
    <dialog open aria-labelledby="quick-note-title" className="quick-note-dialog">
      <header>
        <div><p className="eyebrow">Task notes</p><h2 id="quick-note-title">{task.title}</h2></div>
        <button className="icon-button" onClick={onClose} aria-label="Close"><X /></button>
      </header>
      <form onSubmit={(event) => void submit(event)}>
        {error && <div className="error-state compact-error"><AlertTriangle /> <span>{error}</span></div>}
        <label>Add note<textarea value={body} onChange={(event) => setBody(event.target.value)} maxLength={4000} rows={4} placeholder="Write a short update, blocker, or handover note." autoFocus /></label>
        <footer className="editor-footer"><span /> <div><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving || !body.trim()}>{saving ? 'Adding...' : 'Add note'}</button></div></footer>
      </form>
      <section className="quick-note-list" aria-label="Existing notes">
        {notes.length
          ? notes.map((note) => <article key={note.id}><MessageSquare size={16} /><div><p>{note.body}</p><small className="note-meta"><span className="note-author">{noteAuthor(note.authorId)}</span><span>{new Date(note.createdAt).toLocaleString()}</span></small></div></article>)
          : <div className="empty compact"><MessageSquare /><h2>No notes yet</h2><p>Add the first note for this task.</p></div>}
      </section>
    </dialog>
  </div>
}

/**
 * Full task-edit dialog: title/due date/effort/sprint/assignee, category and tags,
 * notes, priority planning inputs, and available status transition buttons. Plain
 * Members get a read-only view of most fields (`isMember`); planning inputs are only
 * editable by the task creator (`canEditPlanning`).
 */
function TaskEditor({ projectId, task, currentUserId, workspaceRole, isMember, members, categories, sprints, onCategoryCreated, onClose, onSaved }: { projectId: string; task: TaskItem; currentUserId: string; workspaceRole: Workspace['role'] | null; isMember: boolean; members: WorkspaceMember[]; categories: ProjectCategory[]; sprints: Sprint[]; onCategoryCreated: (category: ProjectCategory) => void; onClose: () => void; onSaved: () => void }) {
  const [saving, setSaving] = useState(false)
  const [categoryDraft, setCategoryDraft] = useState('')
  const [tagDraft, setTagDraft] = useState('')
  const [noteDraft, setNoteDraft] = useState('')
  const [error, setError] = useState('')
  const explanation = task.priorityExplanation
  const canEditPlanning = !isMember && (!task.createdByUserId || task.createdByUserId === currentUserId)
  const isUnscored = !explanation
  const availableActions = (nextActions[task.status] ?? [])
    .filter(({ action }) => canMoveTask(task, actionTargets[action], currentUserId, workspaceRole))
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      const data = new FormData(event.currentTarget)
      const title = isMember ? task.title : String(data.get('title'))
      const dueDate = isMember ? task.dueDate ?? '' : String(data.get('dueDate'))
      const effort = canEditPlanning ? Number(data.get('effort')) : explanation?.effort ?? task.effort ?? 3
      const sprintId = isMember ? task.sprintId ?? '' : String(data.get('sprintId') ?? '')
      await api.updateTask(task.id, title, dueDate, effort, sprintId)
      if (canEditPlanning) {
        await api.updatePlanning(
          task.id,
          Number(data.get('businessValue')),
          Number(data.get('urgency')),
          Number(data.get('riskReduction')),
          effort,
        )
      }
      const assignee = String(data.get('assignedUserId') ?? '')
      if (assignee) await api.assign(task.id, assignee)
      else if (task.assignedUserId) await api.unassign(task.id)
      if (!isMember) {
        const categoryId = String(data.get('categoryId') ?? '')
        await api.updateCategory(task.id, categoryId || null)
      }
      if (!isMember && tagDraft.trim()) await api.addTag(task.id, tagDraft.trim())
      if (noteDraft.trim()) await api.addNote(task.id, noteDraft.trim())
      onSaved()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Task could not be saved.')
    } finally {
      setSaving(false)
    }
  }
  const createCategory = async () => {
    if (!categoryDraft.trim()) return
    setSaving(true)
    const category = await api.createCategory(projectId, categoryDraft.trim())
    onCategoryCreated(category)
    setCategoryDraft('')
    setSaving(false)
  }
  const removeTag = async (tag: string) => {
    setSaving(true)
    await api.removeTag(task.id, tag)
    onSaved()
  }
  const transition = async (action: string) => {
    setSaving(true)
    setError('')
    try {
      await api.transition(task.id, action)
      onSaved()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Task status could not be changed.')
      setSaving(false)
    }
  }
  return <div className="dialog-backdrop" role="presentation"><dialog open aria-labelledby="edit-dialog-title"><header><div><p className="eyebrow">{statusLabels[task.status]}</p><h2 id="edit-dialog-title">Edit task</h2></div><button className="icon-button" onClick={onClose} aria-label="Close"><X /></button></header>
    <form onSubmit={(event) => void submit(event)}>
      {error && <div className="error-state compact-error"><AlertTriangle /> <span>{error}</span></div>}
      <label>Task title<input name="title" required maxLength={240} defaultValue={task.title} readOnly={isMember} autoFocus={!isMember} /></label>
      <div className="form-grid"><label>Due date<input name="dueDate" type="date" defaultValue={task.dueDate ?? ''} readOnly={isMember} /></label><label>Effort<select name="effort" defaultValue={String(explanation?.effort ?? task.effort ?? 3)} disabled={!canEditPlanning}>{effortOptions.map((value) => <option key={value}>{value}</option>)}</select><ChevronDown /></label></div>
      <label>Sprint<select name="sprintId" defaultValue={task.sprintId ?? ''} disabled={isMember}><option value="">No sprint</option>{sprints.filter((sprint) => sprint.status !== 'Cancelled').map((sprint) => <option key={sprint.id} value={sprint.id}>{sprint.name} - {sprint.status}</option>)}</select><ChevronDown /></label>
      <label className="assignee-field">Assignee<select name="assignedUserId" defaultValue={task.assignedUserId ?? ''}><option value="">Unassigned</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.displayName} · {member.role}</option>)}</select><ChevronDown /></label>
      <fieldset><legend>Metadata</legend><div className="metadata-editor">
        <label>Category<select name="categoryId" defaultValue={task.categoryId ?? ''} disabled={isMember}><option value="">No category</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select><ChevronDown /></label>
        {!isMember && <div className="inline-create"><label>New category<input value={categoryDraft} onChange={(event) => setCategoryDraft(event.target.value)} maxLength={80} /></label><button type="button" className="secondary" disabled={saving || !categoryDraft.trim()} onClick={() => void createCategory()}><FolderPlus size={16} /> Add</button></div>}
        <label>Add tag<input value={tagDraft} onChange={(event) => setTagDraft(event.target.value)} disabled={isMember} maxLength={40} placeholder="client, urgent, research" /></label>
        {!!task.tags.length && <div className="chip-list" aria-label="Task tags">{task.tags.map((tag) => <button type="button" className="tag-chip removable" key={tag} disabled={saving || isMember} onClick={() => void removeTag(tag)}><Tags size={12} /> {tag} <X size={12} /></button>)}</div>}
        <label className="note-field">Add note<textarea value={noteDraft} onChange={(event) => setNoteDraft(event.target.value)} maxLength={4000} rows={3} /></label>
        {!!task.notes?.length && <div className="note-list" aria-label="Task notes">{task.notes.map((note) => <article key={note.id}><MessageSquare size={15} /><p>{note.body}</p><small>{new Date(note.createdAt).toLocaleString()}</small></article>)}</div>}
      </div></fieldset>
      <fieldset><legend>Priority inputs</legend><PriorityInputGuide readOnly={!canEditPlanning} unscored={isUnscored} /><div className="planning-grid">
        <label>Business value<input name="businessValue" type="number" min="1" max="5" defaultValue={explanation ? explanation.businessValueContribution / 3 : 3} readOnly={!canEditPlanning} /></label>
        <label>Urgency<input name="urgency" type="number" min="1" max="5" defaultValue={explanation ? explanation.urgencyContribution / 2 : 3} readOnly={!canEditPlanning} /></label>
        <label>Risk reduction<input name="riskReduction" type="number" min="1" max="5" defaultValue={explanation ? explanation.riskReductionContribution / 2 : 3} readOnly={!canEditPlanning} /></label>
      </div></fieldset>
      <footer className="editor-footer"><div>{availableActions.map(({ label, action }) => <button type="button" className="secondary" key={action} disabled={saving} onClick={() => void transition(action)}>{label}</button>)}</div><div><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving ? 'Saving...' : 'Save changes'}</button></div></footer>
    </form>
  </dialog></div>
}

/**
 * Task creation dialog: title/due date/effort/assignee/sprint plus initial priority
 * planning inputs, with optional initial tag/note/category set in the same submit and an
 * inline "create category" shortcut. `initialDueDate` lets callers (e.g. the calendar
 * composer) pre-fill the due date.
 */
function TaskDialog({ projectId, isMember, members, categories, sprints, initialDueDate, onCategoryCreated, onClose, onCreated }: { projectId: string; isMember: boolean; members: WorkspaceMember[]; categories: ProjectCategory[]; sprints: Sprint[]; initialDueDate?: string; onCategoryCreated: (category: ProjectCategory) => void; onClose: () => void; onCreated: () => void }) {
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [categoryDraft, setCategoryDraft] = useState('')
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      const data = new FormData(event.currentTarget)
      const assignee = String(data.get('assignedUserId') ?? '')
      const categoryId = String(data.get('categoryId') ?? '')
      const sprintId = String(data.get('sprintId') ?? '')
      const tag = String(data.get('tag') ?? '').trim()
      const note = String(data.get('note') ?? '').trim()
      if (tag && tag.replace(/^#/, '').trim().length < 2) {
        throw new Error('Tag names must be between 2 and 40 characters.')
      }
      const effort = Number(data.get('effort'))
      const task = await api.createTask(
        projectId,
        String(data.get('title')),
        String(data.get('dueDate')),
        effort,
        Number(data.get('businessValue')),
        Number(data.get('urgency')),
        Number(data.get('riskReduction')),
        sprintId,
      )
      if (assignee) await api.assign(task.id, assignee)
      if (categoryId) await api.updateCategory(task.id, categoryId)
      if (tag) await api.addTag(task.id, tag)
      if (note) await api.addNote(task.id, note)
      onCreated()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Task could not be created.')
    } finally {
      setSaving(false)
    }
  }
  const createCategory = async () => {
    if (!categoryDraft.trim()) return
    setSaving(true)
    setError('')
    try {
      const category = await api.createCategory(projectId, categoryDraft.trim())
      onCategoryCreated(category)
      setCategoryDraft('')
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Category could not be created.')
    } finally {
      setSaving(false)
    }
  }
  return <div className="dialog-backdrop" role="presentation"><dialog open aria-labelledby="task-dialog-title"><header><div><p className="eyebrow">Portfolio launch</p><h2 id="task-dialog-title">Create task</h2></div><button className="icon-button" onClick={onClose} aria-label="Close"><X /></button></header>
    <form onSubmit={(event) => void submit(event)}>
      {error && <div className="error-state compact-error"><AlertTriangle /> <span>{error}</span></div>}
      <label>Task title<input name="title" required maxLength={240} autoFocus /></label>
      <div className="form-grid">
        <label>Due date<input name="dueDate" type="date" defaultValue={initialDueDate ?? ''} /></label>
        <label>Effort<select name="effort" defaultValue="3">{effortOptions.map((value) => <option key={value}>{value}</option>)}</select><ChevronDown /></label>
      </div>
      <label className="assignee-field">Assignee<select name="assignedUserId" defaultValue=""><option value="">Unassigned</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.displayName} - {member.role}</option>)}</select><ChevronDown /></label>
      <label>Sprint<select name="sprintId" defaultValue=""><option value="">No sprint</option>{sprints.filter((sprint) => sprint.status !== 'Cancelled').map((sprint) => <option key={sprint.id} value={sprint.id}>{sprint.name} - {sprint.status}</option>)}</select><ChevronDown /></label>
      <fieldset><legend>Priority inputs</legend><PriorityInputGuide /><div className="planning-grid">
        <label>Business value<input name="businessValue" type="number" min="1" max="5" defaultValue="3" /></label>
        <label>Urgency<input name="urgency" type="number" min="1" max="5" defaultValue="3" /></label>
        <label>Risk reduction<input name="riskReduction" type="number" min="1" max="5" defaultValue="3" /></label>
      </div></fieldset>
      <fieldset><legend>Metadata</legend><div className="metadata-editor">
        <label>Category<select name="categoryId" defaultValue=""><option value="">No category</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select><ChevronDown /></label>
        {!isMember && <div className="inline-create"><label>New category<input value={categoryDraft} onChange={(event) => setCategoryDraft(event.target.value)} maxLength={80} /></label><button type="button" className="secondary" disabled={saving || !categoryDraft.trim()} onClick={() => void createCategory()}><FolderPlus size={16} /> Add</button></div>}
        <label>Tag<input name="tag" maxLength={40} /></label>
        <label className="note-field">Note<textarea name="note" maxLength={4000} rows={3} /></label>
      </div></fieldset>
      <footer><button type="button" className="secondary" disabled={saving} onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving ? 'Creating...' : 'Create task'}</button></footer>
    </form>
  </dialog></div>
}
