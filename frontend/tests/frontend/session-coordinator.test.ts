import assert from 'node:assert/strict'
import { randomUUID } from 'node:crypto'
import { test } from 'node:test'
import { createSessionCoordinator, parseSession, SESSION_KEY, type SessionPlatform } from '../../src/services/sessionCoordinator'
import { deferred, fixtureAuthority, FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

const returned = { accessToken: 'rotated-access', refreshToken: 'rotated-refresh' }
function fixture(overrides: Partial<SessionPlatform> = {}) {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  const platform: SessionPlatform = { storage: () => storage, locks: () => locks, id: randomUUID, refreshMs: 20, queueMs: 100, ...overrides }
  return { storage, locks, a: createSessionCoordinator(fixtureAuthority, platform), b: createSessionCoordinator(fixtureAuthority, platform) }
}

test('parser rejects invalid/foreign/oversized session records and legacy keys do not authenticate', () => {
  const { storage, a } = fixture(), good = JSON.parse(storage.getItem(SESSION_KEY)!)
  for (const candidate of [[], null, 'token', { ...good, version: 2 }, { ...good, authority: 'https://other.invalid' }, { ...good, revision: -1 },
    { ...good, revision: Number.MAX_SAFE_INTEGER + 1 }, { ...good, epoch: 'guess' }, { ...good, state: 'other' }, { ...good, pending: randomUUID() },
    { ...good, accessToken: 'line\nbreak' }, { ...good, accessToken: 'a'.repeat(16385) }, { ...good, user: [] }, { ...good, state: 'anonymous' }])
    assert.equal(parseSession(JSON.stringify(candidate), fixtureAuthority), null)
  assert.equal(parseSession('{broken', fixtureAuthority), null)
  assert.equal(parseSession('x'.repeat(65537), fixtureAuthority), null)
  storage.removeItem(SESSION_KEY); storage.setItem('das_access_token', 'legacy'); storage.setItem('das_user', '{"role":"Admin"}')
  assert.equal(a.snapshot(), null)
})

test('known Web Lock permission denial fails closed and is a typed unsupported-session error', async () => {
  const { a } = fixture({ locks: () => ({ request: async () => { throw new DOMException('Fixture permission denied', 'SecurityError') } }) })
  const result = await Promise.allSettled([a.beginLogin()])
  assert.equal(a.snapshot(), null)
  assert.equal(result[0].status, 'rejected')
  if (result[0].status === 'rejected') assert.equal(result[0].reason.status, 503)
})

test('storage read denial returns no identity and a typed error before login transport', async () => {
  const storage = new MemoryStorage(); storage.getItem = () => { throw new DOMException('Fixture storage denied', 'SecurityError') }
  const { a } = fixture({ storage: () => storage })
  assert.equal(a.snapshot(), null)
  await assert.rejects(a.beginLogin(), (error: unknown) => (error as { status: number }).status === 0)
})

test('an unavailable epoch generator invalidates the owned previous session before failing login start', async () => {
  const { a, b } = fixture({ id: () => { throw new Error('Fixture crypto unavailable') } })
  await assert.rejects(a.beginLogin())
  assert.equal(a.snapshot()?.accessToken ?? null, null)
  assert.equal(b.snapshot()?.accessToken ?? null, null)
})

test('failed login-start write removes the owned old session without creating a partial identity', async () => {
  const { storage, a, b } = fixture(); storage.failWrites = true
  await assert.rejects(a.beginLogin())
  assert.equal(storage.getItem(SESSION_KEY), null)
  assert.equal(a.snapshot(), null); assert.equal(b.snapshot(), null)
})

test('failed whole-record login commit leaves no token or identity fields from the server', async () => {
  const { storage, a, b } = fixture(), attempt = await a.beginLogin()
  storage.failWrites = true
  await assert.rejects(a.commitLogin(attempt, returned, { id: 'actor' }))
  assert.equal(storage.getItem(SESSION_KEY), null)
  assert.equal(a.snapshot(), null); assert.equal(b.snapshot(), null)
})

test('an unresolved intent left by a crashed refresh holder is never sent to authority again', async () => {
  const { storage, b } = fixture(), pending = { ...b.snapshot()!, state: 'refreshing', pending: randomUUID() }
  storage.setItem(SESSION_KEY, JSON.stringify(pending))
  let calls = 0
  assert.equal(await b.refresh(b.snapshot(), async () => { calls++; return returned }), null)
  assert.equal(calls, 0)
  assert.equal(b.snapshot()?.accessToken, null)
})

test('refresh intent quota failure prevents any authority request', async () => {
  const { storage, a, b } = fixture(), expected = a.snapshot(); storage.failWrites = true
  let calls = 0
  assert.equal(await a.refresh(expected, async () => { calls++; return returned }), null)
  assert.equal(calls, 0)
  assert.equal(b.snapshot(), null)
})

test('unavailable refresh intent UUID invalidates the owned pair without any authority request', async () => {
  const { a, b } = fixture({ id: () => { throw new Error('Fixture crypto unavailable') } })
  let calls = 0
  assert.equal(await a.refresh(a.snapshot(), async () => { calls++; return returned }), null)
  assert.equal(calls, 0)
  assert.equal(a.snapshot(), null); assert.equal(b.snapshot(), null)
})

for (const removalsDenied of [false, true]) test(`refresh commit quota failure cannot replay old token when removals are ${removalsDenied ? 'denied' : 'allowed'}`, async () => {
  const { storage, a, b } = fixture(), expected = a.snapshot()
  let calls = 0
  const transport = async () => { calls++; storage.failWrites = true; storage.failRemoves = removalsDenied; return returned }
  assert.equal(await a.refresh(expected, transport), null)
  assert.equal(a.snapshot(), null)
  assert.equal(await b.refresh(expected, transport), null)
  assert.equal(calls, 1)
  assert.equal(b.snapshot(), null)
})

test('network loss after possible authority rotation invalidates the pair in every cooperative context', async () => {
  const { a, b } = fixture(), expected = a.snapshot()
  let calls = 0
  const transport = async () => { calls++; throw new TypeError('Fixture response lost after possible commit') }
  assert.equal(await a.refresh(expected, transport), null)
  assert.equal(await b.refresh(expected, transport), null)
  assert.equal(calls, 1)
  assert.equal(a.snapshot()?.accessToken, null); assert.equal(b.snapshot()?.accessToken, null)
})

test('a refresh deadline aborts transport and the next tab cannot repeat the old pair', async () => {
  const { a, b } = fixture(), expected = a.snapshot()
  let calls = 0, signal: AbortSignal | undefined
  const transport = async (_refresh: string, value: AbortSignal) => { calls++; signal = value; return new Promise<null>(() => {}) }
  assert.equal(await a.refresh(expected, transport), null)
  assert.equal(signal?.aborted, true)
  assert.equal(await b.refresh(expected, transport), null)
  assert.equal(calls, 1)
})

test('later legitimate revisions rotate again while simultaneous callers deduplicate each revision', async () => {
  const { a, b } = fixture()
  let calls = 0
  const transport = async () => { const index = ++calls; return { accessToken: `access-${index}`, refreshToken: `refresh-${index}` } }
  const expected = a.snapshot()
  const first = await Promise.all([a.refresh(expected, transport), b.refresh(expected, transport)])
  assert.equal(calls, 1); assert.equal(first[0]?.revision, 1); assert.equal(first[1]?.accessToken, 'access-1')
  const second = await Promise.all([a.refresh(first[0], transport), b.refresh(first[0], transport)])
  assert.equal(calls, 2); assert.equal(second[0]?.revision, 2); assert.equal(second[1]?.accessToken, 'access-2')
  assert.equal(await a.invalidate(first[0]!), false)
  assert.equal(b.snapshot()?.accessToken, 'access-2')
})

for (const outcome of ['success', 'failure'] as const) test(`an old refresh ${outcome} cannot overwrite a new login registered under the independent mutation lock`, async () => {
  const { a, b } = fixture(), expected = a.snapshot()
  const result = await a.refresh(expected, async () => {
    const attempt = await b.beginLogin()
    await b.commitLogin(attempt, { accessToken: 'new-access', refreshToken: 'new-refresh' }, { id: 'new-user' })
    if (outcome === 'failure') throw new TypeError('Fixture lost old response')
    return returned
  })
  assert.equal(result, null)
  assert.equal(a.snapshot()?.accessToken, 'new-access'); assert.equal(a.snapshot()?.user?.id, 'new-user')
})

test('mutation-lock queue timeout does not send authority traffic or cancel the current lock holder', async () => {
  const gate = deferred<void>(), entered = deferred<void>()
  const { locks, a } = fixture({ queueMs: 10 })
  const controller = new AbortController()
  const held = locks.request('das.auth.session.v1', { signal: controller.signal }, async () => { entered.resolve(); await gate.promise })
  await entered.promise
  let calls = 0
  try { assert.equal(await a.refresh(a.snapshot(), async () => { calls++; return returned }), null); assert.equal(calls, 0); assert.equal(controller.signal.aborted, false) }
  finally { gate.resolve(); await held }
})

test('logout that cannot durably invalidate blocks this context without claiming a global logout', async () => {
  const { storage, a, b } = fixture(); storage.failWrites = true; storage.failRemoves = true
  await assert.rejects(a.logout())
  assert.equal(a.snapshot(), null)
  assert.equal(b.snapshot()?.accessToken, 'old-access') // Actual limit; authority revoke is a separate gate.
})

test('exact-pair local invalidation blocks a captured refresh even after storage reads recover', async () => {
  const { storage, a } = fixture(), expected = a.snapshot()!
  const getItem = storage.getItem.bind(storage)
  storage.getItem = () => { throw new DOMException('Fixture read denied', 'SecurityError') }
  await assert.rejects(a.invalidate(expected))
  storage.getItem = getItem
  let calls = 0
  assert.equal(await a.refresh(expected, async () => { calls++; return returned }), null)
  assert.equal(calls, 0)
  assert.equal(a.snapshot(), null)
})

test('logout with no readable owner stays locally unavailable on recovery until a fresh login is registered', async () => {
  const { storage, a, b } = fixture()
  const getItem = storage.getItem.bind(storage)
  storage.getItem = () => { throw new DOMException('Fixture read denied', 'SecurityError') }
  await assert.rejects(a.logout())
  storage.getItem = getItem
  assert.equal(a.snapshot(), null)
  assert.equal(b.snapshot()?.accessToken, 'old-access') // No durable or authority logout was possible.
  const attempt = await a.beginLogin()
  await a.commitLogin(attempt, returned, { id: 'fresh-user' })
  assert.equal(a.snapshot()?.user?.id, 'fresh-user')
})

test('late historical invalidations cannot evict the current rejected pair at the local tombstone limit', async () => {
  const { storage, a } = fixture(), current = a.snapshot()!
  const getItem = storage.getItem.bind(storage)
  storage.getItem = () => { throw new DOMException('Fixture read denied', 'SecurityError') }
  await assert.rejects(a.invalidate(current))
  for (let revision = 1; revision <= 32; revision++)
    await assert.rejects(a.invalidate({ ...current, revision, accessToken: `historical-access-${revision}`, refreshToken: `historical-refresh-${revision}` }))
  storage.getItem = getItem
  assert.equal(a.snapshot(), null)
  let calls = 0
  assert.equal(await a.refresh(current, async () => { calls++; return returned }), null)
  assert.equal(calls, 0)
  const fresh = await a.beginLogin()
  await a.commitLogin(fresh, returned, { id: 'new-registration' })
  assert.equal(a.snapshot()?.user?.id, 'new-registration')
})
