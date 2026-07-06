import { expect, test } from '@playwright/test'

const dashboard = {
  projectCount: 1,
  activeTaskCount: 2,
  blockedTaskCount: 1,
  overdueTaskCount: 0,
  criticalTaskCount: 1,
}
const tasks = {
  totalCount: 1,
  items: [{
    id: 'task-1',
    assignedUserId: null,
    title: 'Ship portfolio',
    status: 'Ready',
    isBlocked: false,
    dueDate: '2026-08-01',
    priorityScore: 11,
    priorityBand: 'Critical',
    deadlineHealth: 'Healthy',
    priorityExplanation: {
      score: 11,
      band: 'Critical',
      effort: 3,
      businessValueContribution: 15,
      urgencyContribution: 10,
      riskReductionContribution: 8,
    },
  }],
}

test.beforeEach(async ({ page }) => {
  await page.route('**/api/v1/workspaces', (route) =>
    route.fulfill({
      json: [{ id: 'workspace-1', name: 'Portfolio team', role: 'Owner' }],
    }))
  await page.route('**/api/v1/workspaces/workspace-1/members', (route) =>
    route.fulfill({
      json: [{
        userId: 'user-1',
        displayName: 'Jadesola Aliu',
        email: 'jadesola@example.com',
        role: 'Owner',
      }],
    }))
  await page.route('**/api/v1/dashboard', (route) =>
    route.fulfill({ json: dashboard }))
  await page.route('**/api/v1/tasks?**', (route) =>
    route.fulfill({ json: tasks }))
})

test('user can inspect prioritized work and open the editor', async ({ page }) => {
  await page.goto('/')

  await expect(page.getByRole('heading', { name: 'Delivery workspace' })).toBeVisible()
  await expect(page.getByText('Ship portfolio')).toBeVisible()
  await expect(page.getByText('Value 15 · Urgency 10 · Risk 8')).toBeVisible()

  await page.getByRole('button', { name: 'Edit Ship portfolio' }).click()
  await expect(page.getByRole('dialog', { name: 'Edit task' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Start task' })).toBeVisible()
})

test('board is keyboard accessible without horizontal page overflow', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Board' }).click()
  const editButton = page.getByTitle('Edit task')
  await editButton.focus()
  await page.keyboard.press('Enter')
  await expect(page.getByRole('dialog', { name: 'Edit task' })).toBeVisible()

  const width = await page.locator('body').evaluate((body) => ({
    scroll: body.scrollWidth,
    client: body.clientWidth,
  }))
  expect(width.scroll).toBe(width.client)
})

test('task can be dragged to a valid workflow column', async ({ page }) => {
  let status = 'Ready'
  await page.unroute('**/api/v1/tasks?**')
  await page.route('**/api/v1/tasks?**', (route) =>
    route.fulfill({
      json: {
        ...tasks,
        items: tasks.items.map((task) => ({ ...task, status })),
      },
    }))
  await page.route('**/api/v1/tasks/task-1/start', async (route) => {
    status = 'InProgress'
    await route.fulfill({ json: { status } })
  })

  await page.goto('/')
  await page.getByRole('button', { name: 'Board' }).click()
  const request = page.waitForRequest('**/api/v1/tasks/task-1/start')
  await page.locator('.board-task').dragTo(
    page.getByRole('region', { name: 'In progress tasks' }),
  )
  await request

  await expect(
    page.getByRole('region', { name: 'In progress tasks' })
      .getByText('Ship portfolio'),
  ).toBeVisible()
})
