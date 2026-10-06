import assert from 'node:assert/strict'
import { test } from 'node:test'
import { deferred, FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

import { apiContext as tab } from './helpers/api-context'
const loginResponse = (user: string) => Response.json({ success: true, data: { accessToken: `${user}-access`, refreshToken: `${user}-refresh`, user: { id: user, fullName: user, email: null, roles: ['Staff'] } } })
const expired = () => Response.json({ success: false }, { status: 401 })
const rotated = () => Response.json({ success: true, data: { accessToken: 'rotated-access', refreshToken: 'rotated-refresh' } })

function pair(fetch: typeof globalThis.fetch) {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  // The old client reads these during RED; the new client must use only SESSION_KEY.
  storage.setItem('das_access_token', 'old-access'); storage.setItem('das_refresh_token', 'old-refresh')
  return { storage, a: tab(storage, locks, fetch), b: tab(storage, locks, fetch) }
}

test('two independent API contexts share one refresh and both receive server data', async () => {
  const entered = deferred<void>(), gate = deferred<void>()
  let rotations = 0
  const { a, b } = pair(async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) { const attempt = ++rotations; entered.resolve(); await gate.promise; return attempt === 1 ? rotated() : expired() }
    if (new Headers(options?.headers).get('Authorization') === 'Bearer old-access') return expired()
    return Response.json({ success: true, data: 'server value' })
  })
  const first = a.requestApiEnvelope('document', '/fixture/first')
  const second = b.requestApiEnvelope('document', '/fixture/second')
  const results = Promise.allSettled([first, second])
  await entered.promise; gate.resolve()
  const completed = await results
  assert.equal(rotations, 1)
  assert.deepEqual(completed.map(result => result.status), ['fulfilled', 'fulfilled'])
})

test('logout in another context invalidates a pending login even when both have empty tokens', async () => {
  const entered = deferred<void>(), gate = deferred<Response>()
  const { a, b } = pair(async input => { if (String(input).endsWith('/api/auth/login')) { entered.resolve(); return gate.promise } return Response.json({ success: true }) })
  const old = a.authApi.login('old', 'fixture-password')
  const result = Promise.allSettled([old])
  await entered.promise; await b.authApi.logout(); gate.resolve(loginResponse('old'))
  assert.equal((await result)[0].status, 'rejected')
  assert.equal(b.tokenManager.getToken(), null)
})

for (const olderFirst of [false, true]) test(`login registered later in a separate context wins when the older response arrives ${olderFirst ? 'first' : 'last'}`, async () => {
  const entered = deferred<void>(), newerEntered = deferred<void>(), oldResponse = deferred<Response>(), newResponse = deferred<Response>()
  const { a, b } = pair(async (_input, options) => {
    const username = JSON.parse(String(options?.body)).username
    if (username === 'old') { entered.resolve(); return oldResponse.promise }
    newerEntered.resolve(); return newResponse.promise
  })
  const result = Promise.allSettled([a.authApi.login('old', 'fixture-password')])
  await entered.promise
  const current = Promise.allSettled([b.authApi.login('new', 'fixture-password')])
  await newerEntered.promise
  if (olderFirst) { oldResponse.resolve(loginResponse('old')); await result; newResponse.resolve(loginResponse('new')) }
  else { newResponse.resolve(loginResponse('new')); await current; oldResponse.resolve(loginResponse('old')) }
  assert.equal((await result)[0].status, 'rejected')
  assert.equal((await current)[0].status, 'fulfilled')
  assert.equal(b.tokenManager.getToken(), 'new-access')
  assert.equal(b.tokenManager.getUser().id, 'new')
})

test('a browser without Web Locks does not send credentials or trust a cached session', async () => {
  const storage = new MemoryStorage(); seedSession(storage)
  storage.setItem('das_access_token', 'old-access')
  let calls = 0
  const api = tab(storage, null, async () => { calls++; return loginResponse('new') })
  const result = await Promise.allSettled([api.authApi.login('new', 'fixture-password')])
  assert.equal(calls, 0)
  assert.equal(result[0].status, 'rejected')
  assert.equal(api.tokenManager.getToken(), null)
})

test('a delayed401 reuses a rotation completed by another tab of the same epoch', async () => {
  const delayed = deferred<Response>(), entered = deferred<void>()
  let rotations = 0
  const { a, b } = pair(async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) { rotations++; return rotated() }
    if (new Headers(options?.headers).get('Authorization') !== 'Bearer old-access') return Response.json({ success: true, data: 'same actor' })
    if (String(input).endsWith('/slow')) { entered.resolve(); return delayed.promise }
    return expired()
  })
  const old = a.requestApiEnvelope('document', '/fixture/slow')
  const result = Promise.allSettled([old])
  await entered.promise; await b.requestApiEnvelope('document', '/fixture/fast')
  delayed.resolve(expired())
  assert.equal((await result)[0].status, 'fulfilled')
  assert.equal(rotations, 1)
})

for (const duringBody of [false, true]) {
  test(`a successful old-actor ${duringBody ? 'JSON body' : 'response'} cannot populate a new session`, async () => {
    const entered = deferred<void>(), gate = deferred<void>()
    const { a, b } = pair(async input => {
      if (String(input).endsWith('/api/auth/login')) return loginResponse('new')
      if (!duringBody) { entered.resolve(); await gate.promise; return Response.json({ success: true, data: 'private old data' }) }
      const response = Response.json({ success: true })
      response.json = async () => { entered.resolve(); await gate.promise; return { success: true, data: 'private old data' } }
      return response
    })
    const result = Promise.allSettled([a.requestApiEnvelope('document', '/fixture/private')])
    await entered.promise; await b.authApi.login('new', 'fixture-password'); gate.resolve()
    const completed = (await result)[0]
    assert.equal(completed.status, 'rejected')
    if (completed.status === 'rejected') assert.equal(completed.reason.status, 409)
  })
}

