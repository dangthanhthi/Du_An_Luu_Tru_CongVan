import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { historyApi } from '../../src/services/das/history'

const original = globalThis.fetch
afterEach(() => { globalThis.fetch = original })
const id = '11111111-1111-4111-8111-111111111111', actor = '22222222-2222-4222-8222-222222222222'
const event = { id: '33333333-3333-4333-8333-333333333333', actorUserId: actor, version: '9223372036854775807', occurredAt: '2026-10-09T01:02:03.1234567Z', action: 'Restore', changesAvailability: 'NotRecorded' }
const page = { partnerId: id, throughVersion: event.version, items: [event], totalCount: 1, pageNumber: 1, pageSize: 20 }
const reply = (data: unknown) => Response.json({ success: true, data })
test('J23 partner audit keeps Int64 strings and passes no-store, owner signal, watermark and correct service route', async () => {
  const controller = new AbortController()
  globalThis.fetch = async (input, init) => {
    const url = new URL(String(input)); assert.equal(url.pathname, `/api/partners/${id}/audit`)
    assert.equal(url.searchParams.get('throughVersion'), event.version); assert.equal(init?.cache, 'no-store'); assert.equal(init?.signal, controller.signal)
    return reply(page)
  }
  assert.deepEqual(await historyApi.partner(id, { throughVersion: event.version }, controller.signal), page)
})
for (const change of [{ partnerId: actor }, { throughVersion: 1 }, { throughVersion: '01' }, { throughVersion: '9223372036854775808' },
  { pageNumber: 2 }, { pageSize: 100 }, { totalCount: 0 }, { items: [event, event] },
  { items: [{ ...event, action: 'DeleteAll' }] }, { items: [{ ...event, actorUserId: '00000000-0000-0000-0000-000000000000' }] },
  { items: [{ ...event, version: '9223372036854775808' }] }, { items: [{ ...event, occurredAt: '2026-02-30T00:00:00Z' }] },
  { items: [{ ...event, occurredAt: '2026-10-09T00:00:00+07:00' }] }, { items: [{ ...event, changesAvailability: 'Recorded' }] },
  { items: [{ ...event, occurredAt: '2026-10-09T00:00:00+00:00' }] },
  { items: [{ ...event, actorName: 'Invented name' }] }]) {
  test('J23 rejects malformed partner page ' + JSON.stringify(change), async () => {
    globalThis.fetch = async () => reply({ ...page, ...change })
    await assert.rejects(historyApi.partner(id), (e: any) => e.status === 502)
  })
}
test('J23 decoder sorts no strings and rejects incorrect numeric order or changed watermark', async () => {
  globalThis.fetch = async () => reply({ ...page, items: [{ ...event, version: '9' }, { ...event, id: actor, version: '10' }], totalCount: 2 })
  await assert.rejects(historyApi.partner(id), (e: any) => e.status === 502)
  globalThis.fetch = async () => reply(page)
  await assert.rejects(historyApi.partner(id, { throughVersion: '10' }), (e: any) => e.status === 502)
})
const lifecycle = { id: event.id, actorUserId: actor, version: '2', occurredAt: event.occurredAt, action: 'Restore', fromStatus: 'Cancelled', toStatus: 'Distributed', cancellationReason: '<script>' + 'ế'.repeat(3990) }
const documentPage = { documentId: id, throughVersion: '2', items: [lifecycle], totalCount: 1, pageNumber: 1, pageSize: 20, coverage: 'V2LifecycleOnly' }
test('J23 lifecycle preserves long reason verbatim and both restoration destinations', async () => {
  for (const toStatus of ['InProgress', 'Distributed']) {
    globalThis.fetch = async () => reply({ ...documentPage, items: [{ ...lifecycle, toStatus }] })
    assert.equal((await historyApi.document(id)).items[0].cancellationReason, lifecycle.cancellationReason)
  }
})
for (const change of [{ coverage: 'AllHistory' }, { items: [{ ...lifecycle, toStatus: 'Cancelled' }] },
  { items: [{ ...lifecycle, cancellationReason: null }] }, { items: [{ ...lifecycle, cancellationReason: ' ' }] },
  { items: [{ ...lifecycle, cancellationReason: 'x'.repeat(4001) }] }, { items: [{ ...lifecycle, action: 'Distribute', fromStatus: 'InProgress', toStatus: 'Distributed' }] }]) {
  test('J23 malformed lifecycle is rejected ' + JSON.stringify(change).slice(0, 180), async () => {
    globalThis.fetch = async () => reply({ ...documentPage, ...change })
    await assert.rejects(historyApi.document(id), (e: any) => e.status === 502)
  })
}
test('J23 invalid request has no network side effects; later page requires an anchor', async () => {
  let calls = 0; globalThis.fetch = async () => { calls++; return reply(page) }
  for (const query of [{ pageNumber: 2 }, { throughVersion: '01' }, { throughVersion: '9223372036854775808' }, { pageSize: 101 }, { pageNumber: 0 }])
    await assert.rejects(historyApi.partner(id, query), (e: any) => e.status === 400)
  await assert.rejects(historyApi.partner('invalid'), (e: any) => e.status === 400)
  assert.equal(calls, 0)
})
