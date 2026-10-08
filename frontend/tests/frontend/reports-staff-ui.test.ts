import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent, type Node } from './helpers/component-harness'

// Execute actual component callbacks. API responses and hooks are fixtures;
// these checks do not claim DOM/browser or real TMS/authority verification.
const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() {
  let resolve!: (value: T) => void, reject!: (reason: unknown) => void
  const promise = new Promise<T>((a, b) => { resolve = a; reject = b })
  return { promise, resolve, reject }
}
function button(tree: Node, label: string) {
  const node = nodes(tree).find(n => n.type === 'Button' && textContent(n) === label)
  assert.ok(node, `Missing ${label} button`)
  return node
}
const alice = { userId: '11111111-1111-4111-8111-111111111111', name: 'Alice fixture', departmentId: '33333333-3333-4333-8333-333333333333', departmentName: 'ADM' }
const bob = { userId: '22222222-2222-4222-8222-222222222222', name: 'Bob fixture', departmentId: '44444444-4444-4444-8444-444444444444', departmentName: 'IT' }
const report = { items: [], total: 40, pageNumber: 1, pageSize: 20, groups: [{ departmentId: alice.departmentId, departmentName: 'ADM', count: 40 }], canExport: true, evaluatedAt: '2026-10-07T01:00:00Z' }
function reportFixture() {
  const requests: { filter: any; signal: AbortSignal; reply: ReturnType<typeof deferred<any>> }[] = []
  const exports: { filter: any; signal: AbortSignal; reply: ReturnType<typeof deferred<Blob>> }[] = []
  const downloads: any[] = []
  const ui = componentHarness(new URL('../../src/views/apps/reports/IncompleteReport.tsx', import.meta.url), {
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/api': { ApiRequestError }, 'next/link': { default: 'Link' },
    'next/navigation': { useParams: () => ({ lang: 'en' }) },
    '@/services/das/reports-staff': { incompleteReportsApi: {
      list(filter: any, signal: AbortSignal) { const r = { filter: structuredClone(filter), signal, reply: deferred<any>() }; requests.push(r); return r.reply.promise },
      export(filter: any, signal: AbortSignal) { const r = { filter: structuredClone(filter), signal, reply: deferred<Blob>() }; exports.push(r); return r.reply.promise }
    } }
  }, { document: { createElement: () => ({ click() { downloads.push(this) } }) },
    URL: { createObjectURL: () => 'blob:synthetic-report', revokeObjectURL() {} } })
  ui.render(); ui.commit()
  return { ui, requests, exports, downloads }
}
test('Report ignores an old filter response and resets pagination when kind changes', async () => {
  const f = reportFixture()
  f.requests[0].reply.resolve(report); await tick()
  button(f.ui.render(), 'Next').props.onClick(); f.ui.render(); f.ui.commit()
  assert.equal(f.requests[1].filter.pageNumber, 2)
  nodes(f.ui.render()).find(n => n.props.label === 'Document Kind')!.props.onChange({ target: { value: 'Outgoing' } })
  f.ui.render(); f.ui.commit()
  assert.equal(f.requests[1].signal.aborted, true)
  assert.equal(f.requests[2].filter.kind, 'Outgoing'); assert.equal(f.requests[2].filter.pageNumber, 1)
  f.requests[2].reply.resolve({ ...report, total: 0, groups: [] }); await tick()
  f.requests[1].reply.resolve({ ...report, pageNumber: 2, total: 999, groups: [{ ...report.groups[0], count: 999 }] }); await tick()
  const text = textContent(f.ui.render()).replace(/\s+/g, ' ')
  assert.ok(text.includes('0 dossiers')); assert.ok(!text.includes('999 dossiers'))
  f.ui.unmount()
})
test('Report cancellation stops the export download while retaining the captured filter', async () => {
  const f = reportFixture(); f.requests[0].reply.resolve(report); await tick()
  const pending = button(f.ui.render(), 'Export Excel').props.onClick()
  assert.equal(f.exports.length, 1); assert.equal(f.exports[0].filter.includeRecent, false)
  button(f.ui.render(), 'Cancel').props.onClick()
  assert.equal(f.exports[0].signal.aborted, true)
  f.exports[0].reply.resolve(new Blob(['synthetic-workbook'])); await pending
  assert.equal(f.downloads.length, 0)
  assert.equal(button(f.ui.render(), 'Export Excel').props.disabled, false)
  f.ui.unmount()
})
test('Report export never shows success or creates a download after a revoked export grant', async () => {
  const f = reportFixture(); f.requests[0].reply.resolve(report); await tick()
  const pending = button(f.ui.render(), 'Export Excel').props.onClick()
  f.exports[0].reply.reject(new ApiRequestError(403, 'Denied')); await pending
  assert.equal(f.downloads.length, 0)
  assert.ok(textContent(f.ui.render()).includes('do not have permission to export'))
  f.ui.unmount()
})
