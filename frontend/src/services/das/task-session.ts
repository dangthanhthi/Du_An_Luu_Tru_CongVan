import { isDocumentGuid } from './documents'
import { validTaskRequest, type TaskDraft } from './document-tasks'
export type FrozenTaskRequest = { key: string; draft: TaskDraft }
type TaskStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>
const storageKey = (user: string, document: string) => {
  if (!isDocumentGuid(user) || !isDocumentGuid(document)) throw new Error('Phiên yêu cầu task không hợp lệ.')
  return `das_task_request:${user}:${document}`
}
export function loadTaskRequest(storage: TaskStorage, user: string, document: string): FrozenTaskRequest | null {
  const raw = storage.getItem(storageKey(user, document))
  if (raw === null) return null
  if (raw.length > 2000) throw new Error('Không thể đọc yêu cầu task đang gửi. Cần đối soát trước khi tạo thêm.')
  const value = JSON.parse(raw) as FrozenTaskRequest
  if (!value || !validTaskRequest(value.key, value.draft)) throw new Error('Không thể đọc yêu cầu task đang gửi. Cần đối soát trước khi tạo thêm.')
  return { key: value.key, draft: { assigneeUserId: value.draft.assigneeUserId, title: value.draft.title } }
}
export function saveTaskRequest(storage: TaskStorage, user: string, document: string, request: FrozenTaskRequest) {
  if (!validTaskRequest(request.key, request.draft)) throw new Error('Yêu cầu task không hợp lệ.')
  const frozen = { key: request.key, draft: { assigneeUserId: request.draft.assigneeUserId, title: request.draft.title } }
  storage.setItem(storageKey(user, document), JSON.stringify(frozen))
}
export function clearTaskRequest(storage: TaskStorage, user: string, document: string) { storage.removeItem(storageKey(user, document)) }
