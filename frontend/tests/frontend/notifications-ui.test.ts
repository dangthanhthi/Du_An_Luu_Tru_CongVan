import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void, reject!: (error: unknown) => void; const promise = new Promise<T>((a, b) => { resolve = a; reject = b }); return { promise, resolve, reject } }
function fixture() {
  const requests: { page: number; signal?: AbortSignal; result: ReturnType<typeof deferred<any>>; unread: ReturnType<typeof deferred<number>> }[] = []
  const timers = new Set<() => void>()
  const ui = componentHarness(new URL('../../src/components/layout/shared/DasNotificationsDropdown.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    'next/link': { default: 'Link' },
    '@/services/das/notifications': { notificationsApi: {
      list(page: number, _size: number, signal?: AbortSignal) { const request = { page, signal, result: deferred<any>(), unread: deferred<number>() }; requests.push(request); return request.result.promise },
      unread() { return requests.at(-1)!.unread.promise }, readAll: async () => {}, read: async () => {}
    } }
  }, { setInterval: (work: () => void) => { timers.add(work); return work }, clearInterval: (work: () => void) => timers.delete(work) })
  const settle = async (index: number, title: string, total = 21) => {
    requests[index].result.resolve({ items: [{ id: title, title, message: title, isRead: false, actionUrl: null, createdAt: '2026-10-06T00:00:00Z' }], totalCount: total }); requests[index].unread.resolve(total); await tick()
  }
  return { ui, requests, timers, settle }
}
test('notification responses from superseded opening/poll requests cannot overwrite the latest list', async () => {
  const f = fixture(); f.ui.render(); f.ui.commit()
  nodes(f.ui.render()).find(node => node.props['aria-label'] === 'Notifications')!.props.onClick({ currentTarget: {} })
  assert.equal(f.requests.length, 2)
  assert.equal(f.requests[0].signal?.aborted, true)
  await f.settle(1, 'Latest result')
  await f.settle(0, 'Stale result')
  const text = textContent(f.ui.render())
  assert.ok(text.includes('Latest result')); assert.ok(!text.includes('Stale result'))
  f.ui.unmount()
  assert.equal(f.requests[1].signal?.aborted, true)
  assert.equal(f.timers.size, 0)
})
test('changing notification page fetches once and clamps when the total shrinks', async () => {
  const f = fixture(); f.ui.render(); f.ui.commit(); await f.settle(0, 'First')
  nodes(f.ui.render()).find(node => node.type === 'Button' && textContent(node) === 'Next')!.props.onClick()
  f.ui.render(); f.ui.commit()
  assert.equal(f.requests.length, 2, 'Page transition must have one effect-owned request')
  assert.equal(f.requests[1].page, 2)
  await f.settle(1, 'Out of range', 1)
  f.ui.render(); f.ui.commit()
  assert.equal(f.requests.at(-1)!.page, 1)
  await f.settle(2, 'Only entry', 1)
  assert.ok(textContent(f.ui.render()).includes('Page 1/1 (1)'))
  f.ui.unmount()
})
test('failed unread/list refresh shows unknown count instead of claiming zero notifications', async () => {
  const f = fixture(); f.ui.render(); f.ui.commit()
  f.requests[0].result.reject(new Error('offline')); f.requests[0].unread.resolve(0); await tick()
  const tree = f.ui.render()
  assert.ok(textContent(tree).includes('Unable to load'))
  assert.ok(!textContent(tree).includes('Page 1/1 (0)'))
  f.ui.unmount()
})
