import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(r => { resolve = r }); return { promise, resolve } }
const doc = { header: { id: 'document-fixture', version: 7, registrationNumber: '27-01-0007/HL/ADM', allowedActions: ['ReplacePdf'] }, pdfState: 'None' }
function fixture(api: any) {
  const urls: string[] = [], revoked: string[] = []
  let refreshes = 0, uuid = 0
  const ui = componentHarness(new URL('../../src/views/apps/documents/V2PdfPanel.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/das/document-pdf': { documentPdfApi: api },
    '@/components/DocumentPDFPreview': { default: 'PdfPreview' }
  }, { Error, crypto: { randomUUID: () => `operation-${++uuid}` },
    URL: { createObjectURL() { const url = `blob:fixture-${urls.length}`; urls.push(url); return url }, revokeObjectURL(url: string) { revoked.push(url) } } })
  const render = (document: any = doc) => ui.render({ document, onChange: () => { refreshes++ } })
  return { ui, render, urls, revoked, refreshes: () => refreshes }
}
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
test('A PDF awaiting scan cannot replace the document until a verified Available receipt arrives', async () => {
  let ready = false, replacements = 0
  const f = fixture({ upload: async () => ({ id: 'file-fixture' }), uploadInfo: async () => ({ state: ready ? 'Available' : 'PendingScan', canAttach: ready }), replace: async () => { replacements++ } })
  nodes(f.render()).find(n => n.type === 'input')!.props.onChange({ target: { files: [{}], value: '' } }); await tick()
  assert.equal(replacements, 0); assert.ok(textContent(f.render()).includes('scanning for security verification'))
  ready = true
  await nodes(f.render()).find(n => n.type === 'Button' && textContent(n) === 'Retry with same request')!.props.onClick()
  assert.equal(replacements, 1); assert.equal(f.refreshes(), 1)
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
