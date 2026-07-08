import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import {
  DndContext, DragOverlay, KeyboardSensor, PointerSensor,
  useDraggable, useDroppable, useSensor, useSensors,
} from '@dnd-kit/core'
import type { DragEndEvent, DragStartEvent } from '@dnd-kit/core'
import {
  Activity, AlertTriangle, CheckCircle2, ChevronDown, CircleGauge,
  Clock3, Columns3, FolderPlus, GripVertical, KeyRound, LayoutList, LogOut,
  Menu, MessageSquare, Pencil, Plus, Save, Search, Settings2, ShieldCheck,
  Tags, UserRound, X,
} from 'lucide-react'
import { api } from './api'
import type {
  AccountSession, Dashboard, ProjectCategory, ProjectDetails, TaskActivity, TaskItem,
  TaskStatus, Workspace, WorkspaceMember,
} from './api'
import './styles.css'

const statusLabels: Record<TaskStatus, string> = {
  Backlog: 'Backlog', Ready: 'Ready', InProgress: 'In progress',
  Blocked: 'Blocked', Completed: 'Completed',
}

const emptyDashboard: Dashboard = {
  projectCount: 0, activeTaskCount: 0, blockedTaskCount: 0,
  overdueTaskCount: 0, criticalTaskCount: 0,
}

type View = 'workspace' | 'activity' | 'settings' | 'profile'

const views: View[] = ['workspace', 'activity', 'settings', 'profile']

function viewFromHash(hash: string): View {
  const value = hash.replace('#', '').toLowerCase()
  return views.includes(value as View) ? value as View : 'workspace'
}

interface UserProfile {
  displayName: string
  email: string
  title: string
}

interface UserSettings {
  defaultView: 'list' | 'board'
  compactMode: boolean
  emailDigest: boolean
}

const defaultProfile: UserProfile = {
  displayName: 'Jadesola Aliu',
  email: 'jadesola@example.com',
  title: 'Portfolio owner',
}

const defaultSettings: UserSettings = {
  defaultView: 'list',
  compactMode: false,
  emailDigest: true,
}

const defaultAccount: AccountSession | null = null

function readLocal<T>(key: string, fallback: T): T {
  try {
    const value = localStorage.getItem(key)
    return value ? { ...fallback, ...JSON.parse(value) } : fallback
  } catch {
    return fallback
  }
}

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
  Blocked: { Ready: { action: 'unblock' } },
  Completed: { Ready: { action: 'reopen' } },
}

