import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { catalogApi } from '../../src/services/das/catalogs'

const original = globalThis.fetch
afterEach(() => { globalThis.fetch = original })
const id = '00000000-0000-0000-0000-000000000001'
const item = { id, group: 'methods' as const, code: 'EMAIL', name: 'Email', sortOrder: 0, isActive: true, version: 1 }
const reply = (data: unknown, status = 200) => Response.json({ success: true, data }, { status })

test('catalog lookup requests one exact group with cancellation and no caching', async () => {
  const controller = new AbortController()
  globalThis.fetch = async (input, init) => {
    const url = new URL(String(input))
    assert.equal(url.pathname, '/api/v2/catalogs')
    assert.equal(url.searchParams.get('groups'), 'methods')
    assert.equal(init?.signal, controller.signal)
    assert.equal(init?.cache, 'no-store')
    return reply({ methods: [item] })
  }
  assert.deepEqual(await catalogApi.getGroup('methods', controller.signal), [item])
})

for (const data of [[], {}, { methods: [null] }, { methods: [{ ...item, group: 'companies' }] },
  { methods: [{ ...item, name: {} }] }, { methods: [{ ...item, version: 0 }] },
  { methods: [{ ...item, isActive: false }] }, { methods: [item, item] }, { methods: [item], companies: [] }]) {
  test('malformed or cross-group catalog data fails closed: ' + JSON.stringify(data), async () => {
    globalThis.fetch = async () => reply(data)
    await assert.rejects(catalogApi.getGroup('methods'), (e: unknown) => (e as { status: number }).status === 502)
  })
}

for (const status of [401, 403, 409, 503]) test('catalog HTTP ' + status + ' remains a failure', async () => {
  globalThis.fetch = async () => reply({ methods: [item] }, status)
  await assert.rejects(catalogApi.getGroup('methods'), (e: unknown) => (e as { status: number }).status === status)
})

test('create normalizes code, sends only business fields and uses server identity', async () => {
  globalThis.fetch = async (input, init) => {
    assert.equal(new URL(String(input)).pathname, '/api/v2/admin/catalogs')
    assert.equal(init?.method, 'POST')
    assert.deepEqual(JSON.parse(String(init?.body)), { group: 'methods', code: 'EMAIL', name: 'Email' })
    return reply(item, 201)
  }
  assert.deepEqual(await catalogApi.create({ group: 'methods', code: ' email ', name: ' Email ' }), item)
})

test('update forwards the original version and preserves immutable identity and code', async () => {
  globalThis.fetch = async (input, init) => {
    assert.equal(new URL(String(input)).pathname, '/api/v2/admin/catalogs/' + id)
    assert.equal(init?.method, 'PUT')
    assert.deepEqual(JSON.parse(String(init?.body)), { name: 'New', sortOrder: 2, isActive: false, version: 1 })
    return reply({ ...item, name: 'New', sortOrder: 2, isActive: false, version: 2 })
  }
  assert.equal((await catalogApi.update(item, { name: 'New', sortOrder: 2, isActive: false })).version, 2)
})

test('historical inactive record remains readable by ID for conflict recovery', async () => {
  globalThis.fetch = async () => reply({ ...item, isActive: false, version: 3 })
  assert.equal((await catalogApi.getById(id)).isActive, false)
})

test('fixed sensitivity codes preserve the server spelling Normal and Confidential', async () => {
  const sensitivity = [{ ...item, group: 'sensitivity', code: 'Normal', name: 'Normal' }]
  globalThis.fetch = async () => reply({ sensitivity })
  assert.deepEqual(await catalogApi.getGroup('sensitivity'), sensitivity)
})

test('wrong identity in detail and mutation replies is rejected', async () => {
  globalThis.fetch = async () => reply({ ...item, id: '00000000-0000-0000-0000-000000000002' })
  await assert.rejects(catalogApi.getById(id))
  await assert.rejects(catalogApi.update(item, { name: 'Email', sortOrder: 0, isActive: true }))
})

test('fixed groups and invalid edits are blocked before any request', async () => {
  let called = false
  globalThis.fetch = async () => { called = true; return reply(item) }
  for (const group of ['companies', 'sensitivity', 'unknown'])
    await assert.rejects(catalogApi.create({ group: group as 'methods', code: 'ABC', name: 'Name' }))
  for (const patch of [{ name: '', sortOrder: 0, isActive: true }, { name: 'Name', sortOrder: -1, isActive: true }])
    await assert.rejects(catalogApi.update(item, patch))
  await assert.rejects(catalogApi.getById('../admin'))
  assert.equal(called, false)
})

test('distribution targets use server paging and retain Pending mapping', async () => {
  const data = { items: [{ id, name: 'DRI', initial: 'DRI', mappingState: 'Pending', version: 1 }], pageNumber: 2, pageSize: 10, totalCount: 11 }
  globalThis.fetch = async (input) => {
    const url = new URL(String(input))
    assert.equal(url.pathname, '/api/v2/distribution-targets')
    assert.equal(url.searchParams.get('search'), 'DRI')
    assert.equal(url.searchParams.get('pageNumber'), '2')
    return reply(data)
  }
  assert.deepEqual(await catalogApi.getDistributionTargets({ search: 'DRI', pageNumber: 2, pageSize: 10 }), data)
})

test('distribution response cannot silently widen requested paging or guess mapping', async () => {
  const target = { id, name: 'DRI', initial: 'DRI', mappingState: 'Pending', version: 1 }
  for (const data of [{ items: [target], pageNumber: 1, pageSize: 20, totalCount: 1 },
    { items: [{ ...target, mappingState: 'Guessed' }], pageNumber: 2, pageSize: 10, totalCount: 11 }]) {
    globalThis.fetch = async () => reply(data)
    await assert.rejects(catalogApi.getDistributionTargets({ pageNumber: 2, pageSize: 10 }))
  }
})
