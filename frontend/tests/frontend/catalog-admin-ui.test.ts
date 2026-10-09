import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { catalogGroups, editableCatalogGroups } from '../../src/types/das/catalogs'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
const row = { id: '11111111-1111-4111-8111-111111111111', group: 'methods', code: 'EMAIL', name: 'Stored name', sortOrder: 9, version: 7, isActive: false }
function deferred() { let resolve!: (x: any) => void; return { promise: new Promise(r => { resolve = r }), resolve } }
function fixture(options: any = {}) {
  const queries: any[] = [], writes: any[] = []
  const api = { getOptions: async () => ({ canManage: true }), getGroup: async () => [],
    getAdminPage: async (q: any, signal: any) => { queries.push({ ...q, signal }); return { ...q, items: q.group === 'methods' ? [row] : [], totalCount: q.group === 'methods' ? 1 : 0, canEditGroup: editableCatalogGroups.includes(q.group) } },
    update: async (...args: any[]) => { writes.push(args); return { ...row, isActive: true, version: 8 } }, getById: async () => ({ ...row, version: 8 }), ...options }
  const ui = componentHarness(new URL('../../src/views/apps/settings/BusinessCatalogs.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => options.intent ?? ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/api': { tokenManager: { getUser: () => ({ capabilities: [] }) } },
    '@/services/das/http': { V2ApiError: ApiRequestError }, '@/services/das/catalogs': { catalogApi: api },
    '@/types/das/catalogs': { catalogGroups, editableCatalogGroups }, '@core/components/mui/TextField': { default: 'CustomTextField' }
  }, { Error })
  const render = () => ui.render()
  const flush = async () => { render(); ui.commit(); await tick(); render(); ui.commit(); await tick() }
  const choose = async (group: string) => { nodes(render()).find(n => n.type === 'Tabs')!.props.onChange(null, group); await flush() }
  const button = (label: string) => nodes(render()).find(n => n.type === 'Button' && textContent(n) === label)
  const set = (label: string, value: string) => nodes(render()).find(n => n.props.label === label)!.props.onChange({ target: { value } })
  return { ui, render, flush, choose, button, set, queries, writes }
}
test('J22 authority comes from options despite absent local capability; inactive reactivation preserves ID/code/name/order', async () => {
  const f = fixture(); await f.flush(); await f.choose('methods')
  assert.ok(f.button('Add entry'))
  f.set('Activity', 'Inactive'); await f.flush()
  assert.equal(f.queries.at(-1).activity, 'Inactive')
  assert.ok(nodes(f.render()).some(n => n.type === 'Chip' && n.props.label === 'Inactive'))
  f.button('Reactivate')!.props.onClick()
  const form = nodes(f.render()).find(n => n.type === 'Box' && n.props.component === 'form' && nodes(n).some(x => x.type === 'DialogContent'))!
  const callback = form.props.onSubmit
  callback({ preventDefault() {} }); callback({ preventDefault() {} }); await tick()
  assert.equal(f.writes.length, 1)
  assert.equal(f.writes[0][0].id, row.id)
  assert.deepEqual(JSON.parse(JSON.stringify(f.writes[0][1])), { name: row.name, sortOrder: row.sortOrder, isActive: true })
  f.ui.unmount()
})
test('J22 options false never requests inactive or exposes activity/editor even with local capability', async () => {
  const f = fixture({ getOptions: async () => ({ canManage: false }) }); await f.flush(); await f.choose('methods')
  assert.equal(f.queries.length, 0)
  assert.equal(f.button('Add entry'), undefined)
  assert.equal(nodes(f.render()).find(n => n.props.label === 'Activity'), undefined)
  f.ui.unmount()
})
test('J22 admin 403 revokes controls and cannot keep an earlier editable dialog', async () => {
  const f = fixture({ getAdminPage: async () => { throw new ApiRequestError(403, 'Denied') } }); await f.flush(); await f.choose('methods')
  assert.equal(f.button('Add entry'), undefined)
  assert.equal(nodes(f.render()).find(n => n.props.label === 'Activity'), undefined)
  f.ui.unmount()
})
test('J22 group change aborts admin page owner and ignores late private result', async () => {
  const d = deferred(), signals: AbortSignal[] = []
  const f = fixture({ getAdminPage: (q: any, signal: AbortSignal) => { signals.push(signal); return q.group === 'companies' ? d.promise : Promise.resolve({ ...q, items: [row], totalCount: 1, canEditGroup: true }) } })
  await f.flush(); await f.choose('methods')
  assert.equal(signals[0].aborted, true)
  d.resolve({ items: [{ ...row, name: 'Stale private contents' }], totalCount: 1 }); await tick()
  assert.ok(!textContent(f.render()).includes('Stale private contents'))
  f.ui.unmount()
})
test('J22 clamp uses new effect owner with a fresh signal and clears stale rows', async () => {
  const calls: any[] = []
  const f = fixture({ getAdminPage: async (q: any, signal: AbortSignal) => { calls.push({ q, signal }); return { ...q, items: q.pageNumber === 1 ? [row] : [], totalCount: q.pageNumber === 1 ? 21 : 0, canEditGroup: true } } })
  await f.flush(); await f.choose('methods')
  nodes(f.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1)
  await f.flush()
  const last = calls.slice(-2)
  assert.equal(last[0].q.pageNumber, 2); assert.equal(last[1].q.pageNumber, 1)
  assert.equal(last[0].signal.aborted, true); assert.equal(last[1].signal.aborted, false)
  f.ui.unmount()
})
test('J22 unknown mutation cannot be retried by closing and reopening an editor; reload reconciles before save', async () => {
  let calls = 0
  const f = fixture({ update: async () => { calls++; throw new ApiRequestError(500, 'Unknown') } })
  await f.flush(); await f.choose('methods'); f.button('Reactivate')!.props.onClick()
  const submit = () => nodes(f.render()).find(n => n.type === 'Box' && n.props.component === 'form' && nodes(n).some(x => x.type === 'DialogContent'))!.props.onSubmit({ preventDefault() {} })
  submit(); await tick(); assert.equal(f.button('Save')!.props.disabled, true)
  f.button('Close')!.props.onClick(); f.button('Reactivate')!.props.onClick()
  submit(); await tick(); assert.equal(calls, 1); assert.equal(f.button('Save')!.props.disabled, true)
  f.button('Reload data')!.props.onClick(); await f.flush()
  assert.equal(f.button('Save')!.props.disabled, false)
  f.ui.unmount()
})
test('J22 options failure shows a retryable error without any admin request', async () => {
  const f = fixture({ getOptions: async () => { throw new ApiRequestError(503, 'Unavailable') } })
  await f.flush()
  assert.equal(f.queries.length, 0)
  assert.ok(nodes(f.render()).some(n => n.type === 'Alert' && n.props.severity === 'error'))
  f.ui.unmount()
})
test('J22 recovery belongs to the uncertain entry even when another row is opened', async () => {
  const readIds: string[] = []
  const other = { ...row, id: '22222222-2222-4222-8222-222222222222', code: 'POST' }
  const f = fixture({ getAdminPage: async (q: any) => ({ ...q, items: [row, other], totalCount: 2, canEditGroup: true }),
    update: async () => { throw new ApiRequestError(500, 'Unknown') },
    getById: async (id: string) => { readIds.push(id); return { ...row, version: 8 } } })
  await f.flush(); await f.choose('methods'); f.button('Reactivate')!.props.onClick()
  nodes(f.render()).find(n => n.type === 'Box' && n.props.component === 'form' && nodes(n).some(x => x.type === 'DialogContent'))!.props.onSubmit({ preventDefault() {} })
  await tick(); f.button('Close')!.props.onClick()
  nodes(f.render()).find(n => n.props['aria-label'] === 'Edit: POST')!.props.onClick()
  f.button('Reload data')!.props.onClick(); await f.flush()
  assert.deepEqual(readIds, [row.id])
  f.ui.unmount()
})
test('J22 options retry recovers a transient dependency failure; stale session renders no private rows', async () => {
  let tries = 0, valid = true
  const f = fixture({ intent: { assertCurrent() { if (!valid) throw new Error('Session changed') } },
    getOptions: async () => { if (++tries === 1) throw new ApiRequestError(503, 'Unavailable'); return { canManage: true } } })
  await f.flush(); nodes(f.render()).find(n => n.type === 'Alert' && n.props.severity === 'error')!.props.action.props.onClick(); await f.flush(); await f.choose('methods')
  assert.ok(f.button('Add entry'))
  valid = false
  assert.ok(!textContent(f.render()).includes(row.name))
  assert.equal(f.button('Add entry'), undefined)
  f.ui.unmount()
})
