import assert from 'node:assert/strict'
import { test } from 'node:test'
import { loadOverview } from '../../src/views/dashboards/overview/overview-data'
import { ApiRequestError } from '../../src/services/api'

function sources() {
  const queries: any[] = []
  const api: any = {
    documents: async (q: any, signal: AbortSignal) => { queries.push({ ...q, signal }); const counts: Record<string, number> = { Incoming: 120, Outgoing: 90, Internal: 40 }; return { items: [], totalCount: counts[q.kind], pageNumber: 1, pageSize: 6 } },
    options: async () => ({ canRegister: true }),
    report: async () => ({ items: [], total: 17, groups: [{ count: 17 }], canExport: true }),
    notificationList: async () => ({ items: [], totalCount: 30 }), unread: async () => 9,
    staff: async () => ({ staff: [], total: 42 }), staffTasks: async () => ({ taskState: 'TMS_UNAVAILABLE', tasks: null })
  }
  return { api, queries }
}

test('Dashboard uses whole-scope totalCount, independent report scope and real unknown TMS state', async () => {
  const { api, queries } = sources(); const sections: any = {}; const signal = new AbortController().signal
  await loadOverview('department', signal, (key, value) => { sections[key] = value }, api)
  assert.equal(sections.Incoming.data.page.totalCount, 120)
  assert.equal(sections.Outgoing.data.page.totalCount, 90)
  assert.equal(sections.Internal.data.page.totalCount, 40)
  assert.equal(sections.report.data.total, 17)
  assert.equal(sections.notifications.data.unread, 9)
  assert.equal(sections.staff.data.members.total, 42)
  assert.equal(sections.staff.data.tasks.data.taskState, 'TMS_UNAVAILABLE')
  assert.equal(queries.length, 3)
  assert.ok(queries.every(q => q.view === 'department' && q.pageSize === 6 && q.pageNumber === 1 && q.signal === signal))
})

test('A report denial does not hide readable documents or convert denial to zero', async () => {
  const { api } = sources(); const sections: any = {}
  api.report = async () => { throw new ApiRequestError(403, 'Denied') }
  api.options = async () => { throw new ApiRequestError(503, 'Unavailable') }
  await loadOverview('all', new AbortController().signal, (key, value) => { sections[key] = value }, api)
  assert.deepEqual(sections.report, { state: 'error', status: 403 })
  assert.equal(sections.Incoming.state, 'ready'); assert.equal(sections.Incoming.data.canRegister, false)
  assert.equal(sections.notifications.state, 'ready')
})

test('Aborted dashboard sources never publish into the next scope', async () => {
  const { api } = sources(); const controller = new AbortController(); const published: any[] = []
  controller.abort()
  await loadOverview('mine', controller.signal, (key, value) => published.push([key, value]), api)
  assert.equal(published.length, 0)
})

test('Task contract error retains the trusted staff count and reports tasks separately', async () => {
  const { api } = sources(); const sections: any = {}
  api.staffTasks = async () => { throw new ApiRequestError(502, 'Invalid TMS') }
  await loadOverview('mine', new AbortController().signal, (key, value) => { sections[key] = value }, api)
  assert.equal(sections.staff.data.members.total, 42)
  assert.deepEqual(sections.staff.data.tasks, { state: 'error', status: 502 })
})