export default function App() {
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [taskTotal, setTaskTotal] = useState(0)
  const [dashboard, setDashboard] = useState(emptyDashboard)
  const [workspace, setWorkspace] = useState<Workspace | null>(null)
  const [project, setProject] = useState<ProjectDetails | null>(null)
  const [members, setMembers] = useState<WorkspaceMember[]>([])
  const [categories, setCategories] = useState<ProjectCategory[]>([])
  const [mode, setMode] = useState<'list' | 'board'>('list')
  const [view, setView] = useState<View>(() => viewFromHash(window.location.hash))
  const [search, setSearch] = useState('')
  const [pageNumber, setPageNumber] = useState(1)
  const pageSize = 10
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [selectedTask, setSelectedTask] = useState<TaskItem | null>(null)
  const [navOpen, setNavOpen] = useState(false)
  const [activity, setActivity] = useState<TaskActivity[]>([])
  const [profile, setProfile] = useState<UserProfile>(() =>
    readLocal('todoapp_profile', defaultProfile))
  const [settings, setSettings] = useState<UserSettings>(() =>
    readLocal('todoapp_settings', defaultSettings))
  const [account, setAccount] = useState<AccountSession | null>(() =>
    readLocal('todoapp_account', defaultAccount))
  const [loggedOut, setLoggedOut] = useState(() =>
    localStorage.getItem('todoapp_logged_out') === 'true')
  const [notice, setNotice] = useState('')

  const load = async () => {
    try {
      setLoading(true)
      setError('')
      const available = await api.workspaces()
      const selected = available[0]
      if (!selected) throw new Error('No workspace membership was found.')
      const projects = await api.projects(selected.id)
      const selectedProject = projects[0] ??
        await api.createWorkspaceProject(selected.id, 'My task project')
      const [summary, page, workspaceMembers] = await Promise.all([
        api.dashboard(), api.tasks(selectedProject.id, search, pageNumber, pageSize), api.members(selected.id),
      ])
      setWorkspace(selected)
      setProject(selectedProject)
      setMembers(workspaceMembers)
      setCategories(selectedProject.categories)
      setDashboard(summary)
      setTasks(page.items)
      setTaskTotal(page.totalCount)
      const activityItems = await Promise.all(
        page.items.slice(0, 10).map((task) => api.activity(task.id).catch(() => [])),
      )
      setActivity(activityItems.flat().sort((left, right) =>
        Date.parse(right.occurredAt) - Date.parse(left.occurredAt)))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load workspace.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [pageNumber, search])
  useEffect(() => {
    const syncViewFromHash = () => setView(viewFromHash(window.location.hash))
    window.addEventListener('hashchange', syncViewFromHash)
    syncViewFromHash()
    return () => window.removeEventListener('hashchange', syncViewFromHash)
  }, [])
  useEffect(() => { setMode(settings.defaultView) }, [settings.defaultView])
  const visible = useMemo(() => tasks, [tasks])
  const totalPages = Math.max(1, Math.ceil(taskTotal / pageSize))
  const logout = () => {
    localStorage.removeItem('todoapp_access_token')
    localStorage.removeItem('todoapp_account')
    localStorage.setItem('todoapp_logged_out', 'true')
    setAccount(null)
    setLoggedOut(true)
    setNotice('You have been logged out of the browser session.')
    window.location.hash = 'workspace'
    setNavOpen(false)
  }
  const authenticated = (session: AccountSession) => {
    localStorage.setItem('todoapp_access_token', session.accessToken)
    localStorage.setItem('todoapp_account', JSON.stringify(session))
    localStorage.removeItem('todoapp_logged_out')
    setAccount(session)
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
    void load()
  }
  const openView = (next: View) => {
    setView(next)
    window.location.hash = next
    setNavOpen(false)
  }
  const moveTask = async (task: TaskItem, target: TaskStatus) => {
    const transition = boardTransitions[task.status]?.[target]
    if (!transition) return

    try {
      setError('')
      await api.transition(task.id, transition.action, transition.body)
      await load()
    } catch (reason) {
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

  if (loggedOut || (!import.meta.env.DEV && !localStorage.getItem('todoapp_access_token'))) {
    return <AuthPage onAuthenticated={authenticated} />
  }

  return (
    <div className="app-shell">
      <aside className={navOpen ? 'sidebar open' : 'sidebar'}>
        <div className="brand"><span className="brand-mark">T</span><strong>Todo Intelligence</strong></div>
        <nav aria-label="Primary navigation">
          <button className={view === 'workspace' ? 'active' : ''} onClick={() => openView('workspace')}><CircleGauge size={18} /> Workspace</button>
          <button className={view === 'activity' ? 'active' : ''} onClick={() => openView('activity')}><Activity size={18} /> Activity</button>
          <button className={view === 'settings' ? 'active' : ''} onClick={() => openView('settings')}><Settings2 size={18} /> Settings</button>
          <button className={view === 'profile' ? 'active' : ''} onClick={() => openView('profile')}><UserRound size={18} /> Profile</button>
        </nav>
        <button className="sidebar-logout" onClick={logout}><LogOut size={18} /> Logout</button>
        <button className="sidebar-foot" onClick={() => openView('profile')}>
          <span className="avatar">{initials(profile.displayName)}</span>
          <div><strong>{profile.displayName}</strong><small>{profile.title}</small></div>
        </button>
      </aside>

      <main id="workspace">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setNavOpen(!navOpen)} aria-label="Toggle navigation"><Menu /></button>
          <div><p className="eyebrow">{workspace?.name ?? 'Workspace'}</p><h1>{viewTitle(view)}</h1></div>
          {view === 'workspace' && <button className="primary" onClick={() => setDialogOpen(true)}><Plus size={17} /> New task</button>}
        </header>

        {notice && <div className="success-state"><ShieldCheck /> <span>{notice}</span><button onClick={() => setNotice('')}>Dismiss</button></div>}
        {error && <div className="error-state"><AlertTriangle /> <span>{error}</span><button onClick={() => void load()}>Retry</button></div>}

        {view === 'workspace' && <>
          <section className="metrics" aria-label="Portfolio health">
            <Metric label="Active work" value={dashboard.activeTaskCount} icon={<Clock3 />} />
            <Metric label="Critical" value={dashboard.criticalTaskCount} icon={<AlertTriangle />} tone="danger" />
            <Metric label="Blocked" value={dashboard.blockedTaskCount} icon={<Columns3 />} tone="warn" />
            <Metric label="Overdue" value={dashboard.overdueTaskCount} icon={<CheckCircle2 />} tone="danger" />
          </section>

          <section className="work-area">
            <div className="toolbar">
              <div className="search"><Search size={17} /><input value={search} onChange={(e) => { setSearch(e.target.value); setPageNumber(1) }} placeholder="Search tasks" aria-label="Search tasks" /></div>
              <div className="segmented" aria-label="View">
                <button className={mode === 'list' ? 'selected' : ''} onClick={() => setMode('list')}><LayoutList size={16} /> List</button>
                <button className={mode === 'board' ? 'selected' : ''} onClick={() => setMode('board')}><Columns3 size={16} /> Board</button>
              </div>
            </div>

            {loading ? <div className="loading">Loading workspace...</div> :
              mode === 'list'
                ? <TaskList tasks={visible} categories={categories} onEdit={(task) => void openTaskEditor(task)} />
                : <Board tasks={visible} categories={categories} onEdit={(task) => void openTaskEditor(task)} onMove={moveTask} />}
            {!loading && <Pagination
              pageNumber={pageNumber}
              pageSize={pageSize}
              totalCount={taskTotal}
              totalPages={totalPages}
              onPageChange={setPageNumber}
            />}
          </section>
        </>}
        {view === 'activity' && <ActivityPage activity={activity} tasks={tasks} loading={loading} />}
        {view === 'settings' && <SettingsPage settings={settings} onSave={(next) => {
          setSettings(next)
          localStorage.setItem('todoapp_settings', JSON.stringify(next))
          setNotice('Settings saved.')
        }} />}
        {view === 'profile' && <ProfilePage profile={profile} account={account} onSave={(next) => {
          setProfile(next)
          localStorage.setItem('todoapp_profile', JSON.stringify(next))
          setNotice('Profile updated.')
        }} onPasswordChanged={() => setNotice('Password preference recorded. Connect a production identity provider to enforce password changes.')} onLogout={logout} />}
      </main>
      {dialogOpen && project && <TaskDialog projectId={project.id} categories={categories} onCategoryCreated={(category) => setCategories((items) => [...items, category].sort((left, right) => left.name.localeCompare(right.name)))} onClose={() => setDialogOpen(false)} onCreated={() => { setDialogOpen(false); void load() }} />}
      {selectedTask && project && <TaskEditor projectId={project.id} task={selectedTask} members={members} categories={categories} onCategoryCreated={(category) => setCategories((items) => [...items, category].sort((left, right) => left.name.localeCompare(right.name)))} onClose={() => setSelectedTask(null)} onSaved={() => { setSelectedTask(null); void load() }} />}
    </div>
  )
}

function viewTitle(view: View) {
  return {
    workspace: 'Delivery workspace',
    activity: 'Activity timeline',
    settings: 'Workspace settings',
    profile: 'Profile',
  }[view]
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('') || 'U'
}

function AuthPage({ onAuthenticated }: { onAuthenticated: (session: AccountSession) => void }) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    const data = new FormData(event.currentTarget)
    try {
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
      <div className="brand"><span className="brand-mark">T</span><strong>Todo Intelligence</strong></div>
      <div>
        <p className="eyebrow">Account access</p>
        <h1>{mode === 'login' ? 'Sign in' : 'Create account'}</h1>
      </div>
      <div className="segmented" aria-label="Account mode">
        <button className={mode === 'login' ? 'selected' : ''} onClick={() => setMode('login')}><KeyRound size={16} /> Login</button>
        <button className={mode === 'register' ? 'selected' : ''} onClick={() => setMode('register')}><UserRound size={16} /> Register</button>
      </div>
      <form className="auth-form" onSubmit={(event) => void submit(event)}>
        {mode === 'register' && <>
          <label>Display name<input name="displayName" required maxLength={120} autoComplete="name" /></label>
          <label>Workspace name<input name="workspaceName" required maxLength={160} defaultValue="Portfolio workspace" /></label>
        </>}
        <label>Email<input name="email" type="email" required autoComplete="email" /></label>
        <label>Password<input name="password" type="password" required minLength={8} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} /></label>
        {error && <p className="field-error">{error}</p>}
        <button className="primary" disabled={busy}>{busy ? 'Working...' : mode === 'login' ? 'Login' : 'Register'}</button>
      </form>
    </section>
  </main>
}

function Metric({ label, value, icon, tone = '' }: { label: string; value: number; icon: ReactNode; tone?: string }) {
  return <div className={`metric ${tone}`}><span className="metric-icon">{icon}</span><div><strong>{value}</strong><span>{label}</span></div></div>
}

function ActivityPage({
  activity,
  tasks,
  loading,
}: {
  activity: TaskActivity[]
  tasks: TaskItem[]
  loading: boolean
}) {
  const taskTitles = new Map(tasks.map((task) => [task.id, task.title]))
  if (loading) return <section className="work-area"><div className="loading">Loading activity...</div></section>
  if (!activity.length) {
    return <section className="panel-page activity-page">
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

  return <section className="panel-page activity-page" aria-label="Activity timeline">
    {activity.slice(0, 20).map((item) => <article className="activity-item" key={item.sequence}>
      <span className="activity-icon"><Activity /></span>
      <div>
        <strong>{taskTitles.get(item.taskId) ?? 'Task activity'}</strong>
        <p>{item.actor} changed {activityLabel(item)} from {item.previousValue ?? 'none'} to {item.currentValue ?? 'none'}.</p>
        <small>{new Date(item.occurredAt).toLocaleString()}</small>
      </div>
    </article>)}
  </section>
}

function activityLabel(item: TaskActivity) {
  return (item.action ?? item.activityType ?? 'Activity')
    .replace(/([A-Z])/g, ' $1')
    .trim()
    .toLowerCase()
}

function Pagination({
  pageNumber,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
}: {
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  onPageChange: (page: number) => void
}) {
  if (totalCount <= pageSize) {
    return <footer className="pagination"><span>{totalCount} task{totalCount === 1 ? '' : 's'}</span></footer>
  }

  const start = (pageNumber - 1) * pageSize + 1
  const end = Math.min(pageNumber * pageSize, totalCount)

  return <footer className="pagination" aria-label="Task pagination">
    <span>Showing {start}-{end} of {totalCount}</span>
    <div>
      <button className="secondary" disabled={pageNumber === 1} onClick={() => onPageChange(pageNumber - 1)}>Previous</button>
      <strong>Page {pageNumber} of {totalPages}</strong>
      <button className="secondary" disabled={pageNumber === totalPages} onClick={() => onPageChange(pageNumber + 1)}>Next</button>
    </div>
  </footer>
}

function SettingsPage({
  settings,
  onSave,
}: {
  settings: UserSettings
  onSave: (settings: UserSettings) => void
}) {
  const [draft, setDraft] = useState(settings)
  useEffect(() => setDraft(settings), [settings])

  return <section className="panel-page settings-page">
    <form className="settings-form" onSubmit={(event) => {
      event.preventDefault()
      onSave(draft)
    }}>
      <div className="settings-section">
        <div>
          <h2>Workspace preferences</h2>
          <p>Choose the default view and notification behaviour for this browser session.</p>
        </div>
        <label>Default view<select value={draft.defaultView} onChange={(event) => setDraft({ ...draft, defaultView: event.target.value as 'list' | 'board' })}><option value="list">List</option><option value="board">Board</option></select><ChevronDown /></label>
      </div>
      <label className="toggle-row"><input type="checkbox" checked={draft.compactMode} onChange={(event) => setDraft({ ...draft, compactMode: event.target.checked })} /><span><strong>Compact task rows</strong><small>Prepare the UI for dense operational dashboards.</small></span></label>
      <label className="toggle-row"><input type="checkbox" checked={draft.emailDigest} onChange={(event) => setDraft({ ...draft, emailDigest: event.target.checked })} /><span><strong>Email digest</strong><small>Keep the preference ready for a production notification service.</small></span></label>
      <footer><button className="primary"><Save size={16} /> Save settings</button></footer>
    </form>
  </section>
}

function ProfilePage({
  profile,
  account,
  onSave,
  onPasswordChanged,
  onLogout,
}: {
  profile: UserProfile
  account: AccountSession | null
  onSave: (profile: UserProfile) => void
  onPasswordChanged: () => void
  onLogout: () => void
}) {
  const [draft, setDraft] = useState(profile)
  const [password, setPassword] = useState({ current: '', next: '', confirm: '' })
  const [passwordError, setPasswordError] = useState('')
  useEffect(() => setDraft(profile), [profile])

  return <section className="profile-grid">
    <form className="panel-page profile-card" onSubmit={(event) => {
      event.preventDefault()
      onSave(draft)
    }}>
      <div className="profile-heading"><span className="avatar large">{initials(draft.displayName)}</span><div><h2>Personal details</h2><p>{account ? `Signed in as ${account.email}.` : 'Using the development workspace session.'}</p></div></div>
      <label>Display name<input value={draft.displayName} onChange={(event) => setDraft({ ...draft, displayName: event.target.value })} required maxLength={120} /></label>
      <label>Email<input type="email" value={draft.email} onChange={(event) => setDraft({ ...draft, email: event.target.value })} required maxLength={180} /></label>
      <label>Title<input value={draft.title} onChange={(event) => setDraft({ ...draft, title: event.target.value })} required maxLength={120} /></label>
      <footer><button className="primary"><Save size={16} /> Save profile</button></footer>
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
      setPassword({ current: '', next: '', confirm: '' })
      onPasswordChanged()
    }}>
      <div className="profile-heading"><span className="metric-icon"><ShieldCheck /></span><div><h2>Password</h2><p>Record the change intent until production identity is connected.</p></div></div>
      <label>Current password<input type="password" value={password.current} onChange={(event) => setPassword({ ...password, current: event.target.value })} autoComplete="current-password" /></label>
      <label>New password<input type="password" value={password.next} onChange={(event) => setPassword({ ...password, next: event.target.value })} autoComplete="new-password" /></label>
      <label>Confirm password<input type="password" value={password.confirm} onChange={(event) => setPassword({ ...password, confirm: event.target.value })} autoComplete="new-password" /></label>
      {passwordError && <p className="field-error">{passwordError}</p>}
      <footer><button className="secondary" type="button" onClick={onLogout}><LogOut size={16} /> Logout</button><button className="primary">Change password</button></footer>
    </form>
  </section>
}

