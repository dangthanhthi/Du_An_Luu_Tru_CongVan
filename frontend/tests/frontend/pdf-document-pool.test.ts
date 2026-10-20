import assert from 'node:assert/strict'
import { test } from 'node:test'
import { createPdfDocumentPool } from '../../src/components/pdf/documentPool'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(r => { resolve = r }); return { resolve, promise } }

test('inline and full-screen leases parse the same mounted PDF once and destroy after the last viewer', async () => {
  let loads = 0, destroys = 0
  const pdf = { numPages: 4 }
  const pool = createPdfDocumentPool(async () => { loads++; return { promise: Promise.resolve(pdf), destroy: async () => { destroys++ } } })
  const inline = pool.acquire('blob:first'), fullscreen = pool.acquire('blob:first')
  assert.equal(await inline.promise, pdf); assert.equal(await fullscreen.promise, pdf)
  assert.equal(loads, 1)
  fullscreen.release(); fullscreen.release(); await tick(); assert.equal(destroys, 0)
  inline.release(); await tick(); assert.equal(destroys, 1)
  const reopened = pool.acquire('blob:first'); await reopened.promise
  assert.equal(loads, 2); reopened.release(); await tick(); assert.equal(destroys, 2)
})

test('last unmount before engine import resolves destroys the late task without keeping a document cache', async () => {
  const late = deferred<any>(); let loads = 0, destroyed = 0
  const pool = createPdfDocumentPool(() => { loads++; return late.promise })
  const first = pool.acquire('blob:first'); first.release()
  late.resolve({ promise: Promise.resolve({ numPages: 1 }), destroy: async () => { destroyed++ } })
  await first.promise; await tick(); assert.equal(destroyed, 1)
  const next = pool.acquire('blob:first'); await next.promise
  assert.equal(loads, 2); next.release()
})

test('different blob URLs never share PDF data and a failed load can be retried after release', async () => {
  const pool = createPdfDocumentPool(async url => {
    if (url === 'blob:bad') throw new Error('Invalid PDF')
    return { promise: Promise.resolve({ url }), destroy: async () => {} }
  })
  const a = pool.acquire('blob:a'), b = pool.acquire('blob:b'), bad = pool.acquire('blob:bad')
  assert.deepEqual(await a.promise, { url: 'blob:a' }); assert.deepEqual(await b.promise, { url: 'blob:b' })
  await assert.rejects(bad.promise, /Invalid PDF/); bad.release()
  const retry = pool.acquire('blob:bad'); await assert.rejects(retry.promise, /Invalid PDF/)
  a.release(); b.release(); retry.release()
})
