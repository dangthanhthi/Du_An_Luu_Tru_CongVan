import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(r => { resolve = r }); return { promise, resolve } }
const doc = { header: { id: 'document-fixture', version: 7, registrationNumber: '27-01-0007/HL/ADM', allowedActions: ['ReplacePdf'] }, pdfState: 'None' }
function fixture(api: any, isEn = true) {
  const urls: string[] = [], revoked: string[] = []
  let refreshes = 0, uuid = 0
  const timers = new Map<number, () => void>()
  let timerId = 0
  let sessionCurrent = true
  const ui = componentHarness(new URL('../../src/views/apps/documents/V2PdfPanel.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() { if (!sessionCurrent) throw new Error('Session changed') } }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn }) },
    '@/services/das/document-pdf': { documentPdfApi: api },
    '@/components/DocumentPDFPreview': { default: 'PdfPreview' },
    '@/components/pdf/loadPdfDocument': { preloadPdfEngine: async () => {} }, './PrintedPdfPanel': { default: 'PrintedPdfPanel' }
  }, { Error, setTimeout: (fn: () => void) => { timers.set(++timerId, fn); return timerId }, clearTimeout: (id: number) => timers.delete(id), crypto: { randomUUID: () => `operation-${++uuid}` },
    URL: { createObjectURL() { const url = `blob:fixture-${urls.length}`; urls.push(url); return url }, revokeObjectURL(url: string) { revoked.push(url) } } })
  const render = (document: any = doc) => ui.render({ document, onChange: () => { refreshes++ } })
  return { ui, render, urls, revoked, timers, expireSession: () => { sessionCurrent = false },
    advance: async () => { const first = timers.entries().next().value; if (first) { timers.delete(first[0]); first[1]() } await tick() }, refreshes: () => refreshes }
}

for (const isEn of [false, true]) {
  test(`Invalid PDF upload uses the selected ${isEn ? 'English' : 'Vietnamese'} language and keeps the current preview`, async () => {
    const f = fixture({
      info: async () => ({ fileId: 'current-file', originalName: 'valid.pdf', completion: { isComplete: true } }),
      bytes: async () => new Blob(['current-pdf']),
      upload: async () => { throw new ApiRequestError(422, 'The PDF is malformed, encrypted or outside the supported page limit.', undefined, 'INVALID_PDF') }
    }, isEn)
    const current = { ...doc, pdfState: 'Ready' }
    f.render(current); f.ui.commit(); await tick()
    nodes(f.render(current)).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } })
    await tick()
    const text = textContent(f.render(current))
    assert.ok(text.includes(isEn ? 'PDF is invalid' : 'Tệp PDF không hợp lệ'), text)
    assert.ok(!text.includes('The PDF is malformed'))
    assert.equal(nodes(f.render(current)).find(n => n.type === 'PdfPreview')!.props.pdfUrl, 'blob:fixture-0')
    assert.equal(f.refreshes(), 0)
    f.ui.unmount()
  })

  test(`Invalid PDF metadata is translated in ${isEn ? 'English' : 'Vietnamese'} load errors`, async () => {
    const f = fixture({ info: async () => { throw new ApiRequestError(422, 'Server-only English', undefined, 'INVALID_PDF') } }, isEn)
    const current = { ...doc, pdfState: 'Ready' }
    f.render(current); f.ui.commit(); await tick()
    const text = textContent(f.render(current))
    assert.ok(text.includes(isEn ? 'PDF is invalid' : 'Tệp PDF không hợp lệ'), text)
    assert.ok(!text.includes('Server-only English'))
    assert.equal(f.urls.length, 0)
    f.ui.unmount()
  })
}

test('Select PDF activates the native file input through a real button handler', () => {
  const f = fixture({})
  const tree = f.render()
  const input = nodes(tree).find(n => n.type === 'input')!
  const select = nodes(tree).find(n => n.type === 'Button' && textContent(n).trim() === 'Select PDF')!
  assert.equal(typeof select.props.onClick, 'function', 'keyboard-activated button must open the chooser')
  let opens = 0
  input.props.ref.current = { click() { opens++ } }
  select.props.onClick()
  assert.equal(opens, 1)
  assert.equal(f.refreshes(), 0)
  f.ui.unmount()
})
test('PDF replacement retries an ambiguous response with the same operation, file and aggregate version', async () => {
  let uploads = 0
  const replacements: any[][] = []
  const f = fixture({ upload: async () => { uploads++; return { id: 'file-fixture' } }, uploadInfo: async () => ({ state: 'Available', canAttach: true }),
    replace: async (...args: any[]) => { replacements.push(args.slice(0, 4)); if (replacements.length === 1) throw new ApiRequestError(0, 'Lost response') } })
  nodes(f.render()).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: 'chosen' } }); await tick()
  assert.equal(replacements.length, 1); assert.equal(f.refreshes(), 0)
  await nodes(f.render()).find(n => n.type === 'Button' && textContent(n) === 'Retry with same request')!.props.onClick()
  assert.equal(uploads, 1); assert.deepEqual(replacements[1], replacements[0])
  assert.deepEqual(replacements[0], ['document-fixture', 'operation-1', 'file-fixture', 7])
  assert.equal(f.refreshes(), 1)
  f.ui.unmount()
})

