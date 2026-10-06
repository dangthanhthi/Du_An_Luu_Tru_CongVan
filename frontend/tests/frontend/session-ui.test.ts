import assert from 'node:assert/strict'
import { afterEach, beforeEach, test } from 'node:test'
import * as browserSession from '../../src/services/browserSession'
import { FixtureLocks, MemoryStorage, seedSession, SESSION_KEY } from './helpers/session-fixture'
import { tokenManager } from '../../src/services/api'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import ts from 'typescript'
import { captureSessionIntent } from '../../src/services/api'

const properties = ['window', 'localStorage', 'sessionStorage', 'navigator'] as const
const previous = new Map(properties.map(key => [key, Object.getOwnPropertyDescriptor(globalThis, key)]))
let events: EventTarget, storage: MemoryStorage, drafts: MemoryStorage
beforeEach(() => {
  events = new EventTarget(); storage = new MemoryStorage(); drafts = new MemoryStorage()
  for (const [key, value] of [['window', events], ['localStorage', storage], ['sessionStorage', drafts], ['navigator', { locks: new FixtureLocks() }]] as const)
    Object.defineProperty(globalThis, key, { configurable: true, value })
  seedSession(storage)
  drafts.setItem('das_task_request:actor:document', 'private frozen body')
  drafts.setItem('preference', 'keep')
})
afterEach(() => {
  for (const key of properties) {
    const descriptor = previous.get(key)
    if (descriptor) Object.defineProperty(globalThis, key, descriptor)
    else Reflect.deleteProperty(globalThis, key)
  }
})
const storageEvent = (key: string | null) => {
  const event = new Event('storage'); Object.defineProperty(event, 'key', { value: key }); return event
}

test('same-tab epoch notifications clear private drafts and subscriptions are removable', () => {
  assert.equal(typeof browserSession.subscribeBrowserSession, 'function')
  let changes = 0
  const unsubscribe = browserSession.subscribeBrowserSession(() => changes++, tokenManager.getEpoch)
  seedSession(storage, 'new-access', 'new-refresh', { id: 'new-actor' })
  events.dispatchEvent(new Event(browserSession.SESSION_CHANGED_EVENT))
  assert.equal(changes, 1)
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
  assert.equal(drafts.getItem('preference'), 'keep')
  unsubscribe(); events.dispatchEvent(new Event(browserSession.SESSION_CHANGED_EVENT)); events.dispatchEvent(new Event('focus'))
  assert.equal(changes, 1)
})

test('same-epoch token rotation preserves frozen draft bodies and the authenticated render identity', () => {
  assert.equal(typeof browserSession.subscribeBrowserSession, 'function')
  assert.equal(typeof tokenManager.getAuthenticatedEpoch, 'function')
  const before = tokenManager.getAuthenticatedEpoch()
  let changes = 0
  const unsubscribe = browserSession.subscribeBrowserSession(() => changes++, tokenManager.getEpoch)
  const record = JSON.parse(storage.getItem(SESSION_KEY)!)
  storage.setItem(SESSION_KEY, JSON.stringify({ ...record, revision: 1, accessToken: 'rotated-access', refreshToken: 'rotated-refresh' }))
  events.dispatchEvent(storageEvent(SESSION_KEY))
  assert.equal(tokenManager.getAuthenticatedEpoch(), before)
  assert.equal(drafts.getItem('das_task_request:actor:document'), 'private frozen body')
  events.dispatchEvent(storageEvent('unrelated'))
  assert.equal(changes, 1)
  unsubscribe()
})

test('another-tab logout or a missed event caught on focus clears drafts and hides authenticated content', () => {
  assert.equal(typeof browserSession.subscribeBrowserSession, 'function')
  assert.equal(typeof tokenManager.getAuthenticatedEpoch, 'function')
  const unsubscribe = browserSession.subscribeBrowserSession(() => {}, tokenManager.getEpoch)
  storage.removeItem(SESSION_KEY)
  events.dispatchEvent(storageEvent(null))
  assert.equal(tokenManager.getAuthenticatedEpoch(), null)
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
  seedSession(storage); events.dispatchEvent(storageEvent(SESSION_KEY))
  drafts.setItem('das_task_request:actor:document', 'another private body')
  seedSession(storage, 'next-access', 'next-refresh'); events.dispatchEvent(new Event('focus'))
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
  unsubscribe()
})