function TaskList({ tasks, categories, onEdit }: { tasks: TaskItem[]; categories: ProjectCategory[]; onEdit: (task: TaskItem) => void }) {
  const categoryNames = new Map(categories.map((category) => [category.id, category.name]))
  if (!tasks.length) return <div className="empty"><Search /><h2>No matching work</h2><p>Try a different search term.</p></div>
  return <div className="task-table"><div className="table-head"><span>Task</span><span>Status</span><span>Deadline</span><span>Priority</span><span /></div>
    {tasks.map((task) => <article className="task-row" key={task.id}>
      <div className="task-name"><span className={`priority-line ${task.priorityBand?.toLowerCase()}`} /><div><strong>{task.title}</strong><small>{task.priorityExplanation ? `Value ${task.priorityExplanation.businessValueContribution} · Urgency ${task.priorityExplanation.urgencyContribution} · Risk ${task.priorityExplanation.riskReductionContribution}` : 'Planning factors not set'}</small></div></div>
      <TaskMetadataLine task={task} categoryName={task.categoryId ? categoryNames.get(task.categoryId) : undefined} />
      <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
      <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ?? 'Not scheduled'}</span>
      <span className="score"><strong>{task.priorityScore?.toFixed(1) ?? '—'}</strong><small>{task.priorityBand ?? 'Unscored'}</small></span>
      <button className="icon-button" onClick={() => onEdit(task)} aria-label={`Edit ${task.title}`} title="Edit task"><Pencil /></button>
    </article>)}
  </div>
}