test('First passive effect cannot unlock an upload started immediately after initial render', async () => {
  const upload = deferred<any>(); let uploads = 0
  const f = fixture({ upload: async () => { uploads++; return upload.promise }, uploadInfo: async () => ({ state: 'Available', canAttach: true }), replace: async () => {} })
  const input = nodes(f.render()).find(n => n.type === 'input')!
  input.props.onChange({ target: { files: [{}], value: '' } }); f.ui.commit()
  input.props.onChange({ target: { files: [{}], value: '' } })
  assert.equal(uploads, 1)
  upload.resolve({ id: 'file' }); await tick(); f.ui.unmount()
})

test('Returning A to B to A while an old upload settles does not resurrect busy state', async () => {
  const upload = deferred<any>()
  const f = fixture({ upload: async () => upload.promise })
  const tree = f.render(); f.ui.commit(); nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } })
  f.render({ ...doc, header: { ...doc.header, id: 'B' } }); f.ui.commit()
  upload.resolve({ id: 'file' }); await tick()
  f.render(doc); f.ui.commit()
  assert.equal(nodes(f.render()).find(n => n.type === 'Button' && textContent(n).includes('Select PDF'))?.props.disabled, false)
  assert.equal(nodes(f.render()).find(n => n.type === 'Card')?.props['aria-busy'], false)
  f.ui.unmount()
})
test('A PDF awaiting scan cannot replace the document until a verified Available receipt arrives', async () => {
  let ready = false, replacements = 0
  const f = fixture({ upload: async () => ({ id: 'file-fixture' }), uploadInfo: async () => ({ state: ready ? 'Available' : 'PendingScan', canAttach: ready }), replace: async () => { replacements++ } })
  const tree = f.render(); f.ui.commit(); nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick()
  assert.equal(replacements, 0); assert.ok(textContent(f.render()).includes('scanning for security verification'))
  ready = true
  await f.advance()
  assert.equal(replacements, 1); assert.equal(f.refreshes(), 1)
  f.ui.unmount()
})

test('Pending attachment activation refreshes the document automatically after 423 becomes Ready', async () => {
  let inspections = 0, bytes = 0
  const f = fixture({ info: async () => { if (++inspections === 1) throw new ApiRequestError(423, 'Pending', undefined, 'PDF_UNAVAILABLE'); return { fileId: 'file', originalName: 'real.pdf', completion: { isComplete: true } } }, bytes: async () => { bytes++; return new Blob(['pdf']) } })
  f.render({ ...doc, pdfState: 'Pending' }); f.ui.commit(); await tick()
  assert.equal(inspections, 1); assert.equal(f.refreshes(), 0); assert.equal(bytes, 0)
  await f.advance()
  assert.equal(f.refreshes(), 1)
  f.render({ ...doc, pdfState: 'Ready' }); f.ui.commit(); await tick()
  assert.equal(f.urls.length, 1); assert.equal(bytes, 1)
  f.ui.unmount(); assert.equal(f.timers.size, 0)
})

test('Automatic scan monitoring stops at rejection without attaching or refreshing', async () => {
  let calls = 0, replacements = 0
  const f = fixture({ upload: async () => ({ id: 'file' }), uploadInfo: async () => ({ state: ++calls === 1 ? 'PendingScan' : 'Rejected', canAttach: false }), replace: async () => { replacements++ } })
  const tree = f.render(); f.ui.commit(); nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick(); await f.advance()
  assert.equal(calls, 2); assert.equal(replacements, 0); assert.equal(f.refreshes(), 0)
  assert.ok(textContent(f.render()).includes('rejected')); assert.equal(f.timers.size, 0)
  assert.equal(nodes(f.render()).find(n => n.type === 'Button' && textContent(n).includes('Select PDF'))?.props.disabled, false)
  f.ui.unmount()
})

