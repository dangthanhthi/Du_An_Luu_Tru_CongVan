import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
const tick = () => new Promise(resolve => setImmediate(resolve))
const id = '11111111-1111-4111-8111-111111111111', other = '22222222-2222-4222-8222-222222222222'
const event = { id: other, actorUserId: id, version: '2', occurredAt: '2026-10-09T01:02:03Z', action: 'Restore', fromStatus: 'Cancelled', toStatus: 'Distributed', cancellationReason: '<script>' + 'ế'.repeat(3990) }
function pending() { let resolve!: (x: any) => void; return { promise: new Promise(r => { resolve = r }), resolve } }
function fixture(options: any = {}) {
  let props = { kind: 'document', id, ...options.props }, valid = true
  const calls: any[] = []
  const api = { document: async (entityId: string, query: any, signal: AbortSignal) => { calls.push({ entityId, query, signal }); return { documentId: entityId, throughVersion: '40', coverage: 'V2LifecycleOnly', items: [event], totalCount: 21, pageNumber: query.pageNumber, pageSize: query.pageSize } }, ...options.api }
  const ui = componentHarness(new URL('../../src/views/apps/history/HistoryPanel.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ epoch: 'current', assertCurrent() { if (!valid) throw new Error('Expired') } }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: options.isEn ?? true }) },
    '@/services/das/history': { historyApi: api, historyGuid: (s: string) => /^[0-9a-f-]{36}$/.test(s) },
    '@/services/api': { ApiRequestError }
  }, { Date, Intl, Error })
  const render = () => ui.render(props)
  const flush = async () => { render(); ui.commit(); await tick(); render(); ui.commit(); await tick() }
  return { ui, render, flush, calls, setProps: (next: any) => { props = { ...props, ...next } }, expire: () => { valid = false } }
}
test('J23 UI preserves reason as escaped React text with explicit coverage, actor ID and timezone', async () => {
  const f = fixture(); await f.flush()
  const text = textContent(f.render()); assert.ok(text.includes(event.cancellationReason)); assert.ok(text.includes('Lifecycle history')); assert.ok(text.includes('UTC+7')); assert.ok(text.includes('Actor ID'))
  assert.ok(!nodes(f.render()).some(n => n.props.dangerouslySetInnerHTML)); assert.ok(nodes(f.render()).some(n => n.props.sx?.overflowWrap === 'anywhere'))
  f.ui.unmount()
})
test('J23 UI page two keeps watermark; reload drops it and returns to first page', async () => {
  const f = fixture(); await f.flush()
  nodes(f.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1); await f.flush()
  assert.equal(f.calls.at(-1).query.pageNumber, 2); assert.equal(f.calls.at(-1).query.throughVersion, '40')
  nodes(f.render()).find(n => n.type === 'Button')!.props.onClick(); await f.flush()
  assert.equal(f.calls.at(-1).query.pageNumber, 1); assert.equal(f.calls.at(-1).query.throughVersion, undefined)
  f.ui.unmount()
})
test('J23 UI changed identity resets page, aborts A and cannot show a late private response', async () => {
  const d = pending(), calls: any[] = []
  const f = fixture({ api: { document: (entityId: string, query: any, signal: AbortSignal) => { calls.push({ entityId, query, signal }); return entityId === id ? d.promise : Promise.resolve({ items: [], totalCount: 0, throughVersion: '1', pageNumber: 1, pageSize: 20 }) } } })
  await f.flush(); f.setProps({ id: other }); await f.flush(); assert.equal(calls[0].signal.aborted, true)
  d.resolve({ items: [event], totalCount: 1, throughVersion: '2' }); await tick()
  assert.ok(!textContent(f.render()).includes(event.cancellationReason)); assert.equal(calls[1].query.pageNumber, 1)
  f.ui.unmount()
})
for (const status of [401, 403, 404, 503]) test(`J23 UI ${status} clears old timeline and shows an error instead of fake empty success`, async () => {
  let failed = false
  const f = fixture({ api: { document: async () => { if (failed) throw new ApiRequestError(status, 'Denied'); return { items: [event], totalCount: 1, throughVersion: '2', pageNumber: 1, pageSize: 20 } } } })
  await f.flush(); failed = true
  nodes(f.render()).find(n => n.type === 'Button')!.props.onClick(); await f.flush()
  assert.ok(!textContent(f.render()).includes(event.cancellationReason)); assert.ok(nodes(f.render()).some(n => n.type === 'Alert' && n.props.severity === 'error'))
  f.ui.unmount()
})
test('J23 UI session change removes private history and stale callbacks cannot send another request', async () => {
  const f = fixture(); await f.flush(); const reload = nodes(f.render()).find(n => n.type === 'Button')!.props.onClick
  f.expire(); reload(); await f.flush()
  assert.ok(!textContent(f.render()).includes(event.cancellationReason)); assert.equal(f.calls.length, 1)
  f.ui.unmount()
})
test('J23 UI invalid GUID is rejected before a request; Vietnamese labels are present', async () => {
  const f = fixture({ isEn: false, props: { id: 'invalid' } }); await f.flush()
  assert.equal(f.calls.length, 0); assert.ok(textContent(f.render()).includes('Định danh'))
  f.ui.unmount()
})
