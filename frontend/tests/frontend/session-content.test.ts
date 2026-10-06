import assert from 'node:assert/strict'
import { test } from 'node:test'
import { apiContext } from './helpers/api-context'
import { deferred, FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

for (const format of ['pdf', 'xlsx'] as const) {
  for (const stage of ['response', 'stream', 'signature'] as const) {
    test(`${format} from the previous actor is rejected when the session changes during ${stage}`, async () => {
      const entered = deferred<void>(), gate = deferred<void>()
      const storage = new MemoryStorage(), locks = new FixtureLocks()
      seedSession(storage)
      // Baseline compatibility for the independent RED source snapshot only.
      storage.setItem('das_access_token', 'old-access'); storage.setItem('das_refresh_token', 'old-refresh')
      const bytes = format === 'pdf' ? new TextEncoder().encode('%PDF-fixture') : new Uint8Array([80, 75, 3, 4, 1])
      const mime = format === 'pdf' ? 'application/pdf' : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      let cancelled = false, released = false
      const delay = async () => { entered.resolve(); await gate.promise }
      const fetch: typeof globalThis.fetch = async () => {
        if (stage === 'response') await delay()
        const response = new Response(bytes, { headers: { 'content-type': mime } })
        if (stage === 'stream') {
          let first = true
          Object.defineProperty(response, 'body', { value: { getReader: () => ({
            read: async () => { if (first) { first = false; await delay(); return { value: bytes, done: false } } return { done: true } },
            cancel: async () => { cancelled = true; throw new Error('Fixture cleanup failure') },
            releaseLock: () => { released = true }
          }) } })
        }
        return response
      }
      class SignatureBlob extends Blob {
        override slice(...args: Parameters<Blob['slice']>): Blob {
          const part = super.slice(...args)
          if (stage === 'signature' && format === 'pdf') {
            const text = part.text.bind(part); part.text = async () => { await delay(); return text() }
          } else if (stage === 'signature') {
            const arrayBuffer = part.arrayBuffer.bind(part); part.arrayBuffer = async () => { await delay(); return arrayBuffer() }
          }
          return part
        }
      }
      const client = apiContext(storage, locks, fetch, { Blob: SignatureBlob })
      const result = Promise.allSettled([format === 'pdf' ? client.requestPdfBytes('fixture-file') : client.requestReportWorkbook('/fixture/report')])
      await entered.promise
      seedSession(storage, 'new-access', 'new-refresh', { id: 'new-actor' })
      // Old snapshot's token keys change as a real old-format tab would do.
      storage.setItem('das_access_token', 'new-access'); storage.setItem('das_refresh_token', 'new-refresh')
      gate.resolve()
      const completed = (await result)[0]
      assert.equal(completed.status, 'rejected', 'Old private bytes must not be delivered')
      if (completed.status === 'rejected') assert.equal(completed.reason.status, 409, 'Cleanup failure must not replace the ownership conflict')
      if (stage === 'stream') { assert.equal(cancelled, true); assert.equal(released, true) }
      assert.equal(client.tokenManager.getToken(), 'new-access')
    })
  }
}
