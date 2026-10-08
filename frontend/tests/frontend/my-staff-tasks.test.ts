import assert from 'node:assert/strict'
import { test } from 'node:test'
import * as api from '../../src/services/das/reports-staff'

const alice = { userId: '11111111-1111-4111-8111-111111111111', name: 'Alice', departmentId: '33333333-3333-4333-8333-333333333333', departmentName: 'ADM' }
const task = { taskId: 'opaque/task', assignee: alice, title: '<script>long title</script>', status: 'Unknown', dueAt: null }
const result = { taskState: 'Connected', selectedAssignee: alice, tasks: { items: [task], total: 1, pageNumber: 1, pageSize: 20 } }
const tasks = (filter: any = {}, signal?: AbortSignal) => { assert.equal(typeof api.getMyStaffTasks, 'function', 'Independent task accessor must exist'); return api.getMyStaffTasks(filter, signal) }
async function withResponse(value: unknown, work: () => Promise<void>) {
  const previous = globalThis.fetch
  globalThis.fetch = async () => Response.json({ success: true, data: value })
  try { await work() } finally { globalThis.fetch = previous }
}
test('Independent accessors send assignee only to tasks and preserve legacy includeTasks=true', async () => {
  const previous = globalThis.fetch, urls: string[] = [], signal = new AbortController().signal
  globalThis.fetch = async (url, options) => {
    urls.push(String(url)); assert.equal(options?.cache, 'no-store'); assert.equal(options?.redirect, 'error'); assert.equal(options?.signal, signal)
    return Response.json({ success: true, data: String(url).includes('/tasks?') ? result : { staff: [alice], total: 1, pageNumber: 1, pageSize: 20, taskState: 'NotRequested', tasks: null } })
  }
  try {
    assert.equal(typeof api.getMyStaffMembers, 'function'); await api.getMyStaffMembers(1, 20, signal)
    await tasks({ assigneeUserId: alice.userId }, signal); await api.getMyStaff(1, 20, signal)
    assert.match(urls[0], /includeTasks=false/); assert.doesNotMatch(urls[0], /assignee/)
    assert.match(urls[1], /\/my-staff\/tasks\?pageNumber=1&pageSize=20&assigneeUserId=11111111/)
    assert.match(urls[2], /includeTasks=true/)
  } finally { globalThis.fetch = previous }
})
test('Task filter bounds reject invalid assignee and pages before any fetch', async () => {
  const previous = globalThis.fetch; let called = false
  globalThis.fetch = async () => { called = true; throw new Error('must not fetch') }
  try {
    for (const filter of [{ assigneeUserId: '' }, { assigneeUserId: '00000000-0000-0000-0000-000000000000' }, { assigneeUserId: 'bad' }, { pageNumber: 0 }, { pageSize: 101 }]) await assert.rejects(tasks(filter))
    assert.equal(called, false)
  } finally { globalThis.fetch = previous }
})
test('Task decoder accepts verified nested names, Connected empty and unknown degraded count', async () => {
  await withResponse(result, async () => assert.deepEqual(await tasks({ assigneeUserId: alice.userId }), result))
  const empty = { taskState: 'Connected', selectedAssignee: null, tasks: { items: [], total: 0, pageNumber: 1, pageSize: 20 } }
  await withResponse(empty, async () => assert.deepEqual(await tasks(), empty))
  for (const taskState of ['TMS_TIMEOUT', 'TMS_UNAVAILABLE', 'TMS_CONTRACT_INVALID']) await withResponse({ taskState, selectedAssignee: alice, tasks: null }, async () => assert.equal((await tasks({ assigneeUserId: alice.userId })).tasks, null))
})
test('Task decoder rejects selected identity, page and entire malformed item payloads', async () => {
  const brokenItems = [null, { ...task, assignee: null }, { ...task, assignee: { ...alice, name: '' } }, { ...task, assignee: { ...alice, userId: 'bad' } }, { ...task, taskId: 'x'.repeat(201) }, { ...task, title: 'x'.repeat(2001) }, { ...task, status: 'x'.repeat(101) }, { ...task, dueAt: '2026-10-08' }, { ...task, dueAt: '2026-02-30T00:00:00Z' }, { ...task, assignee: { ...alice, name: 'x'.repeat(201) } }, { ...task, extra: true }]
  const broken = [null, { ...result, selectedAssignee: null }, { ...result, selectedAssignee: { ...alice, userId: '22222222-2222-4222-8222-222222222222' } }, { ...result, tasks: null }, { ...result, taskState: 'Unavailable' }, { ...result, extra: true }, ...brokenItems.map(item => ({ ...result, tasks: { ...result.tasks, items: [item] } })), ...[{ total: -1 }, { total: 0 }, { pageNumber: 2 }, { pageSize: 10 }, { items: [task, task], total: 2 }, { total: 1, items: [task, { ...task, taskId: 'other' }] }].map(page => ({ ...result, tasks: { ...result.tasks, ...page } }))]
  for (const value of broken) await withResponse(value, async () => assert.rejects(tasks({ assigneeUserId: alice.userId })))
  await withResponse(result, async () => assert.rejects(tasks()))
  await withResponse({ ...result, tasks: { items: [task], total: 20, pageNumber: 2, pageSize: 20 } }, async () => assert.rejects(tasks({ pageNumber: 2, assigneeUserId: alice.userId })))
  await withResponse({ ...result, tasks: { items: [task, { ...task, taskId: 'two' }], total: 21, pageNumber: 2, pageSize: 20 } }, async () => assert.rejects(tasks({ pageNumber: 2, assigneeUserId: alice.userId })))
})
test('Task decoder rejects a foreign valid assignee and contradictory verified names', async () => {
  for (const assignee of [{ ...alice, userId: '22222222-2222-4222-8222-222222222222' }, { ...alice, name: 'Different person name' }, { ...alice, departmentName: 'Wrong department' }]) await withResponse({ ...result, tasks: { ...result.tasks, items: [{ ...task, assignee }] } }, async () => assert.rejects(tasks({ assigneeUserId: alice.userId })))
})
test('Staff-only decoder rejects task leakage, duplicate members and impossible pages', async () => {
  const value = { staff: [alice], total: 1, pageNumber: 1, pageSize: 20, taskState: 'NotRequested', tasks: null }
  for (const broken of [{ ...value, tasks: result.tasks }, { ...value, staff: [alice,alice], total: 2 }, { ...value, staff: [null] }, { ...value, staff: [{ ...alice, name: '' }] }, { ...value, total: 0 }, { ...value, pageNumber: 2 }]) await withResponse(broken, async () => assert.rejects(api.getMyStaffMembers()))
})
test('Sparse and past-end empty pages preserve positive server total', async () => {
  for (const pageNumber of [1, 2]) await withResponse({ taskState: 'Connected', selectedAssignee: null, tasks: { items: [], total: 1, pageNumber, pageSize: 20 } }, async () => assert.equal((await tasks({ pageNumber })).tasks?.total, 1))
})
