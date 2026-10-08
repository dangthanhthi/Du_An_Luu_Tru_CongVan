import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred<T>() { let resolve!: (value: T) => void; const promise = new Promise<T>(r => { resolve = r }); return { promise, resolve } }
const actor = '11111111-1111-4111-8111-111111111111', department = '22222222-2222-4222-8222-222222222222'
const documentId = '33333333-3333-4333-8333-333333333333'
function saved(kind = 'Internal', status = 'InProgress', version = 7) {
  return { header: { id: documentId, kind, status, version, subject: 'Saved subject', registrationNumber: '27-01-0007/HL/ADM', registrationDate: '2027-01-01',
    companyCode: 'HL', sensitivity: 'Normal', allowedActions: ['Edit', 'Distribute', 'Cancel', 'Restore'] },
    originatorUserId: actor, ownerDepartmentId: department, inputterUserId: actor, lastModifierUserId: actor,
    recipients: [], relatedDocumentIds: [], details: null, remark: null, pdfState: 'None' }
}
function formFixture(kind = 'Internal', original?: any, register?: (...args: any[]) => Promise<any>) {
  const calls: any[] = [], routes: string[] = []
  let uuid = 0
  const api = {
    detail: async () => original,
    options: async () => ({ userId: actor, canRegister: true, targets: [{ originatorUserId: actor, departmentId: department, isPrimary: true, originatorName: 'Fixture user', departmentName: 'ADM' }],
      catalogs: [{ group: 'companies', code: 'HL', name: 'Company fixture' }], distributionTargets: [] }),
    register: async (...args: any[]) => { calls.push({ mode: 'register', body: args[0], key: args[1] }); return register ? register(...args) : { id: documentId, registrationNumber: 'server-only-number' } },
    edit: async (...args: any[]) => { calls.push({ mode: 'edit', id: args[0], body: args[1] }); return { id: documentId } }
  }
  const ui = componentHarness(new URL('../../src/views/apps/documents/V2DocumentForm.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    'next/navigation': { useRouter: () => ({ push: (url: string) => routes.push(url), back() {} }), useParams: () => ({ lang: 'en' }),
      useSearchParams: () => ({ get: () => kind, getAll: () => [kind] }) },
    '@/services/das/documents': { documentsV2Api: api }, '@/services/api': { ApiRequestError },
    './V2ReferencePicker': { default: 'ReferencePicker' }
  }, { crypto: { randomUUID: () => `request-${++uuid}` }, window: { dispatchEvent() {} }, Event })
  const render = () => ui.render(original ? { id: documentId } : {})
  const set = (label: string, value: string) => {
    const field = nodes(render()).find(n => n.props.label === label)
    assert.ok(field, `Missing ${label}`)
    field.props.onChange({ target: { value } })
  }
  const submit = () => nodes(render()).find(n => n.type === 'form')!.props.onSubmit({ preventDefault() {} })
  render(); ui.commit()
  return { ui, render, set, submit, calls, routes }
}
for (const kind of ['Incoming', 'Outgoing', 'Internal']) test(`${kind} form submits selected identities and never allocates registration number or date`, async () => {
  const f = formFixture(kind); await tick(); f.set('Subject', 'Fixture subject')
  if (kind === 'Incoming') {
    f.set('Receiving Date', '2026-10-07'); f.set('Method', 'EMAIL')
    nodes(f.render()).find(n => n.props.label === 'Sender Entity')!.props.onChange([{ id: 'sender-fixture', name: 'Sender fixture' }])
  }
  if (kind === 'Outgoing') nodes(f.render()).find(n => n.props.label === 'Recipient Entities')!.props.onChange([{ id: 'recipient-fixture', name: 'Recipient fixture' }])
  await f.submit()
  assert.equal(f.calls.length, 1)
  assert.equal(f.calls[0].body.kind, kind); assert.equal(f.calls[0].body.subject, 'Fixture subject')
  assert.equal(f.calls[0].body.ownerDepartmentId, department); assert.equal(f.calls[0].body.originatorUserId, actor)
  assert.equal(f.calls[0].body.registrationNumber, undefined); assert.equal(f.calls[0].body.registrationDate, undefined)
  assert.deepEqual(f.routes, [`/en/apps/documents/${documentId}`])
  f.ui.unmount()
})
test('Incoming missing sender stops submission and exposes the picker error', async () => {
  const f = formFixture('Incoming'); await tick(); f.set('Subject', 'Fixture subject'); await f.submit()
  assert.equal(f.calls.length, 0)
  assert.ok(textContent(f.render()).includes('Sender entity is required'))
  assert.equal(nodes(f.render()).find(n => n.props.label === 'Sender Entity')!.props.error, true)
  f.ui.unmount()
})
test('Ambiguous registration retry preserves request key and body, without a false navigation', async () => {
  let attempts = 0
  const f = formFixture('Internal', undefined, async () => { if (++attempts === 1) throw new ApiRequestError(0, 'Lost response'); return { id: documentId } })
  await tick(); f.set('Subject', 'Fixture subject'); await f.submit()
  assert.equal(f.routes.length, 0)
  assert.equal(nodes(f.render()).find(n => n.props.label === 'Subject')!.props.disabled, true)
  await f.submit()
  assert.equal(f.calls.length, 2); assert.equal(f.calls[1].key, f.calls[0].key)
  assert.deepEqual(f.calls[1].body, f.calls[0].body); assert.equal(f.routes.length, 1)
  f.ui.unmount()
})
test('Editing uses the saved aggregate version and does not supply immutable date or sequence', async () => {
  const f = formFixture('Internal', saved()); await tick(); f.set('Subject', 'Edited fixture'); f.set('Company', 'HV'); await f.submit()
  assert.equal(f.calls[0].mode, 'edit'); assert.equal(f.calls[0].body.expectedVersion, 7)
  assert.equal(f.calls[0].body.companyCode, 'HV'); assert.equal(f.calls[0].body.registrationDate, undefined)
  assert.equal(f.calls[0].body.sequenceNumber, undefined); assert.ok(textContent(f.render()).includes('27-01-0007/HL/ADM'))
  f.ui.unmount()
})
test('Two submit events before a render dispatch only one registration request', async () => {
  const reply = deferred<any>(), f = formFixture('Internal', undefined, () => reply.promise)
  await tick(); f.set('Subject', 'Fixture subject')
  const handler = nodes(f.render()).find(n => n.type === 'form')!.props.onSubmit
  const first = handler({ preventDefault() {} }), second = handler({ preventDefault() {} })
  try { assert.equal(f.calls.length, 1, 'In-flight dispatch must lock before React rerenders') }
  finally { reply.resolve({ id: documentId }); await Promise.all([first, second]); f.ui.unmount() }
})

