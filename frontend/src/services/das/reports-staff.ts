import { ApiRequestError, requestApiEnvelope, requestReportWorkbook } from '../api'

export type ReportFilter = { kind?: 'Outgoing' | 'Internal'; departmentId?: string; includeRecent?: boolean; pageNumber?: number; pageSize?: number }
export type ReportRow = { documentId: string; kind: 'Outgoing' | 'Internal'; departmentId: string; departmentName: string; hasAttachment: boolean; registrationNumber: string; registeredDate: string; issueDate: string | null; originator: string; status: 'InProgress' | 'Distributed'; recipientList: string[]; version: number }
export type IncompleteReport = { items: ReportRow[]; total: number; pageNumber: number; pageSize: number; groups: { departmentId: string; departmentName: string; count: number }[]; evaluatedAt: string; canExport: boolean }
export type StaffMember = { userId: string; name: string; departmentId: string; departmentName: string }
export type StaffTask = { taskId: string; assigneeUserId: string; title: string; status: string; dueAt: string | null }
export type MyStaffPage = { staff: StaffMember[]; total: number; pageNumber: number; pageSize: number; tasks: { items: StaffTask[]; total: number; pageNumber: number; pageSize: number } | null; taskState: string }
const guid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v) && !/^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(v)
const text = (v: unknown): v is string => typeof v === 'string' && v.trim().length > 0
const integer = (v: unknown): v is number => Number.isSafeInteger(v) && (v as number) >= 0
const date = (v: unknown) => typeof v === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(v))
const invalid = () => new ApiRequestError(502, 'Phản hồi báo cáo/nhân sự không hợp lệ.')
const paging = (page = 1, size = 20) => { if (!Number.isSafeInteger(page) || page < 1 || page > 1000000 || !Number.isSafeInteger(size) || size < 1 || size > 100) throw new ApiRequestError(400, 'Phân trang không hợp lệ.'); return { page, size } }
function reportQuery(filter: ReportFilter, exportFile = false) {
  const { page, size } = paging(filter.pageNumber, filter.pageSize)
  if (filter.kind !== undefined && !['Outgoing', 'Internal'].includes(filter.kind) || filter.departmentId !== undefined && !guid(filter.departmentId) || filter.includeRecent !== undefined && typeof filter.includeRecent !== 'boolean') throw new ApiRequestError(400, 'Bộ lọc không hợp lệ.')
  const q = new URLSearchParams()
  if (filter.kind) q.set('kind', filter.kind)
  if (filter.departmentId) q.set('departmentId', filter.departmentId)
  q.set('includeRecent', String(filter.includeRecent ?? false))
  if (!exportFile) { q.set('pageNumber', String(page)); q.set('pageSize', String(size)) }
  return q.toString()
}
export const incompleteReportsApi = {
  async list(filter: ReportFilter = {}, signal?: AbortSignal): Promise<IncompleteReport> {
    const result = (await requestApiEnvelope<IncompleteReport>('document', `/api/v2/reports/incomplete?${reportQuery(filter)}`, { signal, cache: 'no-store', redirect: 'error' })).data
    const { page, size } = paging(filter.pageNumber, filter.pageSize)
    if (!result || result.pageNumber !== page || result.pageSize !== size || !integer(result.total) || typeof result.canExport !== 'boolean' || !text(result.evaluatedAt) || Number.isNaN(Date.parse(result.evaluatedAt)) || !Array.isArray(result.items) || result.items.length > size || result.items.length > result.total || !Array.isArray(result.groups) || result.groups.reduce((n, g) => n + g.count, 0) !== result.total || result.groups.some(g => !guid(g.departmentId) || !text(g.departmentName) || !integer(g.count) || g.count === 0)) throw invalid()
    if (result.items.some(r => !guid(r.documentId) || !guid(r.departmentId) || !guid(r.originator) || !text(r.departmentName) || !text(r.registrationNumber) || !date(r.registeredDate) || r.issueDate !== null && !date(r.issueDate) || typeof r.hasAttachment !== 'boolean' || !['Outgoing', 'Internal'].includes(r.kind) || !['InProgress', 'Distributed'].includes(r.status) || !integer(r.version) || r.version < 1 || !Array.isArray(r.recipientList) || r.recipientList.some(x => !text(x)) || !result.groups.some(g => g.departmentId === r.departmentId))) throw invalid()
    return result
  },
  export: (filter: ReportFilter, signal?: AbortSignal) => requestReportWorkbook(`/api/v2/reports/incomplete/export?${reportQuery(filter, true)}`, signal)
}
export async function getMyStaff(pageNumber = 1, pageSize = 20, signal?: AbortSignal): Promise<MyStaffPage> {
  paging(pageNumber, pageSize)
  const r = (await requestApiEnvelope<MyStaffPage>('document', `/api/v2/my-staff?pageNumber=${pageNumber}&pageSize=${pageSize}&includeTasks=true`, { signal, cache: 'no-store', redirect: 'error' })).data
  if (!r || !integer(r.total) || r.pageNumber !== pageNumber || r.pageSize !== pageSize || !text(r.taskState) || !Array.isArray(r.staff) || r.staff.length > pageSize || r.staff.length > r.total || r.staff.some(s => !guid(s.userId) || !guid(s.departmentId) || !text(s.name) || !text(s.departmentName))) throw invalid()
  if (r.tasks !== null && (!r.tasks || r.taskState !== 'Connected' || r.tasks.pageNumber !== pageNumber || r.tasks.pageSize !== pageSize || !integer(r.tasks.total) || !Array.isArray(r.tasks.items) || r.tasks.items.length > pageSize || r.tasks.items.length > r.tasks.total || r.tasks.items.some(t => !text(t.taskId) || !guid(t.assigneeUserId) || !text(t.title) || !text(t.status) || t.dueAt !== null && (!text(t.dueAt) || Number.isNaN(Date.parse(t.dueAt)))))) throw invalid()
  return r
}
