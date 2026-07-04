import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import {
  Activity, AlertTriangle, CheckCircle2, ChevronDown, CircleGauge,
  Clock3, Columns3, LayoutList, Menu, Plus, Search, Settings2, X,
} from 'lucide-react'
import { api } from './api'
import type { Dashboard, TaskItem, TaskStatus } from './api'
import './styles.css'

const statusLabels: Record<TaskStatus, string> = {
  Backlog: 'Backlog', Ready: 'Ready', InProgress: 'In progress',
  Blocked: 'Blocked', Completed: 'Completed',
}

const emptyDashboard: Dashboard = {
  projectCount: 0, activeTaskCount: 0, blockedTaskCount: 0,
  overdueTaskCount: 0, criticalTaskCount: 0,
}

export default function App() {
  const [tasks, setTasks] = useState<TaskItem[]>([])
  const [dashboard, setDashboard] = useState(emptyDashboard)
  const [mode, setMode] = useState<'list' | 'board'>('list')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [dialogOpen, setDialogOpen] = useState(false)
  const [navOpen, setNavOpen] = useState(false)

  const load = async () => {
    try {
      setError('')
      const [summary, page] = await Promise.all([api.dashboard(), api.tasks()])
      setDashboard(summary)
      setTasks(page.items)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load workspace.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { void load() }, [])
  const visible = useMemo(() => tasks.filter((task) =>
    task.title.toLowerCase().includes(search.toLowerCase())), [tasks, search])

  return (
    <div className="app-shell">
      <aside className={navOpen ? 'sidebar open' : 'sidebar'}>
        <div className="brand"><span className="brand-mark">T</span><strong>Todo Intelligence</strong></div>
        <nav aria-label="Primary navigation">
          <a className="active" href="#workspace"><CircleGauge size={18} /> Workspace</a>
          <a href="#activity"><Activity size={18} /> Activity</a>
          <a href="#settings"><Settings2 size={18} /> Settings</a>
        </nav>
        <div className="sidebar-foot"><span className="avatar">JA</span><div><strong>Jadesola</strong><small>Portfolio owner</small></div></div>
      </aside>

      <main id="workspace">
        <header className="topbar">
          <button className="icon-button mobile-menu" onClick={() => setNavOpen(!navOpen)} aria-label="Toggle navigation"><Menu /></button>
          <div><p className="eyebrow">Portfolio launch</p><h1>Delivery workspace</h1></div>
          <button className="primary" onClick={() => setDialogOpen(true)}><Plus size={17} /> New task</button>
        </header>

        <section className="metrics" aria-label="Portfolio health">
          <Metric label="Active work" value={dashboard.activeTaskCount} icon={<Clock3 />} />
          <Metric label="Critical" value={dashboard.criticalTaskCount} icon={<AlertTriangle />} tone="danger" />
          <Metric label="Blocked" value={dashboard.blockedTaskCount} icon={<Columns3 />} tone="warn" />
          <Metric label="Overdue" value={dashboard.overdueTaskCount} icon={<CheckCircle2 />} tone="danger" />
        </section>

        <section className="work-area">
          <div className="toolbar">
            <div className="search"><Search size={17} /><input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search tasks" aria-label="Search tasks" /></div>
            <div className="segmented" aria-label="View">
              <button className={mode === 'list' ? 'selected' : ''} onClick={() => setMode('list')}><LayoutList size={16} /> List</button>
              <button className={mode === 'board' ? 'selected' : ''} onClick={() => setMode('board')}><Columns3 size={16} /> Board</button>
            </div>
          </div>

          {error && <div className="error-state"><AlertTriangle /> <span>{error}</span><button onClick={() => void load()}>Retry</button></div>}
          {loading ? <div className="loading">Loading workspace...</div> :
            mode === 'list' ? <TaskList tasks={visible} /> : <Board tasks={visible} />}
        </section>
      </main>
      {dialogOpen && <TaskDialog onClose={() => setDialogOpen(false)} onCreated={() => { setDialogOpen(false); void load() }} />}
    </div>
  )
}

function Metric({ label, value, icon, tone = '' }: { label: string; value: number; icon: ReactNode; tone?: string }) {
  return <div className={`metric ${tone}`}><span className="metric-icon">{icon}</span><div><strong>{value}</strong><span>{label}</span></div></div>
}

function TaskList({ tasks }: { tasks: TaskItem[] }) {
  if (!tasks.length) return <div className="empty"><Search /><h2>No matching work</h2><p>Try a different search term.</p></div>
  return <div className="task-table"><div className="table-head"><span>Task</span><span>Status</span><span>Deadline</span><span>Priority</span></div>
    {tasks.map((task) => <article className="task-row" key={task.id}>
      <div className="task-name"><span className={`priority-line ${task.priorityBand?.toLowerCase()}`} /><div><strong>{task.title}</strong><small>{task.priorityExplanation ? `Value ${task.priorityExplanation.businessValueContribution} · Urgency ${task.priorityExplanation.urgencyContribution} · Risk ${task.priorityExplanation.riskReductionContribution}` : 'Planning factors not set'}</small></div></div>
      <span className={`status ${task.status.toLowerCase()}`}>{statusLabels[task.status]}</span>
      <span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.dueDate ?? 'Not scheduled'}</span>
      <span className="score"><strong>{task.priorityScore?.toFixed(1) ?? '—'}</strong><small>{task.priorityBand ?? 'Unscored'}</small></span>
    </article>)}
  </div>
}

function Board({ tasks }: { tasks: TaskItem[] }) {
  const columns: TaskStatus[] = ['Backlog', 'Ready', 'InProgress', 'Blocked', 'Completed']
  return <div className="board">{columns.map((status) => <section className="board-column" key={status}><header><span>{statusLabels[status]}</span><small>{tasks.filter((task) => task.status === status).length}</small></header>
    {tasks.filter((task) => task.status === status).map((task) => <article className="board-task" key={task.id}><strong>{task.title}</strong><div><span className={`deadline ${task.deadlineHealth.toLowerCase()}`}>{task.deadlineHealth}</span><b>{task.priorityScore?.toFixed(1) ?? '—'}</b></div></article>)}
  </section>)}</div>
}

function TaskDialog({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [saving, setSaving] = useState(false)
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); setSaving(true)
    const data = new FormData(event.currentTarget)
    await api.createTask(String(data.get('title')), String(data.get('dueDate')), Number(data.get('effort')))
    onCreated()
  }
  return <div className="dialog-backdrop" role="presentation"><dialog open aria-labelledby="task-dialog-title"><header><div><p className="eyebrow">Portfolio launch</p><h2 id="task-dialog-title">Create task</h2></div><button className="icon-button" onClick={onClose} aria-label="Close"><X /></button></header>
    <form onSubmit={(event) => void submit(event)}><label>Task title<input name="title" required maxLength={240} autoFocus /></label><div className="form-grid"><label>Due date<input name="dueDate" type="date" /></label><label>Effort<select name="effort" defaultValue="3"><option>1</option><option>2</option><option>3</option><option>5</option><option>8</option><option>13</option></select><ChevronDown /></label></div><footer><button type="button" className="secondary" onClick={onClose}>Cancel</button><button className="primary" disabled={saving}>{saving ? 'Creating...' : 'Create task'}</button></footer></form>
  </dialog></div>
}