function TaskMetadataLine({ task, categoryName }: { task: TaskItem; categoryName?: string }) {
  if (!categoryName && !task.tags.length) return null
  return <span className="metadata-line">
    {categoryName && <span className="category-pill"><FolderPlus size={12} /> {categoryName}</span>}
    {task.tags.slice(0, 3).map((tag) => <span className="tag-chip" key={tag}><Tags size={12} /> {tag}</span>)}
  </span>
}

function Board({
  tasks,
  categories,
  onEdit,
  onMove,
}: {
  tasks: TaskItem[]
  categories: ProjectCategory[]
  onEdit: (task: TaskItem) => void
  onMove: (task: TaskItem, target: TaskStatus) => Promise<void>
}) {
  const columns: TaskStatus[] = ['Backlog', 'Ready', 'InProgress', 'Blocked', 'Completed']
  const [activeTask, setActiveTask] = useState<TaskItem | null>(null)
  const [moving, setMoving] = useState(false)
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(KeyboardSensor),
  )
  const validTargets = activeTask
    ? new Set(Object.keys(boardTransitions[activeTask.status] ?? {}))
    : new Set<string>()

  const finishDrag = async ({ active, over }: DragEndEvent) => {
    const task = active.data.current?.task as TaskItem | undefined
    const target = over?.id as TaskStatus | undefined
    setActiveTask(null)
    if (!task || !target || !boardTransitions[task.status]?.[target]) return

    setMoving(true)
    try {
      await onMove(task, target)
    } finally {
      setMoving(false)
    }
  }

  return <DndContext
    sensors={sensors}
    onDragStart={({ active }: DragStartEvent) =>
      setActiveTask(active.data.current?.task as TaskItem)}
    onDragCancel={() => setActiveTask(null)}
    onDragEnd={(event) => void finishDrag(event)}
  >
    <div className={`board ${moving ? 'moving' : ''}`}>
      {columns.map((status) => <BoardColumn
        key={status}
        status={status}
        categories={categories}
        tasks={tasks.filter((task) => task.status === status)}
        onEdit={onEdit}
        dragActive={activeTask !== null}
        validTarget={validTargets.has(status)}
      />)}
    </div>
    <DragOverlay>
      {activeTask ? <BoardCard task={activeTask} categories={categories} onEdit={onEdit} overlay /> : null}
    </DragOverlay>
  </DndContext>
}

