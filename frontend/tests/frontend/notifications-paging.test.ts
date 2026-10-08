import assert from 'node:assert/strict'
import { test } from 'node:test'
import { apiContext } from './helpers/api-context'
import { FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

function fixture(reply: any = { items: [], totalCount: 0, page: 2, pageSize: 10 }) {
  const storage = new MemoryStorage(); seedSession(storage)
  const calls: { url: string; signal?: AbortSignal | null }[] = []
  const api = apiContext(storage, new FixtureLocks(), async (url, options) => {
    calls.push({ url: String(url), signal: options?.signal })
    return Response.json({ success: true, data: reply })
  }, {}, 'das/notifications') as unknown as typeof import('../../src/services/das/notifications')
  return { api: api.notificationsApi, calls }
}
test('notification paging uses the requested page and size with the owning AbortSignal', async () => {
  const f = fixture(), controller = new AbortController()
  await f.api.list(2, 10, controller.signal)
  const url = new URL(f.calls[0].url)
  assert.equal(url.searchParams.get('page'), '2'); assert.equal(url.searchParams.get('pageSize'), '10')
  assert.equal(f.calls[0].signal, controller.signal)
})
test('notification list retains the legacy signal-only call contract', async () => {
  const f = fixture({ items: [], totalCount: 0, page: 1, pageSize: 20 }), controller = new AbortController()
  await f.api.list(controller.signal)
  assert.equal(new URL(f.calls[0].url).searchParams.get('pageSize'), '20')
  assert.equal(f.calls[0].signal, controller.signal)
})
test('invalid notification paging is rejected before any network call', async () => {
  const f = fixture()
  for (const [page, size] of [[0, 10], [2, 101], [NaN, 10], [2, 0]]) await assert.rejects(f.api.list(page, size))
  assert.equal(f.calls.length, 0)
})
test('mismatched server page metadata and malformed totals are not displayed', async () => {
  for (const reply of [{ items: [], totalCount: 0, page: 1, pageSize: 10 }, { items: [], totalCount: -1, page: 2, pageSize: 10 }]) await assert.rejects(fixture(reply).api.list(2, 10))
})