// Exercise the actual boundary's render/commit ordering. The browser fixture
// separately verifies these hooks with React and native storage/Web Locks.
function boundaryHarness() {
  let state: unknown, initialized = false
  let effects: (() => void)[] = []
  let previousDependencies: readonly unknown[] | undefined
  const provider = { fixture: 'session-owner-provider' }
  const exports: { default?: (props: { children: string; locale: string }) => { type: unknown; props: { children?: unknown } } } = {}
  const source = readFileSync(new URL('../../src/components/DasSessionBoundary.tsx', import.meta.url), 'utf8')
  const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.ReactJSX } }).outputText
  runInNewContext(code, { exports, require: (name: string) => {
    if (name === 'react') return {
      useSyncExternalStore: (_subscribe: unknown, read: () => unknown) => read(),
      useMemo: (compute: () => unknown) => compute(),
      useState: (initialize: unknown) => {
        if (!initialized) { state = typeof initialize === 'function' ? initialize() : initialize; initialized = true }
        return [state, (next: unknown) => { state = next }]
      },
      useLayoutEffect: (work: () => void, dependencies: readonly unknown[]) => {
        if (!previousDependencies || dependencies.some((value, index) => !Object.is(value, previousDependencies![index]))) effects.push(work)
        previousDependencies = [...dependencies]
      }
    }
    if (name === 'react/jsx-runtime') return { jsx: (type: unknown, props: unknown) => ({ type, props }), jsxs: (type: unknown, props: unknown) => ({ type, props }) }
    if (name === '@/services/api') return { tokenManager, captureSessionIntent }
    if (name === '@/services/browserSession') return browserSession
    if (name === '@/contexts/sessionIntentContext') return { SessionIntentContext: { Provider: provider } }
    assert.fail('Unexpected boundary import ' + name)
  } })
  return {
    provider,
    render: () => exports.default!({ children: 'private-child', locale: 'vi' }),
    commit: () => { const pending = effects; effects = []; for (const effect of pending) effect() }
  }
}

test('a boundary that sees a new account during render clears old frozen bodies before mounting replacement children without notifications', () => {
  const boundary = boundaryHarness()
  assert.equal(boundary.render().type, boundary.provider); boundary.commit()
  seedSession(storage, 'replacement-access', 'replacement-refresh')
  const transitional = boundary.render()
  assert.notEqual(transitional.type, boundary.provider, 'Replacement children must wait for private storage cleanup')
  boundary.commit()
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
  assert.equal(drafts.getItem('preference'), 'keep')
  assert.equal(boundary.render().type, boundary.provider)
})

test('initial boundary mount and same-epoch rotation preserve reloadable frozen bodies', () => {
  const boundary = boundaryHarness()
  assert.equal(boundary.render().type, boundary.provider); boundary.commit()
  const record = JSON.parse(storage.getItem(SESSION_KEY)!)
  storage.setItem(SESSION_KEY, JSON.stringify({ ...record, revision: 1, accessToken: 'rotated-access', refreshToken: 'rotated-refresh' }))
  assert.equal(boundary.render().type, boundary.provider); boundary.commit()
  assert.equal(drafts.getItem('das_task_request:actor:document'), 'private frozen body')
})

test('a boundary render detecting logout hides children and clears private storage without an event', () => {
  const boundary = boundaryHarness()
  boundary.render(); boundary.commit()
  storage.removeItem(SESSION_KEY)
  assert.notEqual(boundary.render().type, boundary.provider); boundary.commit()
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
})

for (const event of ['storage', 'focus']) test(`a delayed ${event} notification cannot delete the replacement account's new frozen request after boundary cleanup`, () => {
  const listeners = [
    browserSession.subscribeBrowserSession(() => {}, tokenManager.getEpoch),
    browserSession.subscribeBrowserSession(() => {}, tokenManager.getEpoch)
  ]
  const boundary = boundaryHarness()
  boundary.render(); boundary.commit()
  seedSession(storage, 'replacement-access', 'replacement-refresh')
  boundary.render(); boundary.commit(); boundary.render()
  drafts.setItem('das_task_request:replacement:document', 'new body and idempotency key')
  events.dispatchEvent(event === 'storage' ? storageEvent(SESSION_KEY) : new Event('focus'))
  assert.equal(drafts.getItem('das_task_request:replacement:document'), 'new body and idempotency key')
  for (const unsubscribe of listeners) unsubscribe()
})

for (const failure of ['access', 'removal']) test(`transient task storage ${failure} denial keeps replacement children blocked until cleanup succeeds and then preserves new requests`, () => {
  const unsubscribe = browserSession.subscribeBrowserSession(() => {}, tokenManager.getEpoch)
  const boundary = boundaryHarness()
  boundary.render(); boundary.commit()
  seedSession(storage, 'replacement-access', 'replacement-refresh')
  if (failure === 'removal') drafts.failRemoves = true
  else Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, get: () => { throw new Error('Fixture access denial') } })
  boundary.render(); boundary.commit()
  assert.notEqual(boundary.render().type, boundary.provider, 'New account cannot read old frozen bodies while cleanup failed')
  assert.throws(() => captureSessionIntent().assertCurrent(), 'Mutations must not persist a new body before cleanup recovers')
  boundary.commit()
  drafts.failRemoves = false
  Object.defineProperty(globalThis, 'sessionStorage', { configurable: true, value: drafts })
  events.dispatchEvent(new Event('focus'))
  boundary.render(); boundary.commit()
  assert.equal(boundary.render().type, boundary.provider)
  assert.equal(drafts.getItem('das_task_request:actor:document'), null)
  drafts.setItem('das_task_request:replacement:document', 'new replacement request')
  events.dispatchEvent(storageEvent(SESSION_KEY))
  assert.equal(drafts.getItem('das_task_request:replacement:document'), 'new replacement request')
  unsubscribe()
})
