import assert from 'node:assert/strict'
import { afterEach, beforeEach, test } from 'node:test'
import { authApi, requestApiEnvelope, tokenManager } from '../../src/services/api'

class MemoryStorage implements Storage {
  private values = new Map<string, string>()
  failNextWriteFor?: string
  get length() { return this.values.size }
  clear() { this.values.clear() }
  getItem(key: string) { return this.values.get(key) ?? null }
  key(index: number) { return [...this.values.keys()][index] ?? null }
  removeItem(key: string) { this.values.delete(key) }
  setItem(key: string, value: string) {
    if (key === this.failNextWriteFor) { this.failNextWriteFor = undefined; throw new Error('Fixture storage write failed') }
    this.values.set(key, String(value))
  }
}
function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}
const originalFetch = globalThis.fetch
const windowDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'window')
const localDescriptor = Object.getOwnPropertyDescriptor(globalThis, 'localStorage')
let storage: MemoryStorage
beforeEach(() => {
  storage = new MemoryStorage()
  Object.defineProperty(globalThis, 'window', { configurable: true, value: {} })
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: storage })
  tokenManager.clearTokens()
  tokenManager.setTokens('old-access', 'old-refresh')
})
afterEach(() => {
  tokenManager.clearTokens()
  globalThis.fetch = originalFetch
  if (windowDescriptor) Object.defineProperty(globalThis, 'window', windowDescriptor)
  else Reflect.deleteProperty(globalThis, 'window')
  if (localDescriptor) Object.defineProperty(globalThis, 'localStorage', localDescriptor)
  else Reflect.deleteProperty(globalThis, 'localStorage')
})
const expired = () => Response.json({ success: false }, { status: 401 })
const refreshed = () => Response.json({ success: true, data: { accessToken: 'rotated-access', refreshToken: 'rotated-refresh' } })
const status401 = (error: unknown) => (error as { status: number }).status === 401
const loginResponse = (name: string) => Response.json({ success: true, data: {
  accessToken: `${name}-access`, refreshToken: `${name}-refresh`,
  user: { id: name, fullName: name, email: null, roles: ['Staff'] }
} })

test('login success arriving after logout rejects and cannot resurrect tokens or identity', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  globalThis.fetch = async () => { entered.resolve(); return gate.promise }
  const pending = assert.rejects(authApi.login('operator', 'fixture-password'), e => (e as { status: number }).status === 409)
  await entered.promise; tokenManager.clearTokens(); gate.resolve(loginResponse('old-user')); await pending
  assert.equal(tokenManager.getToken(), null)
  assert.equal(tokenManager.getRefreshToken(), null)
  assert.equal(tokenManager.getUser(), null)
})

for (const success of [true, false]) {
  test(`late login ${success ? 'success' : 'failure'} preserves a newer authenticated session`, async () => {
    const gate = deferred<Response>(), entered = deferred<void>()
    globalThis.fetch = async () => { entered.resolve(); return gate.promise }
    const pending = assert.rejects(authApi.login('operator', 'fixture-password'), e => (e as { status: number }).status === (success ? 409 : 401))
    await entered.promise; tokenManager.setTokens('new-access', 'new-refresh'); tokenManager.setUser({ id: 'new-user' })
    gate.resolve(success ? loginResponse('old-user') : expired()); await pending
    assert.equal(tokenManager.getToken(), 'new-access')
    assert.equal(tokenManager.getRefreshToken(), 'new-refresh')
    assert.equal(tokenManager.getUser().id, 'new-user')
  })
}

