import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { webcrypto } from 'node:crypto'
import { runInNewContext } from 'node:vm'
import { test } from 'node:test'
import ts from 'typescript'
import { apiContext } from './helpers/api-context'
import { FixtureLocks, MemoryStorage, seedSession } from './helpers/session-fixture'

for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) test(`${method} of an old rendered intent is rejected before sending under the replacement session`, async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks(), old = seedSession(storage)
  let calls = 0
  const client = apiContext(storage, locks, async () => { calls++; return Response.json({ success: true, data: 'unexpected' }) })
  seedSession(storage, 'new-access', 'new-refresh') // No storage event: reproduce the pre-remount interval.
  const sessionIntent = { epoch: old.epoch, assertCurrent: () => {} }
  await assert.rejects(client.requestApiEnvelope('document', '/fixture/old-body', { method, body: '{}', sessionIntent }), (error: unknown) => (error as { status: number }).status === 409)
  assert.equal(calls, 0)
  assert.equal(client.tokenManager.getToken(), 'new-access')
})

test('the actual old task send handler rejects before frozen body persistence or POST without waiting for storage event', async () => {
  const storage = new MemoryStorage(), drafts = new MemoryStorage(), locks = new FixtureLocks(), old = seedSession(storage)
  const actor = '9b8242aa-4500-4f0a-a66d-1a44c951c9c4', documentId = 'd68a56e8-89ca-4ec5-bc6c-0e7f1d2a8ae7'
  let calls = 0
  const fetch: typeof globalThis.fetch = async () => { calls++; return Response.json({ success: true, data: { correlationId: '25730bbf-b7d8-4c1e-b394-194619775036', state: 'Linked', taskId: 'fixture-task' } }) }
  const client = apiContext(storage, locks, fetch)
  const taskModule = apiContext(storage, locks, fetch, {}, 'das/document-tasks') as unknown as typeof import('../../src/services/das/document-tasks')
  const frozen = await import('../../src/services/das/task-session')
  const path = new URL('../../src/views/apps/documents/DocumentTaskPanel.tsx', import.meta.url)
  const source = ts.createSourceFile(path.pathname, readFileSync(path, 'utf8'), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
  let handler: ts.FunctionDeclaration | undefined
  const visit = (node: ts.Node) => { if (ts.isFunctionDeclaration(node) && node.name?.text === 'send') handler = node; ts.forEachChild(node, visit) }
  visit(source); assert.ok(handler, 'Production send handler must be present')
  const code = ts.transpileModule(`exports.send = ${handler.getText(source)}`, { compilerOptions: { target: ts.ScriptTarget.ES2022 } }).outputText
  const exports: { send?: () => Promise<void> } = {}
  let notice = ''
  const sessionIntent = { epoch: old.epoch, assertCurrent: () => { if (client.tokenManager.getEpoch() !== old.epoch) throw new client.ApiRequestError(409, 'Fixture old intent') } }
  runInNewContext(code, { exports, sessionIntent, inFlight: { current: false }, loading: false, options: { userId: actor }, history: { hasPending: false }, error: '', pending: null,
    mounted: { current: true }, assignee: actor, title: 'Private old task', documentId, crypto: webcrypto, window: { sessionStorage: drafts },
    setBusy: () => {}, setError: (message: string) => { notice = message }, setPending: () => {}, setReceipt: () => {}, setTitle: () => {},
    ...frozen, documentTasksApi: taskModule.documentTasksApi, load: async () => {}, errorMessage: (error: Error) => error.message })
  seedSession(storage, 'new-access', 'new-refresh')
  await exports.send!()
  assert.equal(calls, 0)
  assert.equal(drafts.length, 0)
  assert.match(notice, /old intent/)
})

test('all live DAS mutation call sites pass the intent captured by their mounted component', () => {
  const entries: Record<string, Record<string, string[]>> = {
    'views/apps/documents/DocumentTaskPanel.tsx': { documentTasksApi: ['create', 'retry', 'reconcile'] },
    'views/apps/documents/V2DocumentForm.tsx': { documentsV2Api: ['register', 'edit'] },
    'views/apps/documents/V2PdfPanel.tsx': { documentPdfApi: ['upload', 'replace'] },
    'views/apps/documents/detail/index.tsx': { documentsV2Api: ['status'] },
    'views/apps/partners/list/AddPartnerDrawer.tsx': { externalEntityApi: ['create', 'update'] },
    'views/apps/partners/list/PartnerListTable.tsx': { externalEntityApi: ['changeDeletion'] },
    'views/apps/settings/BusinessCatalogs.tsx': { catalogApi: ['create', 'update'] },
    'components/layout/shared/DasNotificationsDropdown.tsx': { notificationsApi: ['read', 'readAll'] }
  }
  let total = 0
  for (const [path, services] of Object.entries(entries)) {
    const source = ts.createSourceFile(path, readFileSync(new URL('../../src/' + path, import.meta.url), 'utf8'), ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX)
    const seen = new Set<string>()
    const visit = (node: ts.Node) => {
      if (ts.isCallExpression(node) && ts.isPropertyAccessExpression(node.expression) && ts.isIdentifier(node.expression.expression)) {
        const receiver = node.expression.expression.text, method = node.expression.name.text
        if (services[receiver]?.includes(method)) {
          assert.equal(node.arguments.at(-1)?.getText(source), 'sessionIntent', `${path}: ${receiver}.${method} must preserve its rendered owner`)
          seen.add(receiver + '.' + method); total++
        }
      }
      ts.forEachChild(node, visit)
    }
    visit(source)
    for (const [receiver, methods] of Object.entries(services)) for (const method of methods) assert.ok(seen.has(receiver + '.' + method))
    assert.match(source.text, /const sessionIntent = useSessionIntent\(\)/)
  }
  assert.equal(total, 15)
})

test('a captured intent survives same-actor rotation, is never put into fetch options and is unusable after logout', async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  const client = apiContext(storage, locks, async (_input, options) => {
    assert.ok(!Object.hasOwn(options!, 'sessionIntent'))
    return Response.json({ success: true, data: 'accepted' })
  })
  const owner = client.captureSessionIntent()
  const record = JSON.parse(storage.getItem('das_session_v1')!)
  storage.setItem('das_session_v1', JSON.stringify({ ...record, revision: 1, accessToken: 'rotated-access', refreshToken: 'rotated-refresh' }))
  owner.assertCurrent()
  assert.equal((await client.requestApiEnvelope('document', '/fixture/current', { method: 'POST', sessionIntent: owner })).data, 'accepted')
  await client.authApi.logout()
  assert.throws(owner.assertCurrent, (error: unknown) => (error as { status: number }).status === 409)
})

