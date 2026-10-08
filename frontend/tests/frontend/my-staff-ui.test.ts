import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { translations } from '../../src/hooks/useDictionary'
import { getMyStaffMembers, getMyStaffTasks } from '../../src/services/das/reports-staff'
import { componentHarness, nodes, textContent, type Node } from './helpers/component-harness'
const tick = () => new Promise(resolve => setImmediate(resolve))
const alice = { userId: '11111111-1111-4111-8111-111111111111', name: 'Alice fixture', departmentId: '33333333-3333-4333-8333-333333333333', departmentName: 'ADM' }
const bob = { ...alice, userId: '22222222-2222-4222-8222-222222222222', name: 'Bob fixture' }
function deferred() { let resolve!: (value: any) => void, reject!: (reason: unknown) => void; const promise = new Promise<any>((a,b) => {resolve=a; reject=b}); return { promise, resolve, reject } }
const staff = (page = 1, members = [alice, bob]) => ({ staff: members, total: 45, pageNumber: page, pageSize: 20, taskState: 'NotRequested', tasks: null })
const taskResult = (member: any = null, page = 1, title = 'Task beyond old global page', total = 67) => ({ taskState: 'Connected', selectedAssignee: member, tasks: { items: [{ taskId: 'opaque-task', assignee: member ?? { ...bob, name: 'Verified server name' }, title, status: '<opaque-status>', dueAt: null }], total, pageNumber: page, pageSize: 20 } })
function button(tree: Node, label: string) { const all = nodes(tree); const found = [...all, ...all.flatMap(n => nodes(n.props.action))].find(n => n.type === 'Button' && textContent(n) === label); assert.ok(found, `Missing ${label}`); return found }
function fixture(isEn = true) {
  const staffRequests: any[] = [], taskRequests: any[] = []; let epoch = 'first', listener = () => {}
  const previousFetch = globalThis.fetch
  // Real component, API transport and decoders; only the HTTP boundary is a
  // synthetic server. Deliberately resolves aborted fetches to test ownership.
  globalThis.fetch = async (url, options) => {
    const route = new URL(String(url)), page = Number(route.searchParams.get('pageNumber'))
    assert.equal(route.searchParams.get('pageSize'), '20')
    assert.equal(options?.cache, 'no-store'); assert.equal(options?.redirect, 'error')
    const r = { page, signal: options?.signal, filter: { pageNumber: page, pageSize: 20, assigneeUserId: route.searchParams.get('assigneeUserId') ?? undefined }, reply: deferred() }
    if (route.pathname === '/api/v2/my-staff/tasks') taskRequests.push(r)
    else { assert.equal(route.pathname, '/api/v2/my-staff'); assert.equal(route.searchParams.get('includeTasks'), 'false'); assert.equal(route.searchParams.has('assigneeUserId'), false); staffRequests.push(r) }
    try { return Response.json({ success: true, data: await r.reply.promise }) }
    catch(error) { assert.ok(error instanceof ApiRequestError); return Response.json({success:false,message:error.message,code:error.code}, {status:error.status}) }
  }
  const ui = componentHarness(new URL('../../src/views/apps/tasks/MyStaff.tsx', import.meta.url), {
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn, t: translations[isEn ? 'en' : 'vi'] }) },
    '@/services/api': { ApiRequestError, tokenManager: { getEpoch: () => epoch } },
    '@/services/browserSession': { subscribeBrowserSession: (callback: () => void) => { listener = callback; return () => { listener = () => {} } } },
    '@/services/das/reports-staff': { getMyStaffMembers, getMyStaffTasks }
  })
  const dispose = ui.unmount
  ui.unmount = () => { dispose(); globalThis.fetch = previousFetch }
  const commit = () => { ui.render(); ui.commit() }
  commit()
  assert.equal(taskRequests.length, 1, 'My Staff must issue an independent tasks request')
  return {ui, staffRequests, taskRequests, commit, changeSession() { epoch = 'second'; listener() } }
}
async function ready(f: ReturnType<typeof fixture>) { f.staffRequests[0].reply.resolve(staff()); f.taskRequests[0].reply.resolve(taskResult()); await tick() }
function select(f: ReturnType<typeof fixture>, name: string) { const row = nodes(f.ui.render()).find(n => n.type === 'TableRow' && textContent(n).includes(name))!; const b = nodes(row).find(n => n.type === 'Button')!; b.props.onClick(); f.commit() }
test('Staff and task pagination stay independent and server filtering reaches tasks beyond the old global page', async () => {
  const f=fixture(); await ready(f)
  assert.ok(textContent(f.ui.render()).includes('Verified server name'))
  button(f.ui.render(), 'Next task page').props.onClick(); f.commit()
  assert.equal(f.taskRequests[1].filter.pageNumber, 2); assert.equal(f.staffRequests.length,1)
  f.taskRequests[1].reply.resolve(taskResult(null,2)); await tick()
  select(f,'Alice fixture'); assert.equal(f.taskRequests[2].filter.assigneeUserId,alice.userId); assert.equal(f.taskRequests[2].filter.pageNumber,1)
  f.taskRequests[2].reply.resolve(taskResult({...alice,name:'Alice refreshed'},1,'Alice beyond page 3',1)); await tick()
  assert.ok(textContent(f.ui.render()).includes('Alice beyond page 3')); assert.ok(textContent(f.ui.render()).includes('Alice refreshed'))
  button(f.ui.render(),'Next staff page').props.onClick(); f.commit(); f.staffRequests[1].reply.resolve(staff(2,[bob])); await tick()
  button(f.ui.render(),'Next staff page').props.onClick(); f.commit(); f.staffRequests[2].reply.resolve(staff(3,[bob])); await tick()
  assert.equal(f.taskRequests.length,3); assert.ok(textContent(f.ui.render()).includes('Alice refreshed'))
  button(f.ui.render(),'Clear filter').props.onClick(); f.commit(); assert.equal(f.taskRequests[3].filter.assigneeUserId,undefined); assert.equal(f.taskRequests[3].filter.pageNumber,1); assert.equal(f.staffRequests.length,3)
  f.ui.unmount()
})
test('Fast A to B selection aborts A and never renders its late result', async () => {
  const f=fixture(); await ready(f); select(f,'Alice fixture'); const a=f.taskRequests[1]; select(f,'Bob fixture'); const b=f.taskRequests[2]
  assert.equal(a.signal.aborted,true); b.reply.resolve(taskResult(bob,1,'B current')); await tick(); a.reply.resolve(taskResult(alice,1,'A stale')); await tick()
  assert.ok(textContent(f.ui.render()).includes('B current')); assert.ok(!textContent(f.ui.render()).includes('A stale'))
  assert.equal(nodes(f.ui.render()).filter(n => n.type==='CircularProgress').length,0); f.ui.unmount(); assert.equal(b.signal.aborted,true)
})
for (const error of [new ApiRequestError(503,'Authority unavailable',undefined,'STAFF_AUTHORITY_UNAVAILABLE'),new ApiRequestError(403,'Inactive',undefined,'ACTOR_INACTIVE'),new ApiRequestError(401,'Expired')]) test(`Task authority ${error.status}/${error.code} invalidates pending staff success`,async()=>{
  const f=fixture(); f.taskRequests[0].reply.reject(error); await tick(); assert.equal(f.staffRequests[0].signal.aborted,true)
  f.staffRequests[0].reply.resolve(staff()); await tick(); assert.ok(!textContent(f.ui.render()).includes('Alice fixture')); assert.ok(!textContent(f.ui.render()).includes('No tasks found')); f.ui.unmount()
})
test('Staff authority denial invalidates a late task success and selection',async()=>{
  const f=fixture(); f.staffRequests[0].reply.reject(new ApiRequestError(403,'Denied')); await tick(); assert.equal(f.taskRequests[0].signal.aborted,true)
  f.taskRequests[0].reply.resolve(taskResult(alice,1,'Leaked task')); await tick(); assert.ok(!textContent(f.ui.render()).includes('Leaked task')); assert.ok(!textContent(f.ui.render()).includes('Alice fixture')); f.ui.unmount()
})
test('Task authority loss clears already rendered staff, task names and active selection',async()=>{
  const f=fixture(); await ready(f); select(f,'Alice fixture'); f.taskRequests[1].reply.reject(new ApiRequestError(503,'Authority unavailable',undefined,'AUTHORITY_MISMATCH')); await tick(); f.commit()
  const text=textContent(f.ui.render()); assert.ok(!text.includes('Alice fixture')); assert.ok(!text.includes('Verified server name')); assert.ok(!text.includes('Filtering tasks for')); assert.equal(f.taskRequests.length,2); f.ui.unmount()
})
test('Session change immediately clears both scopes and aborts responses from the old epoch',async()=>{
  const f=fixture(); await ready(f); select(f,'Alice fixture'); const old=f.taskRequests[1]; f.changeSession()
  assert.equal(old.signal.aborted,true); assert.ok(!textContent(f.ui.render()).includes('Alice fixture')); f.commit(); old.reply.resolve(taskResult(alice,1,'Old private task')); await tick(); assert.ok(!textContent(f.ui.render()).includes('Old private task')); assert.equal(f.taskRequests.at(-1).filter.assigneeUserId,undefined); f.ui.unmount()
})
test('Scope 409 clears both scopes and waits for one explicit refresh without retrying indefinitely',async()=>{
  const f=fixture(); f.taskRequests[0].reply.reject(new ApiRequestError(409,'Scope changed',undefined,'STAFF_SCOPE_CHANGED')); await tick(); f.commit(); assert.equal(f.taskRequests.length,1)
  const header=nodes(f.ui.render()).find(n=>n.type==='CardHeader')!; header.props.action.props.onClick(); f.commit(); assert.equal(f.taskRequests.length,2); assert.equal(f.staffRequests.length,2)
  f.taskRequests[1].reply.reject(new ApiRequestError(409,'Scope changed',undefined,'STAFF_SCOPE_CHANGED')); await tick(); f.commit(); assert.equal(f.taskRequests.length,2); f.ui.unmount()
})
test('Degraded task count stays unknown while verified staff remains visible; Connected empty alone shows no tasks',async()=>{
  const f=fixture(); f.staffRequests[0].reply.resolve(staff()); f.taskRequests[0].reply.resolve({taskState:'TMS_CONTRACT_INVALID',selectedAssignee:null,tasks:null}); await tick()
  let text=textContent(f.ui.render()); assert.ok(text.includes('Alice fixture')); assert.ok(text.includes('Task count is unknown')); assert.ok(!text.includes('No tasks found'))
  nodes(f.ui.render()).find(n=>n.type==='CardHeader')!.props.action.props.onClick(); f.commit(); f.staffRequests[1].reply.resolve(staff()); f.taskRequests[1].reply.resolve({taskState:'Connected',selectedAssignee:null,tasks:{items:[],total:0,pageNumber:1,pageSize:20}}); await tick()
  assert.ok(textContent(f.ui.render()).includes('No tasks found')); f.ui.unmount()
})
test('Empty task page with positive total offers page recovery without global empty message',async()=>{
  const f=fixture(); await ready(f); button(f.ui.render(),'Next task page').props.onClick(); f.commit(); f.taskRequests[1].reply.resolve({taskState:'Connected',selectedAssignee:null,tasks:{items:[],total:1,pageNumber:2,pageSize:20}}); await tick()
  assert.ok(!textContent(f.ui.render()).includes('No tasks found')); assert.equal(button(f.ui.render(),'Previous task page').props.disabled,false); f.ui.unmount()
})
test('Member buttons expose names and selection for keyboard; long title and unknown status remain text in VI',async()=>{
  const f=fixture(false); f.staffRequests[0].reply.resolve(staff()); f.taskRequests[0].reply.resolve(taskResult(null,1,'<script>'+ 'a'.repeat(1990))); await tick()
  const row=nodes(f.ui.render()).find(n=>n.type==='TableRow' && textContent(n).includes('Alice fixture'))!; const b=nodes(row).find(n=>n.type==='Button')!; assert.match(b.props['aria-label'],/Alice fixture/); assert.equal(b.props['aria-pressed'],false)
  const chip=nodes(f.ui.render()).find(n=>n.type==='Chip' && n.props.label==='<opaque-status>')!; assert.ok(chip); assert.equal(chip.props.color,'default'); assert.ok(textContent(f.ui.render()).includes('Không có hạn')); b.props.onClick(); f.commit(); f.taskRequests[1].reply.resolve(taskResult(alice,1,'VI task')); await tick(); assert.ok(button(f.ui.render(),'Bỏ lọc')); assert.ok(textContent(f.ui.render()).includes('Đang lọc')); f.ui.unmount()
})