for (const olderFinishesFirst of [true, false]) {
  test(`overlapping logins commit only the newest attempt when older finishes ${olderFinishesFirst ? 'first' : 'last'}`, async () => {
    const older = deferred<Response>(), newer = deferred<Response>()
    let requests = 0
    globalThis.fetch = async () => ++requests === 1 ? older.promise : newer.promise
    const rejected = assert.rejects(authApi.login('old-user', 'fixture-password'), e => (e as { status: number }).status === 409)
    const current = authApi.login('new-user', 'fixture-password')
    if (olderFinishesFirst) {
      older.resolve(loginResponse('old-user')); await rejected
      assert.equal(tokenManager.getToken(), null)
      newer.resolve(loginResponse('new-user')); await current
    } else {
      newer.resolve(loginResponse('new-user')); await current
      older.resolve(loginResponse('old-user')); await rejected
    }
    assert.equal(tokenManager.getToken(), 'new-user-access')
    assert.equal(tokenManager.getRefreshToken(), 'new-user-refresh')
    assert.equal(tokenManager.getUser().id, 'new-user')
  })
}

test('a login response cannot overwrite another tab changing storage directly', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  globalThis.fetch = async () => { entered.resolve(); return gate.promise }
  const pending = assert.rejects(authApi.login('operator', 'fixture-password'), e => (e as { status: number }).status === 409)
  await entered.promise
  storage.setItem('das_access_token', 'other-tab-access'); storage.setItem('das_refresh_token', 'other-tab-refresh')
  storage.setItem('das_user', JSON.stringify({ id: 'other-tab-user' }))
  gate.resolve(loginResponse('old-user')); await pending
  assert.equal(tokenManager.getToken(), 'other-tab-access')
  assert.equal(tokenManager.getUser().id, 'other-tab-user')
})

for (const key of ['das_access_token', 'das_refresh_token', 'das_user']) {
  test(`a failed owned login storage write at ${key} leaves no partial session`, async () => {
    globalThis.fetch = async () => loginResponse('operator')
    storage.failNextWriteFor = key
    await assert.rejects(authApi.login('operator', 'fixture-password'))
    assert.equal(tokenManager.getToken(), null)
    assert.equal(tokenManager.getRefreshToken(), null)
    assert.equal(tokenManager.getUser(), null)
  })
}

test('simultaneous401s use one rotation and both callers get server data', async () => {
  const gate = deferred<void>(), entered = deferred<void>()
  let rotations = 0
  globalThis.fetch = async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) { rotations++; entered.resolve(); await gate.promise; return refreshed() }
    if (new Headers(options?.headers).get('Authorization') === 'Bearer old-access') return expired()
    assert.equal(new Headers(options?.headers).get('Authorization'), 'Bearer rotated-access')
    return Response.json({ success: true, data: String(input).split('/').pop() })
  }
  const first = requestApiEnvelope('document', '/fixture/first')
  const second = requestApiEnvelope('document', '/fixture/second')
  await entered.promise; gate.resolve()
  const result = await Promise.all([first, second])
  assert.deepEqual(result.map(row => row.data), ['first', 'second'])
  assert.equal(rotations, 1)
  assert.equal(tokenManager.getRefreshToken(), 'rotated-refresh')
})

test('refresh completing after local logout cannot resurrect tokens or retry the old operation', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  let operations = 0
  globalThis.fetch = async input => {
    if (String(input).endsWith('/api/auth/refresh')) { entered.resolve(); return gate.promise }
    operations++; return expired()
  }
  const pending = assert.rejects(requestApiEnvelope('document', '/fixture/old-write', { method: 'POST', body: '{}' }), status401)
  await entered.promise; tokenManager.clearTokens(); gate.resolve(refreshed()); await pending
  assert.equal(tokenManager.getToken(), null)
  assert.equal(tokenManager.getRefreshToken(), null)
  assert.equal(operations, 1)
})

for (const success of [true, false]) {
  test(`old refresh ${success ? 'success' : 'failure'} cannot overwrite or clear a replacement session`, async () => {
    const gate = deferred<Response>(), entered = deferred<void>()
    let operations = 0
    globalThis.fetch = async input => {
      if (String(input).endsWith('/api/auth/refresh')) { entered.resolve(); return gate.promise }
      operations++; return expired()
    }
    const pending = assert.rejects(requestApiEnvelope('document', '/fixture/old'), status401)
    await entered.promise; tokenManager.clearTokens(); tokenManager.setTokens('new-user-access', 'new-user-refresh')
    gate.resolve(success ? refreshed() : expired()); await pending
    assert.equal(tokenManager.getToken(), 'new-user-access')
    assert.equal(tokenManager.getRefreshToken(), 'new-user-refresh')
    assert.equal(operations, 1)
  })
}

