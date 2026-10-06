export const SESSION_KEY = 'das_session_v1'
const MUTATION_LOCK = 'das.auth.session.v1'
const uuid = (value: unknown): value is string => typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value)
const token = (value: unknown): value is string => typeof value === 'string' && value.length <= 16384 && /^[\x21-\x7e]+$/.test(value)
export type SessionRecord = {
  version: 1; authority: string; epoch: string; revision: number
  state: 'anonymous' | 'active' | 'refreshing'
  accessToken: string | null; refreshToken: string | null
  user: Record<string, unknown> | null; pending: string | null
}
export type SessionTokens = { accessToken: string; refreshToken: string }
export type SessionLocks = { request<T>(name: string, options: { signal: AbortSignal }, work: () => T | Promise<T>): Promise<T> }
export type SessionPlatform = {
  storage: () => Storage | null; locks: () => SessionLocks | null; id: () => string
  notify?: (identityChanged: boolean) => void; queueMs?: number; refreshMs?: number
}
export class SessionError extends Error {
  constructor(public readonly status: number, message: string) { super(message); this.name = 'SessionError' }
}
const unavailable = () => new SessionError(503, 'Trình duyệt không hỗ trợ hoặc không cho phép phối hợp phiên. Vui lòng dùng kết nối an toàn và trình duyệt được hỗ trợ.')
const storageError = () => new SessionError(0, 'Không thể lưu hoặc vô hiệu hóa phiên. Vui lòng kiểm tra quyền lưu trữ của trình duyệt và đăng nhập lại.')
const conflict = () => new SessionError(409, 'Phiên đã thay đổi trong lúc xử lý. Hãy kiểm tra lại kết quả trước khi gửi yêu cầu mới.')

export function parseSession(raw: string | null, authority: string): SessionRecord | null {
  if (raw === null || raw.length > 65536) return null
  try {
    const value = JSON.parse(raw)
    if (!value || Array.isArray(value) || value.version !== 1 || value.authority !== authority || !uuid(value.epoch) ||
        !Number.isSafeInteger(value.revision) || value.revision < 0 ||
        !(value.user === null || typeof value.user === 'object' && !Array.isArray(value.user))) return null
    if (value.state === 'anonymous') {
      if (value.revision !== 0 || value.accessToken !== null || value.refreshToken !== null || value.user !== null || value.pending !== null) return null
    } else {
      if (!token(value.accessToken) || !token(value.refreshToken)) return null
      if (value.state === 'active' ? value.pending !== null : value.state !== 'refreshing' || !uuid(value.pending)) return null
    }
    return value as SessionRecord
  } catch { return null }
}

const samePair = (a: SessionRecord | null, b: SessionRecord | null) => Boolean(a && b && a.epoch === b.epoch && a.revision === b.revision && a.accessToken === b.accessToken && a.refreshToken === b.refreshToken)
const sameCommit = (a: SessionRecord | null, b: SessionRecord | null) => samePair(a, b) && a!.state === b!.state && a!.pending === b!.pending
const commitKey = (value: SessionRecord) => JSON.stringify([value.epoch, value.revision, value.accessToken, value.refreshToken, value.state, value.pending])

