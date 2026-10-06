import { ApiRequestError, requestApiEnvelope } from '../api'
import { isDocumentGuid } from './documents'

export type TaskState = 'PendingConfiguration' | 'Preparing' | 'UnknownOutcome' | 'Linked' | 'Rejected'
export type TaskDraft = { assigneeUserId: string; title: string }
export type TaskReceipt = { correlationId: string; state: TaskState; taskId: string | null }
export type TaskAssignee = { userId: string; name: string; departmentName: string | null; isSelf: boolean }
export type TaskOptions = { documentId: string; userId: string; assignees: TaskAssignee[] }
export type TaskItem = TaskReceipt & TaskDraft
export type TaskHistory = { documentId: string; items: TaskItem[]; total: number; hasPending: boolean }
export const isPendingTask = (state: TaskState) => ['PendingConfiguration', 'Preparing', 'UnknownOutcome'].includes(state)
const text = (v: unknown, max: number): v is string => typeof v === 'string' && v.trim().length > 0 && v.length <= max
const invalid = () => new ApiRequestError(502, 'Phản hồi yêu cầu task không hợp lệ. Tải lại để đối chiếu.')
const checkId = (id: string) => { if (!isDocumentGuid(id)) throw new ApiRequestError(400, 'Định danh không hợp lệ.') }
export function validTaskRequest(key: unknown, draft: TaskDraft): boolean {
  return typeof key === 'string' && /^[A-Za-z0-9._:-]{1,128}$/.test(key) && !!draft && isDocumentGuid(draft.assigneeUserId) && text(draft.title, 250)
}
function receipt(x: TaskReceipt, expected?: string): TaskReceipt {
  if (!x || !isDocumentGuid(x.correlationId) || expected && x.correlationId !== expected || !['PendingConfiguration', 'Preparing', 'UnknownOutcome', 'Linked', 'Rejected'].includes(x.state) || (x.state === 'Linked' ? !text(x.taskId, 200) : x.taskId !== null)) throw invalid()
  return x
}
const get = async <T>(url: string, signal?: AbortSignal) => (await requestApiEnvelope<T>('document', url, { signal, cache: 'no-store', redirect: 'error' })).data
export const documentTasksApi = {
  async options(documentId: string, signal?: AbortSignal): Promise<TaskOptions> {
    checkId(documentId)
    const x = await get<TaskOptions>(`/api/v2/documents/${documentId}/tasks/options`, signal)
    if (!x || x.documentId !== documentId || !isDocumentGuid(x.userId) || !Array.isArray(x.assignees) || x.assignees.length < 1 || x.assignees.length > 2001 || new Set(x.assignees.map(a => a.userId)).size !== x.assignees.length || x.assignees.filter(a => a.isSelf).length !== 1 || x.assignees.some(a => !a || !isDocumentGuid(a.userId) || !text(a.name, 200) || typeof a.isSelf !== 'boolean' || (a.isSelf ? a.userId !== x.userId || a.departmentName !== null : a.userId === x.userId || !text(a.departmentName, 200)))) throw invalid()
    return x
  },
  async history(documentId: string, signal?: AbortSignal): Promise<TaskHistory> {
    checkId(documentId)
    const x = await get<TaskHistory>(`/api/v2/documents/${documentId}/tasks`, signal)
    if (!x || x.documentId !== documentId || !Number.isSafeInteger(x.total) || x.total < 0 || typeof x.hasPending !== 'boolean' || !Array.isArray(x.items) || x.items.length > 50 || x.items.length > x.total || new Set(x.items.map(i => i.correlationId)).size !== x.items.length) throw invalid()
    for (const item of x.items) { receipt(item); if (!isDocumentGuid(item.assigneeUserId) || !text(item.title, 250) || isPendingTask(item.state) && !x.hasPending) throw invalid() }
    return x
  },
  async create(documentId: string, key: string, draft: TaskDraft): Promise<TaskReceipt> {
    checkId(documentId)
    if (!validTaskRequest(key, draft)) throw new ApiRequestError(400, 'Người được giao hoặc tiêu đề task không hợp lệ.')
    return receipt((await requestApiEnvelope<TaskReceipt>('document', `/api/v2/documents/${documentId}/tasks`, { method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify({ assigneeUserId: draft.assigneeUserId, title: draft.title }), cache: 'no-store', redirect: 'error' })).data)
  },
  async retry(correlationId: string): Promise<TaskReceipt> { return this.operation(correlationId, 'retry') },
  async reconcile(correlationId: string): Promise<TaskReceipt> { return this.operation(correlationId, 'reconcile') },
  async operation(correlationId: string, action: 'retry' | 'reconcile'): Promise<TaskReceipt> {
    checkId(correlationId)
    return receipt((await requestApiEnvelope<TaskReceipt>('document', `/api/v2/task-intents/${correlationId}/${action}`, { method: 'POST', cache: 'no-store', redirect: 'error' })).data, correlationId)
  }
}