function detailFixture(initial = saved(), failure?: ApiRequestError) {
  let data = initial
  const mutations: any[][] = []
  const ui = componentHarness(new URL('../../src/views/apps/documents/detail/index.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    'next/navigation': { useParams: () => ({ lang: 'en' }) }, 'next/link': { default: 'Link' },
    '@/services/api': { ApiRequestError }, '../V2PdfPanel': { default: 'PdfPanel' }, '../DocumentTaskPanel': { default: 'TaskPanel' },
    '@/services/das/documents': { documentsV2Api: {
      detail: async () => data,
      status: async (...args: any[]) => {
        mutations.push(args.slice(0, 4)); if (failure) throw failure
        data = { ...data, header: { ...data.header, version: data.header.version + 1, status: args[2] === 'Cancel' ? 'Cancelled' : 'Distributed' } }
      }
    } }
  }, { window: { dispatchEvent() {} }, Event })
  const render = () => ui.render({ id: documentId })
  const button = (label: string) => {
    const node = nodes(render()).find(n => n.type === 'Button' && textContent(n) === label)
    assert.ok(node, `Missing ${label}`); return node
  }
  render(); ui.commit()
  return { ui, render, button, mutations }
}
test('Detail distribute, cancel and restore use each authoritative reloaded version', async () => {
  const f = detailFixture(); await tick()
  await f.button('Distribute').props.onClick(); f.render(); f.ui.commit(); await tick()
  f.button('Cancel Document').props.onClick()
  nodes(f.render()).find(n => n.props.label === 'Cancellation reason')!.props.onChange({ target: { value: 'Fixture reason' } })
  await f.button('Confirm Cancel').props.onClick(); f.render(); f.ui.commit(); await tick()
  await f.button('Restore').props.onClick(); f.render(); f.ui.commit(); await tick()
  assert.deepEqual(f.mutations, [[documentId, 7, 'Distribute', undefined], [documentId, 8, 'Cancel', 'Fixture reason'], [documentId, 9, 'Restore', undefined]])
  f.ui.unmount()
})
test('Cancellation failure stays inside the open dialog and excessive reason disables confirmation', async () => {
  const f = detailFixture(saved(), new ApiRequestError(409, 'Changed')); await tick()
  f.button('Cancel Document').props.onClick()
  nodes(f.render()).find(n => n.props.label === 'Cancellation reason')!.props.onChange({ target: { value: 'Fixture reason' } })
  await f.button('Confirm Cancel').props.onClick()
  const dialog = nodes(f.render()).find(n => n.type === 'Dialog')!
  assert.equal(dialog.props.open, true); assert.ok(textContent(dialog).includes('modified'))
  nodes(f.render()).find(n => n.props.label === 'Cancellation reason')!.props.onChange({ target: { value: 'x'.repeat(4001) } })
  assert.equal(f.button('Confirm Cancel').props.disabled, true)
  assert.equal(f.mutations.length, 1); f.ui.unmount()
})
for (const kind of ['Incoming', 'Outgoing', 'Internal']) test(`${kind} detail shows saved catalog labels rather than losing them to raw catalog codes`, async () => {
  const data: any = saved(kind)
  data.details = { methodCode: 'EMAIL', methodNameSnapshot: 'Historical delivery name', documentTypeCode: 'LETTER', documentTypeNameSnapshot: 'Historical type name', categoryCode: 'OLD', categoryNameSnapshot: 'Historical category name' }
  const f = detailFixture(data); await tick()
  const text = textContent(f.render())
  if (kind !== 'Internal') assert.ok(text.includes('Historical delivery name'))
  assert.ok(text.includes('Historical type name')); assert.ok(text.includes('Historical category name'))
  f.ui.unmount()
})
