import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness } from './helpers/component-harness'
const tick = () => new Promise(resolve => setImmediate(resolve))
test('Navbar unread badge refreshes after dashboard notification events and removes its listener on unmount', async () => {
  const window = new EventTarget(); let requests = 0
  const ui = componentHarness(new URL('../../src/components/layout/shared/DasNotificationsDropdown.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({}) }, '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) }, 'next/link': { default: 'Link' },
    '@/services/das/notifications': { notificationsApi: { list: async () => { requests++; return { items: [], totalCount: 0 } }, unread: async () => 0 } }
  }, { window, setInterval: () => 1, clearInterval() {} })
  ui.render(); ui.commit(); await tick(); assert.equal(requests, 1)
  window.dispatchEvent(new Event('das_notifications_updated')); await tick(); assert.equal(requests, 2)
  ui.unmount(); window.dispatchEvent(new Event('das_notifications_updated')); await tick(); assert.equal(requests, 2)
})