test('Actual scanner terminal 404 releases the unattempted upload so another PDF can be selected', async () => {
  let calls = 0
  const f = fixture({ upload: async () => ({ id: 'file' }), uploadInfo: async () => {
    if (++calls === 1) return { state: 'PendingScan', canAttach: false }
    throw new ApiRequestError(404, 'File not found')
  } })
  const tree = f.render(); f.ui.commit(); nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick(); await f.advance()
  assert.equal(nodes(f.render()).find(n => n.type === 'Button' && textContent(n).includes('Select PDF'))?.props.disabled, false)
  assert.ok(!nodes(f.render()).some(n => n.type === 'Button' && textContent(n) === 'Retry with same request'))
  assert.equal(f.timers.size, 0); f.ui.unmount()
})

for (const change of ['unmount', 'document', 'session']) test(`Scan response after ${change} does not attach or refresh a different owner`, async () => {
  const scanned = deferred<any>(); let calls = 0, replaces = 0
  const f = fixture({ upload: async () => ({ id: 'file' }), uploadInfo: async () => ++calls === 1 ? { state: 'PendingScan', canAttach: false } : scanned.promise, replace: async () => { replaces++ } })
  const tree = f.render(); f.ui.commit()
  nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick(); await f.advance()
  assert.equal(calls, 2)
  if (change === 'unmount') f.ui.unmount()
  if (change === 'document') { f.render({ ...doc, header: { ...doc.header, id: 'another-document' } }); f.ui.commit() }
  if (change === 'session') f.expireSession()
  scanned.resolve({ state: 'Available', canAttach: true }); await tick()
  assert.equal(replaces, 0); assert.equal(f.refreshes(), 0); assert.equal(f.timers.size, 0)
  f.ui.unmount()
})

test('Pending activation does not retry permission errors or fetch bytes', async () => {
  let calls = 0
  const f = fixture({ info: async () => { calls++; throw new ApiRequestError(403, 'Denied') } })
  f.render({ ...doc, pdfState: 'Pending' }); f.ui.commit(); await tick()
  assert.equal(calls, 1); assert.equal(f.timers.size, 0); assert.equal(f.refreshes(), 0)
  assert.ok(textContent(f.render({ ...doc, pdfState: 'Pending' })).includes('Denied'))
  f.ui.unmount()
})

test('A stalled scan has a bounded wait and preserves the same operation for a manual retry', async () => {
  let uploads = 0, replaces = 0
  const f = fixture({ upload: async () => { uploads++; return { id: 'file' } }, uploadInfo: async () => ({ state: 'PendingScan', canAttach: false }), replace: async () => { replaces++ } })
  const tree = f.render(); f.ui.commit(); nodes(tree).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick()
  for (let i = 0; i < 65; i++) await f.advance()
  assert.equal(f.timers.size, 0); assert.equal(uploads, 1); assert.equal(replaces, 0)
  assert.ok(textContent(f.render()).includes('longer than expected'))
  f.ui.unmount()
})
test('PDF preview revokes its blob on document change and never publishes aborted bytes', async () => {
  const bytes = deferred<Blob>()
  const f = fixture({ info: async () => ({ fileId: 'file-fixture', originalName: 'sample.pdf', completion: { isComplete: true } }), bytes: () => bytes.promise })
  f.render({ ...doc, pdfState: 'Ready' }); f.ui.commit(); await tick()
  f.render({ ...doc, header: { ...doc.header, version: 8 }, pdfState: 'None' }); f.ui.commit()
  bytes.resolve(new Blob(['%PDF-fixture'])); await tick()
  assert.equal(f.urls.length, 0)
  f.render({ ...doc, header: { ...doc.header, version: 9 }, pdfState: 'Ready' }); f.ui.commit(); await tick()
  assert.equal(f.urls.length, 1)
  f.ui.unmount(); assert.deepEqual(f.revoked, f.urls)
})
test('Denied PDF metadata never causes a byte request or fabricated complete preview', async () => {
  let bytes = 0
  const f = fixture({ info: async () => { throw new ApiRequestError(403, 'Denied PDF') }, bytes: async () => { bytes++ } })
  f.render({ ...doc, pdfState: 'Ready' }); f.ui.commit(); await tick()
  const tree = f.render({ ...doc, pdfState: 'Ready' })
  assert.equal(bytes, 0); assert.equal(f.urls.length, 0)
  assert.ok(textContent(tree).includes('Denied PDF')); assert.ok(!textContent(tree).includes('Complete document dossier'))
  f.ui.unmount()
})