test('an old401 arriving after account change never refreshes or replays with the new account', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  let requests = 0
  globalThis.fetch = async () => { requests++; entered.resolve(); return requests === 1 ? gate.promise : refreshed() }
  const pending = assert.rejects(requestApiEnvelope('document', '/fixture/old-write', { method: 'POST', body: '{"oldUserDraft":true}' }), status401)
  await entered.promise; tokenManager.clearTokens(); tokenManager.setTokens('new-user-access', 'new-user-refresh')
  gate.resolve(expired()); await pending
  assert.equal(requests, 1)
  assert.equal(tokenManager.getToken(), 'new-user-access')
})

test('logout clears locally before transport and its late response preserves a later login', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  let sentAuthorization: string | null = null, sentBody: unknown
  globalThis.fetch = async (_input, options) => {
    sentAuthorization = new Headers(options?.headers).get('Authorization')
    sentBody = JSON.parse(options?.body as string)
    entered.resolve(); return gate.promise
  }
  const pending = authApi.logout(); await entered.promise
  const tokenBeforeResponse = tokenManager.getToken()
  tokenManager.setTokens('new-user-access', 'new-user-refresh')
  gate.resolve(Response.json({ success: true })); await pending
  assert.equal(tokenBeforeResponse, null)
  assert.equal(tokenManager.getToken(), 'new-user-access')
  assert.equal(sentAuthorization, 'Bearer old-access')
  assert.deepEqual(sentBody, { refreshToken: 'old-refresh' })
})

test('a delayed retried401 cannot clear a session created while that retry was in flight', async () => {
  const gate = deferred<Response>(), retried = deferred<void>()
  globalThis.fetch = async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) return refreshed()
    if (new Headers(options?.headers).get('Authorization') === 'Bearer old-access') return expired()
    retried.resolve(); return gate.promise
  }
  const pending = assert.rejects(requestApiEnvelope('document', '/fixture/read'), status401)
  await retried.promise; tokenManager.clearTokens(); tokenManager.setTokens('new-user-access', 'new-user-refresh')
  gate.resolve(expired()); await pending
  assert.equal(tokenManager.getToken(), 'new-user-access')
})

test('one aborted caller stops waiting without cancelling the shared rotation for another caller', async () => {
  const gate = deferred<Response>(), entered = deferred<void>(), controller = new AbortController()
  let rotations = 0, retries = 0
  globalThis.fetch = async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) { rotations++; entered.resolve(); return gate.promise }
    if (new Headers(options?.headers).get('Authorization') === 'Bearer old-access') return expired()
    retries++; return Response.json({ success: true, data: 'server result' })
  }
  const cancelled = assert.rejects(requestApiEnvelope('document', '/fixture/cancelled', { signal: controller.signal }), (e: unknown) => (e as Error).name === 'AbortError')
  const active = requestApiEnvelope('document', '/fixture/active')
  await entered.promise; controller.abort(); gate.resolve(refreshed()); await cancelled
  assert.equal((await active).data, 'server result')
  assert.equal(rotations, 1)
  assert.equal(retries, 1)
  assert.equal(tokenManager.getToken(), 'rotated-access')
})

test('cancellation before401 is handled does not start a new rotation', async () => {
  const gate = deferred<Response>(), entered = deferred<void>(), controller = new AbortController()
  let rotations = 0
  globalThis.fetch = async input => {
    if (String(input).endsWith('/api/auth/refresh')) { rotations++; return refreshed() }
    entered.resolve(); return gate.promise
  }
  const pending = assert.rejects(requestApiEnvelope('document', '/fixture/cancelled', { signal: controller.signal }), (e: unknown) => (e as Error).name === 'AbortError')
  await entered.promise; controller.abort(); gate.resolve(expired()); await pending
  assert.equal(rotations, 0)
  assert.equal(tokenManager.getToken(), 'old-access')
})
