import assert from 'node:assert/strict'
import { test } from 'node:test'
import { clearBrowserSessionData } from '../../src/services/browserSession'
const doc = 'd68a56e8-89ca-4ec5-bc6c-0e7f1d2a8ae7', user = '9b8242aa-4500-4f0a-a66d-1a44c951c9c4', correlation = '25730bbf-b7d8-4c1e-b394-194619775036'
async function api() { const module = await import('../../src/services/das/document-tasks').catch(() => null); assert.ok(module, 'Document task client must exist'); return module.documentTasksApi }
test('Task options verify document/actor scope and reject duplicate or foreign self entries', async () => {
  const client = await api(), previous = globalThis.fetch
  const options = { documentId: doc, userId: user, assignees: [{ userId: user, name: 'Tôi', departmentName: null, isSelf: true }] }
  try {
    globalThis.fetch = async (_, init) => { assert.equal(init?.cache, 'no-store'); assert.equal(init?.redirect, 'error'); return Response.json({ success: true, data: options }) }
    assert.deepEqual(await client.options(doc), options)
    for (const data of [{ ...options, documentId: correlation }, { ...options, assignees: [...options.assignees, ...options.assignees] }, { ...options, assignees: [{ ...options.assignees[0], userId: doc }] }]) { globalThis.fetch = async () => Response.json({ success: true, data }); await assert.rejects(client.options(doc)) }
  } finally { globalThis.fetch = previous }
})
test('Create freezes key/body and rejects malformed or inconsistent task receipts', async () => {
  const client = await api(), previous = globalThis.fetch, draft = { assigneeUserId: user, title: 'Task title' }
  try {
    globalThis.fetch = async (url, init) => { assert.match(String(url), /documents\/.+\/tasks$/); assert.equal(new Headers(init?.headers).get('Idempotency-Key'), 'fixed-request'); assert.deepEqual(JSON.parse(String(init?.body)), draft); return Response.json({ success: true, data: { correlationId: correlation, state: 'PendingConfiguration', taskId: null } }, { status: 202 }) }
    assert.equal((await client.create(doc, 'fixed-request', draft)).state, 'PendingConfiguration')
    for (const data of [{ correlationId: correlation, state: 'Linked', taskId: null }, { correlationId: correlation, state: 'UnknownOutcome', taskId: 'remote' }, { correlationId: 'bad', state: 'Preparing', taskId: null }, { correlationId: correlation, state: 'Sent', taskId: null }]) { globalThis.fetch = async () => Response.json({ success: true, data }); await assert.rejects(client.create(doc, 'fixed-request', draft)) }
  } finally { globalThis.fetch = previous }
})
test('Reconciliation never emits a create body/key and requires the same correlation', async () => {
  const client = await api(), previous = globalThis.fetch
  try {
    globalThis.fetch = async (url, init) => { assert.match(String(url), new RegExp(`/task-intents/${correlation}/reconcile$`)); assert.equal(init?.body, undefined); assert.equal(new Headers(init?.headers).get('Idempotency-Key'), null); return Response.json({ success: true, data: { correlationId: correlation, state: 'Linked', taskId: 'remote-task' } }) }
    assert.equal((await client.reconcile(correlation)).taskId, 'remote-task')
    globalThis.fetch = async () => Response.json({ success: true, data: { correlationId: doc, state: 'Linked', taskId: 'foreign' } }); await assert.rejects(client.reconcile(correlation))
  } finally { globalThis.fetch = previous }
})
test('History cannot mask pending requests or duplicate correlations', async () => {
  const client = await api(), previous = globalThis.fetch, item = { correlationId: correlation, assigneeUserId: user, title: 'Task', state: 'UnknownOutcome', taskId: null }
  try {
    globalThis.fetch = async () => Response.json({ success: true, data: { documentId: doc, items: [item], total: 1, hasPending: true } }); assert.equal((await client.history(doc)).hasPending, true)
    for (const data of [{ documentId: doc, items: [item], total: 1, hasPending: false }, { documentId: doc, items: [item, item], total: 2, hasPending: true }, { documentId: doc, items: [item], total: 0, hasPending: true }]) { globalThis.fetch = async () => Response.json({ success: true, data }); await assert.rejects(client.history(doc)) }
  } finally { globalThis.fetch = previous }
})
test('Invalid draft and identifiers fail before network or task side effects', async () => {
  const client = await api(), previous = globalThis.fetch
  globalThis.fetch = async () => { assert.fail('Invalid request must not reach the server') }
  try { await assert.rejects(client.create(doc, 'key', { assigneeUserId: 'bad', title: 'Title' })); await assert.rejects(client.create(doc, 'key', { assigneeUserId: user, title: ' '.repeat(10) })); await assert.rejects(client.retry('bad')) } finally { globalThis.fetch = previous }
})
test('Ambiguous request survives reload with the same frozen body and is actor/document scoped', async () => {
  const module = await import('../../src/services/das/task-session').catch(() => null); assert.ok(module, 'Frozen task session must exist')
  const values = new Map<string,string>(), storage = { getItem: (key:string) => values.get(key) ?? null, setItem: (key:string,value:string) => { values.set(key,value) }, removeItem: (key:string) => { values.delete(key) } }
  const pending = { key: 'frozen-key', draft: { assigneeUserId: user, title: 'Original' } }
  module.saveTaskRequest(storage, user, doc, pending); assert.deepEqual(module.loadTaskRequest(storage, user, doc), pending); assert.equal(module.loadTaskRequest(storage, correlation, doc), null)
  module.clearTaskRequest(storage, user, doc); assert.equal(module.loadTaskRequest(storage, user, doc), null)
  values.set(`das_task_request:${user}:${doc}`, '{bad json'); assert.throws(() => module.loadTaskRequest(storage, user, doc))
})
test('Logout clears frozen task bodies while preserving unrelated session preferences', () => {
  const values = new Map([['das_task_request:actor:doc','private task body'],['preference','keep']]), previousWindow = globalThis.window, previousLocal = globalThis.localStorage, previousSession = globalThis.sessionStorage
  const storage = { get length() { return values.size }, key: (i:number) => Array.from(values.keys())[i] ?? null, removeItem: (key:string) => { values.delete(key) } }
  Object.defineProperty(globalThis,'window',{value:{},configurable:true}); Object.defineProperty(globalThis,'localStorage',{value:{removeItem:()=>{}},configurable:true});Object.defineProperty(globalThis,'sessionStorage',{value:storage,configurable:true})
  try { clearBrowserSessionData(); assert.equal(values.has('das_task_request:actor:doc'),false);assert.equal(values.get('preference'),'keep') }
  finally { Object.defineProperty(globalThis,'window',{value:previousWindow,configurable:true});Object.defineProperty(globalThis,'localStorage',{value:previousLocal,configurable:true});Object.defineProperty(globalThis,'sessionStorage',{value:previousSession,configurable:true}) }
})
