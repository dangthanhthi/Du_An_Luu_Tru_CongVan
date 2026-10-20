import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
import { clearTaskRequest, loadTaskRequest, saveTaskRequest } from '../../src/services/das/task-session'

const documentId = 'd68a56e8-89ca-4ec5-bc6c-0e7f1d2a8ae7'
const actor = '9b8242aa-4500-4f0a-a66d-1a44c951c9c4'
const correlation = '25730bbf-b7d8-4c1e-b394-194619775036'
const tick = () => new Promise(resolve => setImmediate(resolve))
function deferred() {
  let resolve!: (value: any) => void
  const promise = new Promise<any>(done => { resolve = done })
  return { promise, resolve }
}
function fixture(create: (...args: any[]) => Promise<any>, isEn = true) {
  const data = new Map<string, string>(), calls: any[][] = []
  const storage = { getItem: (key: string) => data.get(key) ?? null, setItem: (key: string, value: string) => { data.set(key, value) }, removeItem: (key: string) => { data.delete(key) } }
  let items: any[] = []
  const ui = componentHarness(new URL('../../src/views/apps/documents/DocumentTaskPanel.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn }) },
    '@/services/das/task-session': { clearTaskRequest, loadTaskRequest, saveTaskRequest },
    '@/services/das/document-tasks': {
      isPendingTask: (state: string) => ['PendingConfiguration', 'Preparing', 'UnknownOutcome'].includes(state),
      documentTasksApi: {
        options: async () => ({ documentId, userId: actor, assignees: [{ userId: actor, name: 'Self', departmentName: null, isSelf: true }] }),
        history: async () => ({ documentId, items, total: items.length, hasPending: items.some(x => ['PendingConfiguration', 'Preparing', 'UnknownOutcome'].includes(x.state)) }),
        create: async (...args: any[]) => {
          calls.push(args)
          const result = await create(...args)
          items = [{ ...result, ...args[2] }]
          return result
        }
      }
    }
  }, { window: { sessionStorage: storage }, crypto: { randomUUID: () => 'fixture-key' } })
  const render = () => ui.render({ documentId })
  const button = (label: string) => nodes(render()).find(n => n.type === 'Button' && textContent(n) === label)!
  render(); ui.commit()
  return { ui, render, button, calls, storage }
}

test('Task panel explains that integration request state is separate from task business status', async () => {
  const f = fixture(async () => ({})); await tick()
  try { assert.match(textContent(f.render()), /request status.*task.*status/i) } finally { f.ui.unmount() }
})

test('Synchronous duplicate guard keeps one frozen request while showing sending feedback', async () => {
  const reply = deferred(), f = fixture(() => reply.promise); await tick()
  try {
    nodes(f.render()).find(n => n.props.label === 'Task Title')!.props.onChange({ target: { value: 'Test assignment' } })
    const send = f.button('Create Task').props.onClick
    send(); send(); await tick()
    assert.equal(f.calls.length, 1)
    assert.match(textContent(f.render()), /Sending request/)
    assert.equal(f.calls[0][1], 'fixture-key')
    assert.equal(loadTaskRequest(f.storage, actor, documentId)?.draft.title, 'Test assignment')
  } finally {
    reply.resolve({ correlationId: correlation, state: 'PendingConfiguration', taskId: null }); await tick(); f.ui.unmount()
  }
})

for (const [state, severity, phrase] of [
  ['PendingConfiguration', 'info', /configuration or user mapping/i],
  ['UnknownOutcome', 'warning', /outcome unknown/i],
  ['Rejected', 'warning', /rejected/i],
  ['Linked', 'success', /task created/i]
] as const) test(`Receipt ${state} has truthful severity and cannot imply business progress`, async () => {
  const f = fixture(async () => ({ correlationId: correlation, state, taskId: state === 'Linked' ? 'remote-task' : null })); await tick()
  try {
    nodes(f.render()).find(n => n.props.label === 'Task Title')!.props.onChange({ target: { value: 'Test assignment' } })
    f.button('Create Task').props.onClick(); await tick()
    const feedback = nodes(f.render()).find(n => n.type === 'Alert' && phrase.test(textContent(n)))
    assert.ok(feedback)
    assert.equal(feedback.props.severity, severity)
    assert.equal(nodes(f.render()).filter(n => n.type === 'Alert' && n.props.severity === 'success').length, state === 'Linked' ? 1 : 0)
    assert.equal(loadTaskRequest(f.storage, actor, documentId), null)
  } finally { f.ui.unmount() }
})

test('Lost DAS reply retains key and body and retry sends the same request rather than a new task', async () => {
  let attempts = 0
  const f = fixture(async () => { if (++attempts === 1) throw new Error('Network unavailable'); return { correlationId: correlation, state: 'PendingConfiguration', taskId: null } }); await tick()
  try {
    nodes(f.render()).find(n => n.props.label === 'Task Title')!.props.onChange({ target: { value: 'Frozen title' } })
    f.button('Create Task').props.onClick(); await tick()
    assert.equal(loadTaskRequest(f.storage, actor, documentId)?.key, 'fixture-key')
    f.button('Reload Task Requests').props.onClick(); await tick()
    f.button('Check In-flight Request').props.onClick(); await tick()
    assert.equal(f.calls.length, 2)
    assert.equal(f.calls[0][1], f.calls[1][1])
    // Drafts cross the component VM realm and session JSON; compare wire values.
    assert.equal(JSON.stringify(f.calls[0][2]), JSON.stringify(f.calls[1][2]))
  } finally { f.ui.unmount() }
})
