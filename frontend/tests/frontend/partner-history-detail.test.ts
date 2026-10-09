import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
const tick = () => new Promise(resolve => setImmediate(resolve))
const id = '11111111-1111-4111-8111-111111111111', other = '22222222-2222-4222-8222-222222222222'
function fixture(options: any = {}) {
  let props = { id, lang: 'en', validQuery: true }, valid = true, reads = 0
  const api = { getOptions: async () => ({ canManage: true }), getById: async () => { reads++; return { id, fullName: 'Deleted partner', shortName: 'OLD', isDeleted: true, isActive: false } }, ...options.api }
  const ui = componentHarness(new URL('../../src/views/apps/partners/detail/PartnerDetail.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ epoch: 'one', assertCurrent() { if (!valid) throw new Error('Expired') } }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) }, '@/services/das/external-entities': { externalEntityApi: api },
    '@/services/das/history': { historyGuid: (s: string) => /^[0-9a-f-]{36}$/.test(s) }, '@/services/api': { ApiRequestError },
    '@/views/apps/history/HistoryPanel': { default: 'HistoryPanel' }, 'next/link': { default: 'Link' }
  }, { Error })
  const render = () => ui.render(props)
  const flush = async () => { render(); ui.commit(); await tick(); render(); ui.commit(); await tick() }
  return { ui, render, flush, setProps: (next: any) => { props = { ...props, ...next } }, expire: () => { valid = false }, reads: () => reads }
}
test('J23 direct partner deep link exposes soft-delete history to authorized manager', async () => {
  const f = fixture(); await f.flush(); assert.ok(textContent(f.render()).includes('Deleted partner'))
  assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel')!.props.id, id); assert.ok(nodes(f.render()).some(n => n.type === 'Chip' && n.props.label === 'Soft deleted'))
  f.ui.unmount()
})
test('J23 reader denied by options cannot fetch details or mount hidden history', async () => {
  const f = fixture({ api: { getOptions: async () => ({ canManage: false }) } }); await f.flush()
  assert.equal(f.reads(), 0); assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel'), undefined)
  assert.ok(textContent(f.render()).includes('permission')); f.ui.unmount()
})
test('J23 malformed link, language or unsupported query performs no API reads', async () => {
  for (const props of [{ id: 'invalid' }, { lang: '../' }, { validQuery: false }]) {
    const f = fixture(); f.setProps(props); await f.flush(); assert.equal(f.reads(), 0); f.ui.unmount()
  }
})
test('J23 A to B late response and session change remove old partner and child history', async () => {
  let resolve!: (x: any) => void; const signals: AbortSignal[] = []
  const f = fixture({ api: { getById: (entityId: string, signal: AbortSignal) => { signals.push(signal); return entityId === id ? new Promise(r => { resolve = r }) : Promise.reject(new ApiRequestError(404, 'Missing')) } } })
  await f.flush(); f.setProps({ id: other }); await f.flush(); assert.equal(signals[0].aborted, true)
  resolve({ id, fullName: 'Private old partner', isDeleted: true }); await tick()
  assert.ok(!textContent(f.render()).includes('Private old partner')); assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel'), undefined)
  f.expire(); assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel'), undefined); f.ui.unmount()
})
test('J23 partner audit remains independently readable when legacy summary cannot decode an Int64 version', async () => {
  const f = fixture({ api: { getById: async () => { throw new ApiRequestError(502, 'Unsafe legacy summary version') } } })
  await f.flush()
  assert.ok(nodes(f.render()).some(n => n.type === 'Alert' && n.props.severity === 'error'))
  assert.ok(!textContent(f.render()).includes('Deleted partner'))
  assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel')!.props.id, id)
  f.ui.unmount()
})
test('J23 partner summary 403 still removes independent timeline despite earlier options grant', async () => {
  const f = fixture({ api: { getById: async () => { throw new ApiRequestError(403, 'Revoked') } } }); await f.flush()
  assert.equal(nodes(f.render()).find(n => n.type === 'HistoryPanel'), undefined); f.ui.unmount()
})
