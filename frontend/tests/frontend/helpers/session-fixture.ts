import { randomUUID } from 'node:crypto'

export const SESSION_KEY = 'das_session_v1'
export const fixtureAuthority = 'http://localhost:8080'

export class MemoryStorage implements Storage {
  readonly values = new Map<string, string>()
  failWrites = false
  failRemoves = false
  get length() { return this.values.size }
  clear() { this.values.clear() }
  getItem(key: string) { return this.values.get(key) ?? null }
  key(index: number) { return [...this.values.keys()][index] ?? null }
  removeItem(key: string) { if (this.failRemoves) throw new Error('Fixture removal denied'); this.values.delete(key) }
  setItem(key: string, value: string) { if (this.failWrites) throw new Error('Fixture storage quota'); this.values.set(key, String(value)) }
}

// Independent contexts share the manager; fixtures never use a module-local lock.
export class FixtureLocks {
  private tails = new Map<string, Promise<unknown>>()
  request<T>(name: string, options: { signal?: AbortSignal }, callback: (lock: { name: string }) => T | Promise<T>): Promise<T> {
    const previous = this.tails.get(name) ?? Promise.resolve()
    const signal = options.signal
    const queued = previous.catch(() => {}).then(() => {
      signal?.throwIfAborted()
      return callback({ name })
    })
    this.tails.set(name, queued)
    if (!signal) return queued
    return new Promise((resolve, reject) => {
      const abort = () => { cleanup(); reject(signal.reason ?? new DOMException('Cancelled', 'AbortError')) }
      const cleanup = () => signal.removeEventListener('abort', abort)
      signal.addEventListener('abort', abort, { once: true })
      if (signal.aborted) abort()
      queued.then(result => { cleanup(); resolve(result) }, error => { cleanup(); reject(error) })
    })
  }
}

export function seedSession(storage: Pick<Storage, 'setItem'>, accessToken = 'old-access', refreshToken = 'old-refresh', user: unknown = null) {
  const record = { version: 1, authority: fixtureAuthority, epoch: randomUUID(), revision: 0, state: 'active', accessToken, refreshToken, user, pending: null }
  storage.setItem(SESSION_KEY, JSON.stringify(record))
  return record
}

export function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(done => { resolve = done })
  return { promise, resolve }
}