function BoardColumn({
  status,
  tasks,
  categories,
  onEdit,
  dragActive,
  validTarget,
}: {
  status: TaskStatus
  tasks: TaskItem[]
  categories: ProjectCategory[]
  onEdit: (task: TaskItem) => void
  dragActive: boolean
  validTarget: boolean
}) {
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
      {tasks.map((task) =>
        <BoardCard task={task} categories={categories} onEdit={onEdit} key={task.id} />)}
      {!tasks.length && <span className="column-empty">No tasks</span>}
    </div>
  </section>
}

function BoardCard({
  task,
  categories,
  onEdit,
  overlay = false,
}: {
  task: TaskItem
  categories?: ProjectCategory[]
  onEdit: (task: TaskItem) => void
  overlay?: boolean
}) {
  const { attributes, isDragging, listeners, setNodeRef, transform } = useDraggable({
    id: task.id,
    data: { task },
    disabled: overlay,
  })
  const style = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` }
    : undefined

  return <article
    ref={setNodeRef}
    style={style}
    className={`board-task ${task.status.toLowerCase()} ${isDragging ? 'dragging' : ''} ${overlay ? 'overlay' : ''}`}
    {...attributes}
    {...listeners}
  >
    <div className="board-task-heading">
      <GripVertical aria-hidden="true" />
      <strong>{task.title}</strong>
      <button
        className="icon-button"
        onClick={(event) => {
          event.stopPropagation()
          onEdit(task)
        }}
        onKeyDown={(event) => event.stopPropagation()}
        onPointerDown={(event) => event.stopPropagation()}
        aria-label={`Edit ${task.title}`}
        title="Edit task"
      ><Pencil /></button>
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
    />
  </article>
}

const nextActions: Partial<Record<TaskStatus, { label: string; action: string }[]>> = {
  Backlog: [{ label: 'Move to ready', action: 'ready' }],
  Ready: [{ label: 'Start task', action: 'start' }],
  InProgress: [{ label: 'Complete task', action: 'complete' }],
  Blocked: [{ label: 'Unblock task', action: 'unblock' }],
  Completed: [{ label: 'Reopen task', action: 'reopen' }],
}

function TaskEditor({ projectId, task, members, categories, onCategoryCreated, onClose, onSaved }: { projectId: string; task: TaskItem; members: WorkspaceMember[]; categories: ProjectCategory[]; onCategoryCreated: (category: ProjectCategory) => void; onClose: () => void; onSaved: () => void }) {
  const [saving, setSaving] = useState(false)
  const [categoryDraft, setCategoryDraft] = useState('')
  const [tagDraft, setTagDraft] = useState('')
  const [noteDraft, setNoteDraft] = useState('')
  const explanation = task.priorityExplanation
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setSaving(true)
    const data = new FormData(event.currentTarget)
    const effort = Number(data.get('effort'))
    await api.updateTask(task.id, String(data.get('title')), String(data.get('dueDate')), effort)
    await api.updatePlanning(
      task.id,
      Number(data.get('businessValue')),
      Number(data.get('urgency')),
      Number(data.get('riskReduction')),
      effort,
    )
    const assignee = String(data.get('assignedUserId') ?? '')
    if (assignee) await api.assign(task.id, assignee)
    else if (task.assignedUserId) await api.unassign(task.id)
    const categoryId = String(data.get('categoryId') ?? '')
    await api.updateCategory(task.id, categoryId || null)
    if (tagDraft.trim()) await api.addTag(task.id, tagDraft.trim())
    if (noteDraft.trim()) await api.addNote(task.id, noteDraft.trim())
    onSaved()
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
    await api.transition(task.id, action)
    onSaved()
  }
  return <div className="dialog-backdrop" role="presentation"><dialog open aria-labelledby="edit-dialog-title"><header><div><p className="eyebrow">{statusLabels[task.status]}</p><h2 id="edit-dialog-title">Edit task</h2></div><button className="icon-button" onClick={onClose} aria-label="Close"><X /></button></header>
    <form onSubmit={(event) => void submit(event)}>
      <label>Task title<input name="title" required maxLength={240} defaultValue={task.title} autoFocus /></label>
      <div className="form-grid"><label>Due date<input name="dueDate" type="date" defaultValue={task.dueDate ?? ''} /></label><label>Effort<select name="effort" defaultValue={String(explanation?.effort ?? 3)}>{[1, 2, 3, 5, 8, 13].map((value) => <option key={value}>{value}</option>)}</select><ChevronDown /></label></div>
      <label className="assignee-field">Assignee<select name="assignedUserId" defaultValue={task.assignedUserId ?? ''}><option value="">Unassigned</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.displayName} · {member.role}</option>)}</select><ChevronDown /></label>
      <fieldset><legend>Metadata</legend><div className="metadata-editor">
        <label>Category<select name="categoryId" defaultValue={task.categoryId ?? ''}><option value="">No category</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select><ChevronDown /></label>
        <div className="inline-create"><label>New category<input value={categoryDraft} onChange={(event) => setCategoryDraft(event.target.value)} maxLength={80} /></label><button type="button" className="secondary" disabled={saving || !categoryDraft.trim()} onClick={() => void createCategory()}><FolderPlus size={16} /> Add</button></div>
        <label>Add tag<input value={tagDraft} onChange={(event) => setTagDraft(event.target.value)} maxLength={40} placeholder="client, urgent, research" /></label>
        {!!task.tags.length && <div className="chip-list" aria-label="Task tags">{task.tags.map((tag) => <button type="button" className="tag-chip removable" key={tag} disabled={saving} onClick={() => void removeTag(tag)}><Tags size={12} /> {tag} <X size={12} /></button>)}</div>}
        <label className="note-field">Add note<textarea value={noteDraft} onChange={(event) => setNoteDraft(event.target.value)} maxLength={4000} rows={3} /></label>
        {!!task.notes?.length && <div className="note-list" aria-label="Task notes">{task.notes.map((note) => <article key={note.id}><MessageSquare size={15} /><p>{note.body}</p><small>{new Date(note.createdAt).toLocaleString()}</small></article>)}</div>}
      </div></fieldset>
      <fieldset><legend>Priority inputs</legend><div className="planning-grid">
        <label>Business value<input name="businessValue" type="number" min="1" max="5" defaultValue={explanation ? explanation.businessValueContribution / 3 : 3} /></label>
        <label>Urgency<input name="urgency" type="number" min="1" max="5" defaultValue={explanation ? explanation.urgencyContribution / 2 : 3} /></label>
        <label>Risk reduction<input name="riskReduction" type="number" min="1" max="5" defaultValue={explanation ? explanation.riskReductionContribution / 2 : 3} /></label>
      </div></fieldset>
      <footer className="editor-footer"><div>{nextActions[task.status]?.map(({ label, action }) => <button type="button" className="secondary" key={action} disabled={saving} onClick={() => void transition(action)}>{label}</button>)}</div><div><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving ? 'Saving...' : 'Save changes'}</button></div></footer>
    </form>
  </dialog></div>
}

function TaskDialog({ projectId, categories, onCategoryCreated, onClose, onCreated }: { projectId: string; categories: ProjectCategory[]; onCategoryCreated: (category: ProjectCategory) => void; onClose: () => void; onCreated: () => void }) {
  const [saving, setSaving] = useState(false)
  const [categoryDraft, setCategoryDraft] = useState('')
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setSaving(true)
    const data = new FormData(event.currentTarget)
    const task = await api.createTask(projectId, String(data.get('title')), String(data.get('dueDate')), Number(data.get('effort')))
    const categoryId = String(data.get('categoryId') ?? '')
    const tag = String(data.get('tag') ?? '').trim()
    const note = String(data.get('note') ?? '').trim()
    if (categoryId) await api.updateCategory(task.id, categoryId)
    if (tag) await api.addTag(task.id, tag)
    if (note) await api.addNote(task.id, note)
    onCreated()
  }
  const createCategory = async () => {
    if (!categoryDraft.trim()) return
    setSaving(true)
    const category = await api.createCategory(projectId, categoryDraft.trim())
    onCategoryCreated(category)
    setCategoryDraft('')
    setSaving(false)
  }
  return <div className="dialog-backdrop" role="presentation"><dialog open aria-labelledby="task-dialog-title"><header><div><p className="eyebrow">Portfolio launch</p><h2 id="task-dialog-title">Create task</h2></div><button className="icon-button" onClick={onClose} aria-label="Close"><X /></button></header>
    <form onSubmit={(event) => void submit(event)}><label>Task title<input name="title" required maxLength={240} autoFocus /></label><div className="form-grid"><label>Due date<input name="dueDate" type="date" /></label><label>Effort<select name="effort" defaultValue="3"><option>1</option><option>2</option><option>3</option><option>5</option><option>8</option><option>13</option></select><ChevronDown /></label></div><fieldset><legend>Metadata</legend><div className="metadata-editor"><label>Category<select name="categoryId" defaultValue=""><option value="">No category</option>{categories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select><ChevronDown /></label><div className="inline-create"><label>New category<input value={categoryDraft} onChange={(event) => setCategoryDraft(event.target.value)} maxLength={80} /></label><button type="button" className="secondary" disabled={saving || !categoryDraft.trim()} onClick={() => void createCategory()}><FolderPlus size={16} /> Add</button></div><label>Tag<input name="tag" maxLength={40} /></label><label className="note-field">Note<textarea name="note" maxLength={4000} rows={3} /></label></div></fieldset><footer><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving ? 'Creating...' : 'Create task'}</button></footer></form>
  </dialog></div>
}
