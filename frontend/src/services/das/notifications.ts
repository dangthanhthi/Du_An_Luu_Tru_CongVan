import type { SessionIntent } from '../api'
import { ApiRequestError, requestApiEnvelope } from '../api'
export type InAppNotification = { id: string; recipientUserId: string; title: string; message: string; actionUrl: string | null; relatedDocumentId: string | null; notificationType: string; isRead: boolean; readAt: string | null; createdAt: string }
const guid = (v: unknown) => typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)
// eslint-disable-next-line no-control-regex -- Explicitly reject control characters in untrusted action URLs.
const safePath = (v: unknown) => v === null || typeof v === 'string' && v.startsWith('/') && !v.startsWith('//') && !v.includes('\\') && !/[\x00-\x1f]/.test(v) && v.length <= 500
const invalid = () => new ApiRequestError(502, 'Phản hồi thông báo không hợp lệ.')
export const notificationsApi = {
  async list(signal?: AbortSignal) {
    const r = (await requestApiEnvelope<{ items: InAppNotification[]; totalCount: number; page: number; pageSize: number }>('notification', '/api/notifications/my?page=1&pageSize=20', { signal, cache: 'no-store', redirect: 'error' })).data
    if (!r || r.page !== 1 || r.pageSize !== 20 || !Number.isSafeInteger(r.totalCount) || r.totalCount < 0 || !Array.isArray(r.items) || r.items.length > 20 || r.items.length > r.totalCount || r.items.some(x => !guid(x.id) || !guid(x.recipientUserId) || typeof x.title !== 'string' || !x.title.trim() || typeof x.message !== 'string' || typeof x.isRead !== 'boolean' || !safePath(x.actionUrl) || typeof x.createdAt !== 'string' || Number.isNaN(Date.parse(x.createdAt)))) throw invalid()
    return r
  },
  async unread(signal?: AbortSignal) {
    const r = (await requestApiEnvelope<{ unreadCount: number }>('notification', '/api/notifications/unread-count', { signal, cache: 'no-store', redirect: 'error' })).data
    if (!r || !Number.isSafeInteger(r.unreadCount) || r.unreadCount < 0) throw invalid()
    return r.unreadCount
  },
  read(id: string, sessionIntent?: SessionIntent) { if (!guid(id)) throw new ApiRequestError(400, 'ID thông báo không hợp lệ.'); return requestApiEnvelope('notification', `/api/notifications/${id}/read`, { sessionIntent, method: 'PUT', cache: 'no-store', redirect: 'error' }) },
  readAll: (sessionIntent?: SessionIntent) => requestApiEnvelope('notification', '/api/notifications/read-all', { sessionIntent, method: 'PUT', cache: 'no-store', redirect: 'error' })
}