test('a newly mounted descendant inherits its old rendered boundary owner instead of recapturing the replacement', async () => {
  const storage = new MemoryStorage(), locks = new FixtureLocks()
  seedSession(storage)
  let calls = 0
  const client = apiContext(storage, locks, async () => { calls++; return Response.json({ success: true, data: 'unexpected' }) })
  const renderedOwner = client.captureSessionIntent()
  seedSession(storage, 'new-access', 'new-refresh')
  const source = readFileSync(new URL('../../src/hooks/useSessionIntent.ts', import.meta.url), 'utf8')
  const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText
  const exports: { useSessionIntent?: () => ReturnType<typeof client.captureSessionIntent> } = {}
  runInNewContext(code, { exports, require: (name: string) => {
    if (name === 'react') return { useContext: () => renderedOwner, useState: (initializer: () => unknown) => [initializer()] }
    if (name === '@/services/api') return client
    if (name === '@/contexts/sessionIntentContext') return { SessionIntentContext: {} }
    assert.fail('Unexpected hook import ' + name)
  } })
  const childOwner = exports.useSessionIntent!()
  assert.equal(childOwner.epoch, renderedOwner.epoch)
  await assert.rejects(client.requestApiEnvelope('partner', '/fixture/old-row', { method: 'PUT', body: '{}', sessionIntent: childOwner }), (error: unknown) => (error as { status: number }).status === 409)
  assert.equal(calls, 0)
})
