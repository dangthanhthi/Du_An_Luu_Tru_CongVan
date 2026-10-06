import assert from 'node:assert/strict'
import { afterEach, beforeEach, test } from 'node:test'
import { authApi, documentApi, tokenManager } from '../../src/services/api'

class MemoryStorage implements Storage {
  private values = new Map<string, string>()
  get length() { return this.values.size }
  clear() { this.values.clear() }
  getItem(key: string) { return this.values.get(key) ?? null }
  key(index: number) { return [...this.values.keys()][index] ?? null }
  removeItem(key: string) { this.values.delete(key) }
  setItem(key: string, value: string) { this.values.set(key, String(value)) }
}

const originalFetch = globalThis.fetch
const windowDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'window')
const storageDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
let storage: MemoryStorage
beforeEach(() => {
  storage = new MemoryStorage()
  Object.defineProperty(globalThis, 'window', { configurable: true, value: {} })
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: storage })
})
afterEach(() => {
  globalThis.fetch = originalFetch
  if (windowDescriptor) Object.defineProperty(globalThis, 'window', windowDescriptor)
  else Reflect.deleteProperty(globalThis, 'window')
  if (storageDescriptor) Object.defineProperty(globalThis, 'localStorage', storageDescriptor)
  else Reflect.deleteProperty(globalThis, 'localStorage')
})
// Exact identity shape returned by AuthController.ToCurrentUser (no username field).
const user = { id: '1e387e8a-410b-44e0-9497-3d3c2c00ec56', fullName: 'Test Staff', email: null, roles: ['Staff'] }
const data = { accessToken: 'fixture-access', refreshToken: 'fixture-refresh', user }

for (const status of [401, 403, 503]) {
  test(`HTTP ${status} cannot create a session even when the body resembles success`, async () => {
    globalThis.fetch = async () => Response.json({ success: true, data }, { status })
    await assert.rejects(authApi.login('operator', 'fixture-password'), error => (error as { status: number }).status === status)
    assert.equal(storage.getItem('das_access_token'), null)
    assert.equal(storage.getItem('das_user'), null)
  })
}
test('Malformed JSON becomes a typed invalid-response error without a session', async () => {
  globalThis.fetch = async () => new Response('not JSON', { status: 200 })
  await assert.rejects(authApi.login('operator', 'fixture-password'), error => (error as { status: number }).status === 502)
  assert.equal(storage.getItem('das_access_token'), null)
})
test('Missing server identity cannot be replaced with a fabricated user or Admin role', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { accessToken: 'fixture-access', refreshToken: 'fixture-refresh' } })
  await assert.rejects(authApi.login('operator', 'fixture-password'))
  assert.equal(storage.getItem('das_user'), null)
})
test('A valid backend session persists only the returned identity and roles', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data })
  await authApi.login('operator', 'fixture-password')
  assert.equal(storage.getItem('das_access_token'), 'fixture-access')
  assert.equal(tokenManager.getUser().id, user.id)
  assert.deepEqual(tokenManager.getUser().roles, ['Staff'])
  assert.equal(tokenManager.getUser().role, 'Staff')
})
test('An identity with no roles is never upgraded to Admin', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { ...data, user: { ...user, roles: [] } } })
  await authApi.login('operator', 'fixture-password')
  assert.deepEqual(tokenManager.getUser().roles, [])
  assert.equal(tokenManager.getUser().role, null)
})
test('Network failure clears the previous session and its local business cache', async () => {
  storage.setItem('das_access_token', 'previous-user-token')
  storage.setItem('das_user', JSON.stringify({ id: 'previous-user', role: 'Admin' }))
  storage.setItem('das_documents_store', JSON.stringify([{ title: 'previous-user-document' }]))
  globalThis.fetch = async () => { throw new TypeError('fixture network unavailable') }
  await assert.rejects(authApi.login('operator', 'fixture-password'))
  assert.equal(storage.getItem('das_access_token'), null)
  assert.equal(storage.getItem('das_user'), null)
  assert.equal(storage.getItem('das_documents_store'), null)
})

for (const status of [400, 403, 405, 503]) {
  test(`Document HTTP ${status} never reports a saved document or fabricates local data`, async () => {
    globalThis.fetch = async () => Response.json({ success: true, data: { id: 'misleading' } }, { status })
    const result = await documentApi.create({ title: 'Submitted title', direction: 'incoming' })
    assert.equal(result.success, false)
    assert.equal(storage.getItem('das_documents_store'), null)
  })
}

test('Document network error leaves the existing business cache unchanged', async () => {
  const existing = '[{"id":"historical","documentNumber":"CV-DEN-2020-0130"}]'
  storage.setItem('das_documents_store', existing)
  globalThis.fetch = async () => { throw new TypeError('network unavailable') }
  const result = await documentApi.create({ title: 'Unsaved title', direction: 'incoming' })
  assert.equal(result.success, false)
  assert.equal(storage.getItem('das_documents_store'), existing)
})

test('Document success uses the backend ID and number without a browser counter', async () => {
  const saved = { id: '0fdfcd9b-7a54-4508-b227-c0f3d3f3d5a4', documentNumber: 'CV-DEN-2026-0027', title: 'Submitted title' }
  let submitted: unknown
  globalThis.fetch = async (_input, options) => {
    submitted = JSON.parse(options?.body as string)
    return Response.json({ success: true, data: saved }, { status: 201 })
  }
  const payload = { title: 'Submitted title', direction: 'incoming', referenceNumber: '23/ABC' }
  const result = await documentApi.create(payload)
  assert.equal(result.success, true)
  assert.deepEqual(result.data, saved)
  assert.deepEqual(submitted, payload)
  assert.equal(storage.getItem('das_documents_store'), null)
})

test('Document success without a server identity is an invalid response', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { title: 'Missing identity' } })
  assert.equal((await documentApi.create({ title: 'Submitted title' })).success, false)
  assert.equal(storage.getItem('das_documents_store'), null)
})

test('Document success without a server registration number is an invalid response', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { id: 'server-id', title: 'Missing number' } })
  assert.equal((await documentApi.create({ title: 'Submitted title' })).success, false)
})
