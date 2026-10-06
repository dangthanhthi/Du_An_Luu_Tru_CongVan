import { createSessionCoordinator, SESSION_KEY, type SessionLocks } from './sessionCoordinator'

export const SESSION_CHANGED_EVENT = 'das:session-change'
// All boundaries/menu subscriptions in one browser context acknowledge the
// same cleanup transition. A delayed notification must not erase new drafts.
const observedEpochs = new WeakMap<Window, { epoch: string | null; pending: boolean }>()
export function isBrowserSessionCleanupReady(epoch: string | null) {
  if (typeof window === 'undefined') return true
  const observed = observedEpochs.get(window)
  return !observed || observed.epoch === epoch && !observed.pending
}
export function observeBrowserSessionEpoch(current: string | null, clearUnobserved = false) {
  if (typeof window === 'undefined') return true
  const previous = observedEpochs.get(window)
  const next = { epoch: current, pending: previous ? previous.pending || previous.epoch !== current : clearUnobserved }
  observedEpochs.set(window, next)
  if (next.pending && clearBrowserSessionData()) next.pending = false
  return !next.pending
}
export function clearPendingTaskBodies(storage: Pick<Storage, 'length' | 'key' | 'removeItem'>) {
  for (let i = storage.length - 1; i >= 0; i--) {
    const key = storage.key(i)
    if (key?.startsWith('das_task_request:')) storage.removeItem(key)
  }
}
export function clearBrowserSessionData() {
  if (typeof window === 'undefined') return true
  let cleared = true
  for (const key of ['das_access_token', 'das_refresh_token', 'das_user', 'das_documents_store', 'das_partners_store']) {
    try { localStorage.removeItem(key) } catch { cleared = false }
  }
  try { clearPendingTaskBodies(sessionStorage) } catch { cleared = false }
  try { if (typeof document !== 'undefined') document.cookie = 'das_access_token=; path=/; max-age=0' } catch { cleared = false }
  return cleared
}

export function subscribeBrowserSession(onChange: () => void, readEpoch: () => string | null) {
  if (typeof window === 'undefined') return () => {}
  observeBrowserSessionEpoch(readEpoch())
  const sync = () => {
    observeBrowserSessionEpoch(readEpoch())
    onChange()
  }
  const storageChanged = (event: StorageEvent) => {
    if (event.key === SESSION_KEY || event.key === null) sync()
  }

  window.addEventListener('storage', storageChanged)
  window.addEventListener('focus', sync)
  window.addEventListener(SESSION_CHANGED_EVENT, sync)
  return () => {
    window.removeEventListener('storage', storageChanged)
    window.removeEventListener('focus', sync)
    window.removeEventListener(SESSION_CHANGED_EVENT, sync)
  }
}
export function createBrowserSessionCoordinator(authority: string) {
  const coordinator: ReturnType<typeof createSessionCoordinator> = createSessionCoordinator(authority, {
    storage: () => typeof window === 'undefined' ? null : localStorage,
    locks: () => {
      if (typeof window === 'undefined' || typeof navigator === 'undefined' || !navigator.locks?.request) return null
      return { request: (name, options, work) => navigator.locks.request(name, options, () => work()) } as SessionLocks
    },
    id: () => crypto.randomUUID(),
    notify: identityChanged => {
      observeBrowserSessionEpoch(coordinator.snapshot()?.epoch ?? null, identityChanged)
      if (typeof window !== 'undefined' && typeof window.dispatchEvent === 'function') window.dispatchEvent(new Event(SESSION_CHANGED_EVENT))
    }
  })
  return coordinator
}
