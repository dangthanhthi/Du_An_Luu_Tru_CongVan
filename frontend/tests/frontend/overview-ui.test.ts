import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
const tick = () => new Promise(resolve => setImmediate(resolve))
const page = (kind: string, count: number, canRegister = false) => ({ state: 'ready', data: { page: { items: [{ id: `${kind}-id`, kind, registrationNumber: '27-01-0001/HL', subject: 'Database document', status: 'InProgress', registrationDate: '2027-01-01' }], totalCount: count }, canRegister } })
const data: any = {
  Incoming: page('Incoming', 120), Outgoing: page('Outgoing', 90, true), Internal: page('Internal', 40, true),
  report: { state: 'ready', data: { items: [], total: 17, groups: [{ departmentId: 'dep', departmentName: 'Administration', count: 17 }], evaluatedAt: '2026-10-12T01:00:00Z', canExport: false } },
  notifications: { state: 'ready', data: { items: [{ id: 'n1', title: 'Notification from DB', message: 'Read me', isRead: false, createdAt: '2026-10-12T01:00:00Z', actionUrl: '/vi/apps/documents/id' }], totalCount: 30, unread: 9 } },
  staff: { state: 'ready', data: { members: { total: 42, staff: [] }, tasks: { state: 'ready', data: { taskState: 'TMS_UNAVAILABLE', tasks: null } } } }
}
function fixture(options: { data?: any; paused?: boolean } = {}) {
  const requests: any[] = [], exports: any[] = [], downloads: any[] = [], reads: string[] = []
  const window = new EventTarget()
  const ui = componentHarness(new URL('../../src/views/dashboards/overview/IncompleteOverview.tsx', import.meta.url), {
    'next/link': { default: 'Link' }, 'next/navigation': { useParams: () => ({ lang: 'en' }) },
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ epoch: 'session', assertCurrent() {} }) },
    './overview-data': { loadOverview: async (view: string, signal: AbortSignal, publish: any) => { requests.push({ view, signal, publish }); if (!options.paused) Object.entries(options.data ?? data).forEach(([key, value]) => publish(key, value)) } },
    '@/services/das/reports-staff': { incompleteReportsApi: { list: async () => data.report.data, export: async (filter: any) => { exports.push(filter); await tick(); return new Blob(['xlsx']) } } },
    '@/services/das/notifications': { notificationsApi: { read: async (id: string) => { reads.push(id); await tick() }, readAll: async () => { reads.push('all'); await tick() } } },
    '@core/components/mui/Avatar': { default: 'Avatar' }, '@core/styles/table.module.css': { default: {} }
  }, { window, Event, document: { createElement: () => ({ click() { downloads.push(this) }, set href(_: string) {}, download: '' }) }, URL: { createObjectURL: () => 'blob:xlsx', revokeObjectURL() {} } })
  return { ui, render: () => ui.render(), requests, exports, downloads, reads }
}
test('Dashboard renders exact totals, real records and unavailable tasks without fake zeros', async () => {
  const f = fixture(); f.render(); f.ui.commit(); await tick(); const tree = f.render()
  assert.ok(textContent(tree).includes('250')); assert.ok(textContent(tree).includes('42'))
  assert.ok(textContent(tree).includes('Database document')); assert.ok(textContent(tree).includes('TMS'))
  assert.ok(!nodes(tree).some(n => n.props.href === '/en/apps/documents/add?kind=Incoming'))
  assert.ok(nodes(tree).some(n => n.props.href === '/en/apps/documents/add?kind=Outgoing'))
  assert.ok(!nodes(tree).some(n => textContent(n) === 'Export Excel'))
  f.ui.unmount()
})
test('A failed document kind keeps total unknown and unrelated sections usable', async () => {
  const f = fixture({ data: { ...data, Internal: { state: 'error', status: 503 }, report: { state: 'error', status: 403 } } })
  f.render(); f.ui.commit(); await tick(); const text = textContent(f.render())
  assert.ok(!text.includes('250')); assert.ok(text.includes('temporarily unavailable'))
  assert.ok(text.includes('permission')); assert.ok(text.includes('Notification from DB'))
  f.ui.unmount()
})
test('Changing scope aborts old dashboard requests and rejects late publication', async () => {
  const f = fixture({ paused: true }); f.render(); f.ui.commit()
  const first = f.requests[0]; Object.entries(data).forEach(([key, value]) => first.publish(key, value)); let tree = f.render()
  nodes(tree).find(n => n.type === 'TextField')!.props.onChange({ target: { value: 'mine' } }); f.render(); f.ui.commit()
  assert.equal(first.signal.aborted, true); assert.equal(f.requests[1].view, 'mine')
  first.publish('Incoming', page('Incoming', 999)); tree = f.render()
  assert.ok(!textContent(tree).includes('999')); assert.ok(!textContent(tree).includes('250'))
  f.ui.unmount()
})
test('Report export uses its own scope and suppresses simultaneous duplicate downloads', async () => {
  const f = fixture({ data: { ...data, report: { state: 'ready', data: { ...data.report.data, canExport: true } } } })
  f.render(); f.ui.commit(); await tick(); const action = nodes(f.render()).find(n => n.type === 'Button' && textContent(n).includes('Export Excel'))!
  const a = action.props.onClick(); const b = action.props.onClick(); await Promise.all([a, b])
  assert.equal(f.exports.length, 1); assert.equal(f.downloads.length, 1)
  assert.deepEqual(JSON.parse(JSON.stringify(f.exports[0])), { includeRecent: false })
  f.ui.unmount()
})
test('Dashboard notification mutation is guarded against duplicate clicks', async () => {
  const f = fixture(); f.render(); f.ui.commit(); await tick()
  const action = nodes(f.render()).find(n => n.type === 'Button' && textContent(n) === 'Mark all read')!
  await Promise.all([action.props.onClick(), action.props.onClick()])
  assert.deepEqual(f.reads, ['all']); f.ui.unmount()
})