test('logout in another tab does not wait for a network refresh lock', async () => {
  const entered = deferred<void>(), gate = deferred<Response>()
  const { a, b } = pair(async input => {
    if (String(input).endsWith('/api/auth/refresh')) { entered.resolve(); return gate.promise }
    if (String(input).endsWith('/api/auth/logout')) return Response.json({ success: true })
    return expired()
  })
  const pending = Promise.allSettled([a.requestApiEnvelope('document', '/fixture/old')])
  await entered.promise
  try { await b.authApi.logout(); assert.equal(b.tokenManager.getToken(), null) }
  finally { gate.resolve(rotated()) }
  assert.equal((await pending)[0].status, 'rejected')
  assert.equal(b.tokenManager.getToken(), null)
})

test('a delayed original401 reuses the latest revision after two rotations across tabs', async () => {
  const gate = deferred<Response>(), entered = deferred<void>()
  let rotations = 0
  const { a, b } = pair(async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) {
      const index = ++rotations
      return Response.json({ success: true, data: { accessToken: `access-${index}`, refreshToken: `refresh-${index}` } })
    }
    if (String(input).endsWith('/delayed') && new Headers(options?.headers).get('Authorization') === 'Bearer old-access') { entered.resolve(); return gate.promise }
    const expected = String(input).endsWith('/first') ? 'Bearer access-1' : 'Bearer access-2'
    if (new Headers(options?.headers).get('Authorization') !== expected) return expired()
    return Response.json({ success: true, data: 'same actor, latest token' })
  })
  const delayed = Promise.allSettled([a.requestApiEnvelope('document', '/fixture/delayed')])
  await entered.promise
  await a.requestApiEnvelope('document', '/fixture/first')
  await b.requestApiEnvelope('document', '/fixture/second')
  gate.resolve(expired())
  assert.equal((await delayed)[0].status, 'fulfilled')
  assert.equal(rotations, 2)
  assert.equal(a.tokenManager.getToken(), 'access-2')
})

test('logout lock timeout blocks its local epoch before starting revoke transport', async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks(), held = deferred<void>(), entered = deferred<void>()
  seedSession(storage)
  const controller = new AbortController()
  const holding = locks.request('das.auth.session.v1', { signal: controller.signal }, async () => { entered.resolve(); await held.promise })
  await entered.promise
  let sent = 0, tokenAtRevoke: string | null = 'unobserved'
  const client = tab(storage, locks, async () => { sent++; tokenAtRevoke = client.tokenManager.getToken(); return Response.json({ success: true }) }, {
    setTimeout: (callback: () => void, milliseconds: number) => setTimeout(callback, milliseconds === 5000 ? 10 : milliseconds)
  })
  try {
    await assert.rejects(client.authApi.logout(), (error: unknown) => (error as { status: number }).status === 503)
    assert.equal(sent, 1)
    assert.equal(tokenAtRevoke, null)
    assert.equal(client.tokenManager.getToken(), null)
    assert.equal(controller.signal.aborted, false)
  } finally { held.resolve(); await holding }
})

test('API refresh UUID failure does not send authority traffic or retain the owned pair', async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  let calls = 0
  const client = tab(storage, locks, async () => { calls++; return expired() }, { crypto: { randomUUID: () => { throw new Error('Fixture UUID unavailable') } } })
  await assert.rejects(client.requestApiEnvelope('document', '/fixture/owned'), (error: unknown) => (error as { status: number }).status === 401)
  assert.equal(calls, 1)
  assert.equal(client.tokenManager.getToken(), null)
})

test('a retried401 remains401 when owned invalidation cannot obtain its mutation lock', async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  const request = locks.request.bind(locks)
  let denyInvalidation = false
  locks.request = (name, options, callback) => {
    if (denyInvalidation && name === 'das.auth.session.v1') return Promise.reject(new DOMException('Fixture lock permission denied', 'SecurityError'))
    return request(name, options, callback)
  }
  const client = tab(storage, locks, async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) return rotated()
    if (new Headers(options?.headers).get('Authorization') === 'Bearer rotated-access') denyInvalidation = true
    return expired()
  })
  await assert.rejects(client.requestApiEnvelope('document', '/fixture/invalidated'), (error: unknown) => (error as { status: number }).status === 401)
  assert.equal(client.tokenManager.getToken(), null)
})

for (const replaced of [false, true]) test(`transient denied invalidation reads cannot revive rejected credentials and ${replaced ? 'preserve a replacement' : 'block the exact owned pair'} on recovery`, async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  const getItem = storage.getItem.bind(storage)
  let readsDenied = false
  storage.getItem = key => { if (readsDenied) throw new DOMException('Fixture transient storage denial', 'SecurityError'); return getItem(key) }
  const client = tab(storage, locks, async (input, options) => {
    if (String(input).endsWith('/api/auth/refresh')) return rotated()
    if (new Headers(options?.headers).get('Authorization') === 'Bearer rotated-access') {
      if (replaced) seedSession(storage, 'new-access', 'new-refresh')
      readsDenied = true
    }
    return expired()
  })
  await assert.rejects(client.requestApiEnvelope('document', '/fixture/rejected'), (error: unknown) => (error as { status: number }).status === 401)
  readsDenied = false
  assert.equal(client.tokenManager.getToken(), replaced ? 'new-access' : null)
})