export function createSessionCoordinator(authority: string, platform: SessionPlatform) {
  const blocked = new Set<string>()
  const blockedCommits = new Set<string>()
  let locksDenied = false
  let unresolvedLogout = false
  let invalidationOverflow = false
  let lastRefresh: { epoch: string; revision: number; access: string | null; refresh: string | null; promise: Promise<SessionRecord | null> } | null = null
  const storage = () => {
    try { const value = platform.storage(); if (value) return value }
    catch { /* Storage access itself can be denied by the browser. */ }
    throw storageError()
  }
  const readRaw = () => {
    try { return parseSession(storage().getItem(SESSION_KEY), authority) }
    catch { throw storageError() }
  }
  const notify = (before: SessionRecord | null, after: SessionRecord | null) => { try { platform.notify?.(before?.epoch !== after?.epoch) } catch { /* Notification is not the commit. */ } }
  const block = (epoch: string) => {
    if (blocked.has(epoch)) return
    if (blocked.size >= 32) { invalidationOverflow = true; return }
    blocked.add(epoch)
  }
  const blockCommit = (value: SessionRecord) => {
    const key = commitKey(value)

    if (blockedCommits.has(key)) return
    if (blockedCommits.size >= 32) { invalidationOverflow = true; return }
    blockedCommits.add(key)
  }
  const id = () => {
    try { const value = platform.id(); if (uuid(value)) return value }
    catch { /* A missing secure-context UUID generator cannot create a session. */ }
    throw storageError()
  }
  const anonymous = (): SessionRecord => ({ version: 1, authority, epoch: id(), revision: 0, state: 'anonymous', accessToken: null, refreshToken: null, user: null, pending: null })
  const write = (value: SessionRecord) => {
    const raw = JSON.stringify(value)
    if (!parseSession(raw, authority)) throw storageError()
    storage().setItem(SESSION_KEY, raw)
  }
  async function lock<T>(name: string, work: () => T | Promise<T>): Promise<T> {
    let locks: SessionLocks | null
    try { locks = platform.locks() }
    catch { locksDenied = true; throw unavailable() }
    if (!locks) throw unavailable()
    const controller = new AbortController()
    const timer = setTimeout(() => controller.abort(), platform.queueMs ?? 5000)
    try {
      return await locks.request(name, { signal: controller.signal }, () => { clearTimeout(timer); return work() })
    } catch (error) {
      if (['SecurityError', 'NotAllowedError'].includes((error as { name?: string }).name ?? '')) {
        locksDenied = true
        throw unavailable()
      }
      if ((error as { name?: string }).name === 'AbortError') throw new SessionError(503, 'Hết thời gian chờ phối hợp phiên. Vui lòng đăng nhập lại nếu cần.')
      throw error
    } finally { clearTimeout(timer) }
  }
  // Called only inside the short mutation lock. A removal fallback cannot
  // guarantee global invalidation if the browser denies both writes/removals.
  function invalidateOwned(expected: SessionRecord | null): boolean {
    const current = readRaw()
    if (expected && !sameCommit(current, expected)) return false
    try {
      const next = anonymous(); write(next); notify(current, next)
    } catch {
      if (current) block(current.epoch)
      try {
        if (!expected || sameCommit(readRaw(), expected)) { storage().removeItem(SESSION_KEY); notify(current, null) }
      } catch { throw storageError() }
    }
    return true
  }
  const snapshot = (): SessionRecord | null => {
    try {
      if (locksDenied || unresolvedLogout || invalidationOverflow || !platform.locks()) return null
      const value = readRaw()
      return value && !blocked.has(value.epoch) && !blockedCommits.has(commitKey(value)) ? value : null
    } catch { return null }
  }
  const assertEpoch = (expected: SessionRecord | null) => { if ((snapshot()?.epoch ?? null) !== (expected?.epoch ?? null)) throw conflict() }
  const beginLogin = () => lock(MUTATION_LOCK, () => {
    const previous = readRaw()
    let attempt: SessionRecord
    try { attempt = anonymous(); write(attempt) }
    catch {
      invalidateOwned(previous)
      throw storageError()
    }
    // Recovery requires a fresh registered login, never restoring cached identity.
    locksDenied = false
    unresolvedLogout = false
    invalidationOverflow = false
    blocked.clear()
    blockedCommits.clear()
    notify(previous, attempt)
    return attempt
  })
  const commitLogin = (attempt: SessionRecord, tokens: SessionTokens, user: Record<string, unknown>) => lock(MUTATION_LOCK, () => {
    if (!sameCommit(readRaw(), attempt) || attempt.state !== 'anonymous') throw conflict()
    const next: SessionRecord = { ...attempt, state: 'active', ...tokens, user, pending: null }
    try { write(next) }
    catch { invalidateOwned(attempt); throw storageError() }
    notify(attempt, next)
    return next
  })
  const invalidate = async (expected: SessionRecord) => {
    try { return await lock(MUTATION_LOCK, () => invalidateOwned(expected)) }
    catch (error) {
      // Recovery reads can also be denied. Remember exactly the rejected
      // commit now, so recovery never revives it or blocks a newer revision.
      blockCommit(expected)
      notify(expected, snapshot())
      throw error
    }
  }
  const logout = async () => {
    const expected = snapshot()
    try {
      return await lock(MUTATION_LOCK, () => {
        const previous = readRaw(); invalidateOwned(previous)
        return previous
      })
    } catch (error) {
      // Lock acquisition can fail before durable invalidation is possible.
      // Block only this context's captured epoch before any authority revoke.
      if (expected) { block(expected.epoch); notify(expected, null) }
      else { unresolvedLogout = true; notify(null, null) }
      throw error
    }
  }

  async function rotate(expected: SessionRecord, transport: (refresh: string, signal: AbortSignal) => Promise<SessionTokens | null>): Promise<SessionRecord | null> {
    try {
      return await lock(`das.auth.refresh.v1:${expected.epoch}`, async () => {
        const decision = await lock(MUTATION_LOCK, () => {
          const current = readRaw()
          if (!current || current.epoch !== expected.epoch || blocked.has(current.epoch) || blockedCommits.has(commitKey(current)) || unresolvedLogout || invalidationOverflow) return null
          if (current.state === 'refreshing') { invalidateOwned(current); return null }
          if (current.state !== 'active') return null
          if (current.revision > expected.revision) return { reused: current }
          if (!samePair(current, expected) || current.revision === Number.MAX_SAFE_INTEGER) { invalidateOwned(current); return null }
          let pending: SessionRecord
          try { pending = { ...current, state: 'refreshing', pending: id() }; write(pending) }
          catch { invalidateOwned(current); return null }
          return { pending }
        })
        if (!decision) return null
        if ('reused' in decision) return decision.reused!
        const pending = decision.pending!
        const controller = new AbortController()
        let timer: ReturnType<typeof setTimeout> | undefined
        let result: SessionTokens | null = null
        try {
          result = await Promise.race([
            transport(pending.refreshToken!, controller.signal),
            new Promise<null>(resolve => { timer = setTimeout(() => { controller.abort(); resolve(null) }, platform.refreshMs ?? 15000) })
          ])
        } catch { /* The authority outcome may be unknown. Never resend this pair. */ }
        finally { if (timer !== undefined) clearTimeout(timer) }
        return await lock(MUTATION_LOCK, () => {
          if (!sameCommit(readRaw(), pending)) return null
          if (!result || !token(result.accessToken) || !token(result.refreshToken)) { invalidateOwned(pending); return null }
          const next: SessionRecord = { ...pending, state: 'active', ...result, revision: pending.revision + 1, pending: null }
          try { write(next) }
          catch { invalidateOwned(pending); return null }
          notify(pending, next)
          return next
        })
      })
    } catch { return null }
  }
  function refresh(expected: SessionRecord | null, transport: (refresh: string, signal: AbortSignal) => Promise<SessionTokens | null>) {
    if (!expected?.refreshToken || expected.state === 'anonymous') return Promise.resolve(null)
    if (lastRefresh && lastRefresh.epoch === expected.epoch && lastRefresh.revision === expected.revision && lastRefresh.access === expected.accessToken && lastRefresh.refresh === expected.refreshToken) return lastRefresh.promise
    const promise = rotate(expected, transport).finally(() => {
      // Deduplicate pending work only. A later401 must re-read the shared
      // record, which another tab may have rotated more than once since then.
      if (lastRefresh?.promise === promise) lastRefresh = null
    })
    lastRefresh = { epoch: expected.epoch, revision: expected.revision, access: expected.accessToken, refresh: expected.refreshToken, promise }
    return promise
  }
  return { snapshot, assertEpoch, beginLogin, commitLogin, invalidate, logout, refresh }
}
