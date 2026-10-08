import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void; let reject!: (reason: unknown) => void; const promise = new Promise<T>((a, b) => { resolve = a; reject = b }); return { promise, resolve, reject } }
function fixture() {
  const loads: { url: string; reply: ReturnType<typeof deferred<any>> }[] = []
  const ui = componentHarness(new URL('../../src/components/PdfCanvasPreview.tsx', import.meta.url), {
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    './pdf/loadPdfDocument': { loadPdfDocument: (url: string) => { const reply = deferred<any>(); loads.push({ url, reply }); return reply.promise } }
  })
  function render(url = 'blob:first') {
    const tree = ui.render({ url })
    const canvas = nodes(tree).find(n => n.type === 'canvas')!
    canvas.props.ref.current ??= { getContext: () => ({}), width: 0, height: 0 }
    return tree
  }
  render(); ui.commit()
  return { ui, loads, render }
}

test('Unmount before PDF loader settles destroys its task and cannot expose stale content', async () => {
  const f = fixture(); let destroyed = 0
  const document = deferred<any>()
  f.ui.unmount()
  f.loads[0].reply.resolve({ promise: document.promise, destroy: async () => { destroyed++; document.reject(new Error('Loading destroyed')) } })
  await tick()
  assert.equal(destroyed, 1)
  assert.ok(!textContent(f.render()).includes('/9'))
})

test('Changing PDF URL suppresses old page, cancels its render, destroys old document task', async () => {
  const f = fixture(); let destroyed = 0, cancelled = 0
  const painted = deferred<void>()
  const pdf = { numPages: 2, getPage: async () => ({ getViewport: ({ scale }: { scale: number }) => ({ width: 600 * scale, height: 800 * scale }),
    render: () => ({ promise: painted.promise, cancel: () => { cancelled++ } }) }) }
  f.loads[0].reply.resolve({ promise: Promise.resolve(pdf), destroy: async () => { destroyed++ } })
  await tick(); f.render(); f.ui.commit(); await tick()
  f.render('blob:second'); f.ui.commit()
  assert.equal(cancelled, 1); assert.equal(destroyed, 1)
  painted.resolve(); await tick()
  const next = f.render('blob:second')
  assert.equal(nodes(next).find(n => n.type === 'canvas')!.props.style.display, 'none')
  assert.ok(!textContent(next).includes('2'))
  f.ui.unmount()
})

test('PDF pages render to bounded canvas, keyboard buttons respect bounds and failures remain explicit', async () => {
  const f = fixture(); const pages: number[] = []; let rendered = 0
  const pdf = { numPages: 2, getPage: async (page: number) => {
    pages.push(page)
    if (page === 2) throw new Error('Bad PDF page')
    return { getViewport: ({ scale }: { scale: number }) => ({ width: 50000 * scale, height: 70000 * scale }),
      render: () => { rendered++; return { promise: Promise.resolve(), cancel() {} } } }
  } }
  f.loads[0].reply.resolve({ promise: Promise.resolve(pdf), destroy: async () => {} })
  await tick(); f.render(); f.ui.commit(); await tick()
  const tree = f.render()
  const canvas = nodes(tree).find(n => n.type === 'canvas')!
  assert.equal(canvas.props.style.display, 'block')
  assert.ok(canvas.props.ref.current.width <= 1600 && canvas.props.ref.current.height <= 2200)
  assert.equal(rendered, 1)
  assert.equal(nodes(tree).find(n => textContent(n) === 'Previous page')!.props.disabled, true)
  nodes(tree).find(n => textContent(n) === 'Next page')!.props.onClick()
  f.render(); f.ui.commit(); await tick()
  assert.ok(textContent(f.render()).includes('Unable to preview this PDF'))
  assert.deepEqual(pages, [1, 2])
  f.ui.unmount()
})

test('Encrypted PDF failure displays download guidance rather than an infinite spinner', async () => {
  const f = fixture()
  f.loads[0].reply.reject(Object.assign(new Error('Encrypted'), { name: 'PasswordException' }))
  await tick()
  const tree = f.render()
  assert.ok(textContent(tree).includes('requires a password'))
  assert.ok(!nodes(tree).some(n => n.type === 'CircularProgress'))
  f.ui.unmount()
})
