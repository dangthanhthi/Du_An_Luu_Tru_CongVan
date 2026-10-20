import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { catalogApi } from '../../src/services/das/catalogs'

const original = globalThis.fetch
afterEach(() => { globalThis.fetch = original })
const entry = { id: '11111111-1111-4111-8111-111111111111', group: 'methods', code: 'EMAIL', name: 'Email', sortOrder: 2, isActive: false, version: 7 }
const page = { group: 'methods', activity: 'Inactive', items: [entry], totalCount: 1, pageNumber: 1, pageSize: 20, canEditGroup: true }
const api = catalogApi as typeof catalogApi & { getOptions: (s?: AbortSignal) => Promise<{ canManage: boolean }>; getAdminPage: (q: any, s?: AbortSignal) => Promise<any> }
const reply = (data: unknown) => Response.json({ success: true, data })

test('J22 options uses authoritative no-store GET with abort and rejects malformed capability', async () => {
  const controller = new AbortController()
  globalThis.fetch = async (input, init) => {
    assert.equal(new URL(String(input)).pathname, '/api/v2/admin/catalogs/options')
    assert.equal(init?.cache, 'no-store'); assert.equal(init?.signal, controller.signal)
    return reply({ canManage: false })
  }
  assert.deepEqual(await api.getOptions(controller.signal), { canManage: false })
  for (const data of [null, {}, { canManage: 'true' }, { canManage: true, role: 'Admin' }]) {
    globalThis.fetch = async () => reply(data)
    await assert.rejects(api.getOptions(), (e: any) => e.status === 502)
  }
})
test('J22 admin browse permits inactive while active-only lookup stays strict', async () => {
  globalThis.fetch = async (input, init) => {
    const url = new URL(String(input))
    assert.equal(url.pathname, '/api/v2/admin/catalogs')
    assert.equal(url.searchParams.get('activity'), 'Inactive')
    assert.equal(url.searchParams.get('searchTerm'), '%_[]')
    assert.equal(init?.cache, 'no-store')
    return reply(page)
  }
  assert.deepEqual(await api.getAdminPage({ group: 'methods', activity: 'Inactive', searchTerm: ' %_[] ' }), page)
  globalThis.fetch = async () => reply({ methods: [entry] })
  await assert.rejects(catalogApi.getGroup('methods'), (e: any) => e.status === 502)
})
for (const bad of [{ ...page, group: 'categories' }, { ...page, activity: 'All' }, { ...page, pageNumber: 2 },
  { ...page, pageSize: 50 }, { ...page, items: [entry, entry] }, { ...page, items: [{ ...entry, isActive: true }] },
  { ...page, totalCount: 0 }, { ...page, canEditGroup: false }, { ...page, items: [{ ...entry, id: '00000000-0000-0000-0000-000000000000' }] }]) {
  test('J22 admin decoder rejects mismatched scope/count/capability: ' + JSON.stringify(bad), async () => {
    globalThis.fetch = async () => reply(bad)
    await assert.rejects(api.getAdminPage({ group: 'methods', activity: 'Inactive' }), (e: any) => e.status === 502)
  })
}
test('J22 fixed groups are browsable with editor false and empty out-of-range pages permit clamp', async () => {
  globalThis.fetch = async () => reply({ ...page, group: 'companies', items: [], totalCount: 0, pageNumber: 2, canEditGroup: false })
  assert.equal((await api.getAdminPage({ group: 'companies', activity: 'Inactive', pageNumber: 2 })).totalCount, 0)
})
test('J22 invalid admin query is rejected before HTTP', async () => {
  let calls = 0
  globalThis.fetch = async () => { calls++; return reply(page) }
  for (const query of [{ group: 'unknown' }, { group: 'methods', activity: '' }, { group: 'methods', pageNumber: 1000001 }, { group: 'methods', pageSize: 101 }, { group: 'methods', searchTerm: 'x'.repeat(201) }])
    await assert.rejects(api.getAdminPage(query), (e: any) => e.status === 400)
  assert.equal(calls, 0)
})
test('J22 incomplete terminal pages cannot hide matching inactive catalog entries', async () => {
  for (const data of [{ ...page, items: [] }, { ...page, totalCount: 20 }]) {
    globalThis.fetch = async () => reply(data)
    await assert.rejects(api.getAdminPage({ group: 'methods', activity: 'Inactive' }), (e: any) => e.status === 502)
  }
})
