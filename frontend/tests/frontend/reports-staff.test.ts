import assert from 'node:assert/strict'
import { test } from 'node:test'
import { incompleteReportsApi, getMyStaff, type IncompleteReport } from '../../src/services/das/reports-staff'
import { buildDasNavigation } from '../../src/components/layout/shared/dasNavigation'
const id = 'd68a56e8-89ca-4ec5-bc6c-0e7f1d2a8ae7'
const report: IncompleteReport = { items: [{ documentId: id, kind: 'Internal', departmentId: id, departmentName: 'ADM', hasAttachment: false, registrationNumber: '26-09-0001/HL/ADM', registeredDate: '2026-09-01', issueDate: null, originator: id, status: 'InProgress', recipientList: [], version: 1 }], total: 1, pageNumber: 1, pageSize: 20, groups: [{ departmentId: id, departmentName: 'ADM', count: 1 }], evaluatedAt: '2026-10-05T01:00:00Z', canExport: true }
test('Report filters are bounded before network access', async () => {
  await assert.rejects(incompleteReportsApi.list({ pageNumber: 0 }))
  await assert.rejects(incompleteReportsApi.list({ departmentId: 'foreign-invalid' }))
  await assert.rejects(incompleteReportsApi.list({ kind: 'Incoming' as any }))
})
test('Report honors server projection and disables cache/redirects', async () => {
  const previous = globalThis.fetch
  globalThis.fetch = async (url, options) => { assert.match(String(url), /includeRecent=false/); assert.equal(options?.cache, 'no-store'); assert.equal(options?.redirect, 'error'); return Response.json({ success: true, data: report }) }
  try { assert.deepEqual(await incompleteReportsApi.list(), report) } finally { globalThis.fetch = previous }
})
test('Malformed report cannot become a success or fabricated count', async () => {
  const previous = globalThis.fetch
  for (const value of [{ ...report, total: 2 }, { ...report, items: [{ ...report.items[0], status: 'Cancelled' }] }, { ...report, canExport: 'true' }, { ...report, items: [{ ...report.items[0], originator: '' }] }]) {
    globalThis.fetch = async () => Response.json({ success: true, data: value })
    try { await assert.rejects(incompleteReportsApi.list()) } finally { globalThis.fetch = previous }
  }
})
test('Export sends the same filters without page truncation and accepts only XLSX', async () => {
  const previous = globalThis.fetch
  globalThis.fetch = async url => { assert.match(String(url), /includeRecent=true/); assert.match(String(url), /kind=Outgoing/); assert.doesNotMatch(String(url), /pageNumber/); return new Response(new Uint8Array([80,75,3,4]), { headers: { 'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' } }) }
  try { assert.equal((await incompleteReportsApi.export({ includeRecent: true, kind: 'Outgoing', pageNumber: 3 })).size, 4) } finally { globalThis.fetch = previous }
})
test('Export rejects HTML disguised as a workbook', async () => {
  const previous = globalThis.fetch
  globalThis.fetch = async () => new Response('<html/>', { headers: { 'content-type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' } })
  try { await assert.rejects(incompleteReportsApi.export({})) } finally { globalThis.fetch = previous }
})
test('My Staff can preserve scoped members while TMS is unavailable', async () => {
  const previous = globalThis.fetch
  globalThis.fetch = async () => Response.json({ success: true, data: { staff: [{ userId: id, name: 'Fixture', departmentId: id, departmentName: 'ADM' }], total: 1, pageNumber: 1, pageSize: 20, tasks: null, taskState: 'TMS_UNAVAILABLE' } })
  try { assert.equal((await getMyStaff()).tasks, null) } finally { globalThis.fetch = previous }
})
test('Workspace links are present without asserting a business permission', () => {
  const nav = buildDasNavigation({ pathname: '/vi/apps/tasks/my-staff', search: '', lang: 'vi', capabilities: [] })
  assert.equal(nav.activeItemId, 'my-staff'); assert.equal(nav.workspace.length, 2)
})
