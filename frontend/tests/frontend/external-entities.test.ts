import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { externalEntityApi } from '../../src/services/das/external-entities'
const original = globalThis.fetch
afterEach(() => { globalThis.fetch = original })
const id = '11111111-1111-4111-8111-111111111111'
const row = { id, fullName: 'Entity', shortName: null, entityType: 'Both' as const, email: null, phone: null, address: null, taxCode: null, contactPerson: null, contactInformation: null, isActive: true, isDeleted: false, version: 1 }
const page = (items: unknown[]) => ({ items, totalCount: items.length, pageNumber: 1, pageSize: 10 })
const reply = (data: unknown, status = 200) => Response.json({ success: true, data }, { status })
test('new selection rejects inactive or deleted entities even if the API widens the filter', async () => {
  for (const item of [{ ...row, isActive: false }, { ...row, isActive: false, isDeleted: true }]) {
    globalThis.fetch = async () => reply(page([item]))
    await assert.rejects(externalEntityApi.getList({ isActive: true }))
  }
})
test('historical detail must match the requested ID', async () => {
  globalThis.fetch = async () => reply({ ...row, id: '22222222-2222-4222-8222-222222222222' })
  await assert.rejects(externalEntityApi.getById(id))
})
test('AJAX lookup uses exact server paging, auth boundary, cancellation and no caching', async () => {
  const controller = new AbortController()
  globalThis.fetch = async (input, init) => {
    const url = new URL(String(input))
    assert.equal(url.pathname, '/api/partners');assert.equal(url.searchParams.get('searchTerm'), 'Entity')
    assert.equal(url.searchParams.get('isActive'), 'true');assert.equal(url.searchParams.get('pageNumber'), '2')
    assert.equal(init?.signal, controller.signal);assert.equal(init?.cache, 'no-store');assert.equal(init?.redirect, 'error')
    return reply({ items: [row], totalCount: 11, pageNumber: 2, pageSize: 10 })
  }
  assert.equal((await externalEntityApi.getList({ searchTerm: ' Entity ', isActive: true, pageNumber: 2 }, controller.signal)).totalCount, 11)
})
test('historical soft-deleted entity retains contact/name but is inactive', async () => {
  globalThis.fetch = async () => reply({ ...row, isActive: false, isDeleted: true, version: 3 })
  assert.equal((await externalEntityApi.getById(id)).fullName, 'Entity')
})
test('create accepts optional codes and strips browser authority/ID/version fields', async () => {
  globalThis.fetch = async (_url, init) => {
    assert.equal(init?.method, 'POST')
    const body = JSON.parse(String(init?.body))
    assert.equal(body.fullName, 'Entity');assert.equal(body.shortName, null);assert.equal(body.taxCode, null)
    assert.equal(body.contactPerson, 'Mai');assert.equal(body.isActive, undefined);assert.equal(body.createdByUserId, undefined);assert.equal(body.version, undefined)
    return reply({ ...row, contactPerson: 'Mai' }, 201)
  }
  assert.equal((await externalEntityApi.create({ fullName: ' Entity ', contactPerson: ' Mai ', createdByUserId: 'forged', version: 999 } as any)).version, 1)
})
test('edit uses original version and deletion/restore use CAS without invented roles', async () => {
  globalThis.fetch = async (_url, init) => {
    const body = JSON.parse(String(init?.body))
    assert.equal(body.expectedVersion, 1);assert.equal(body.isActive, false);assert.equal(body.fullName, 'Edited')
    return reply({ ...row, fullName: 'Edited', isActive: false, version: 2 })
  }
  assert.equal((await externalEntityApi.update(row, { fullName: 'Edited' }, false)).version, 2)
  globalThis.fetch = async (url, init) => {
    assert.equal(init?.method, 'DELETE');assert.equal(new URL(String(url)).pathname, '/api/partners/' + id)
    assert.deepEqual(JSON.parse(String(init?.body)), { expectedVersion: 1 })
    return reply({ ...row, isActive: false, isDeleted: true, version: 2 })
  }
  const deleted = await externalEntityApi.changeDeletion(row, true)
  globalThis.fetch = async (url, init) => {
    assert.equal(init?.method, 'POST');assert.equal(new URL(String(url)).pathname, '/api/partners/' + id + '/restore')
    assert.deepEqual(JSON.parse(String(init?.body)), { expectedVersion: 2 })
    return reply({ ...row, version: 3 })
  }
  assert.equal((await externalEntityApi.changeDeletion(deleted, false)).version, 3)
})
for (const status of [401, 403, 409, 503]) test('mutation preserves API error ' + status, async () => {
  globalThis.fetch = async () => Response.json({ success: false, message: 'Failed' }, { status })
  await assert.rejects(externalEntityApi.update(row, { fullName: 'Entity' }, true), (e: any) => e.status === status)
})
test('lost create response is an error with no fabricated success or automatic resend', async () => {
  let calls = 0
  globalThis.fetch = async () => { calls++; throw new TypeError('network') }
  await assert.rejects(externalEntityApi.create({ fullName: 'Entity' }), (e: any) => e.status === 0)
  assert.equal(calls, 1)
})
test('malformed counts, identities and paging do not become a successful list', async () => {
  for (const data of [page([{ ...row, id: 'fake' }]), { ...page([row]), pageSize: 20 }, page([row, row]), { ...page([row]), totalCount: 2 }, page([{ ...row, version: '1' }])]) {
    globalThis.fetch = async () => reply(data)
    await assert.rejects(externalEntityApi.getList())
  }
})
test('invalid submissions and path traversal are blocked before network', async () => {
  let called = false
  globalThis.fetch = async () => { called = true; return reply(row) }
  await assert.rejects(externalEntityApi.getById('../admin'))
  await assert.rejects(externalEntityApi.getList({ pageSize: 1000 }))
  await assert.rejects(externalEntityApi.create({ fullName: ' ' }))
  await assert.rejects(externalEntityApi.update({ ...row, isActive: false, isDeleted: true }, { fullName: 'Entity' }, true))
  assert.equal(called, false)
})
test('management options require a server boolean and cannot infer capability from role', async () => {
  globalThis.fetch = async () => reply({ canManage: false, role: 'Admin' })
  assert.deepEqual(await externalEntityApi.getOptions(), { canManage: false })
  globalThis.fetch = async () => reply({ role: 'Secretary' })
  await assert.rejects(externalEntityApi.getOptions())
})
