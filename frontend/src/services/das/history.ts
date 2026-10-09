import { ApiRequestError, requestApiEnvelope } from '../api'
import type { SessionIntent } from '../api'
import type { HistoryQuery, HistoryEvent, PartnerAuditPage, DocumentLifecyclePage } from '../../types/das/history'

const invalid = () => new ApiRequestError(502, 'Phản hồi lịch sử không hợp lệ.')
const badQuery = () => new ApiRequestError(400, 'Tham số lịch sử không hợp lệ.')
export const historyGuid = (x: unknown): x is string => typeof x === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(x) && x !== '00000000-0000-0000-0000-000000000000'
const version = (x: unknown): x is string => typeof x === 'string' && /^[1-9][0-9]{0,18}$/.test(x) && BigInt(x) <= 9223372036854775807n
const integer = (x: unknown, min: number, max: number) => Number.isSafeInteger(x) && Number(x) >= min && Number(x) <= max
function exact(x: any, keys: string[]) {
  if (!x || typeof x !== 'object' || Array.isArray(x) || Object.keys(x).length !== keys.length || keys.some(key => !(key in x))) throw invalid()
}
function timestamp(x: unknown) {
  if (typeof x !== 'string' || !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$/.test(x)) return false
  const parsed = new Date(x)

  return Number.isFinite(parsed.getTime()) && parsed.toISOString().slice(0, 19) === x.slice(0, 19)
}
function queryParams(id: string, query: HistoryQuery) {
  const pageNumber = query.pageNumber ?? 1, pageSize = query.pageSize ?? 20

  if (!historyGuid(id) || !integer(pageNumber, 1, 1000000) || !integer(pageSize, 1, 100) ||
    (query.throughVersion !== undefined && !version(query.throughVersion)) || (pageNumber > 1 && !query.throughVersion)) throw badQuery()
  const params = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize) })

  if (query.throughVersion) params.set('throughVersion', query.throughVersion)
  return { params, pageNumber, pageSize }
}
function page(data: any, id: string, query: HistoryQuery, expected: ReturnType<typeof queryParams>, document: boolean) {
  exact(data, [document ? 'documentId' : 'partnerId', 'throughVersion', 'items', 'totalCount', 'pageNumber', 'pageSize', ...(document ? ['coverage'] : [])])
  if (!historyGuid(data[document ? 'documentId' : 'partnerId']) || data[document ? 'documentId' : 'partnerId'].toLowerCase() !== id.toLowerCase() ||
    !version(data.throughVersion) || (query.throughVersion && data.throughVersion !== query.throughVersion) ||
    data.pageNumber !== expected.pageNumber || data.pageSize !== expected.pageSize || !integer(data.totalCount, 0, 2147483647) || !Array.isArray(data.items) ||
    data.items.length !== Math.min(expected.pageSize, Math.max(0, data.totalCount - (expected.pageNumber - 1) * expected.pageSize)) ||
    (document && data.coverage !== 'V2LifecycleOnly')) throw invalid()
  let previous = BigInt(data.throughVersion) + 1n
  const ids = new Set<string>()

  for (const event of data.items as HistoryEvent[]) {
    if (!historyGuid(event?.id) || !historyGuid(event.actorUserId) || !version(event.version) || !timestamp(event.occurredAt) ||
      BigInt(event.version) >= previous || ids.has(event.id.toLowerCase())) throw invalid()
    ids.add(event.id.toLowerCase()); previous = BigInt(event.version)
    exact(event, ['id', 'actorUserId', 'version', 'occurredAt', 'action', ...(document ? ['fromStatus', 'toStatus', 'cancellationReason'] : ['changesAvailability'])])
    const x = event as any

    if (!document) {
      if (!['Create', 'Update', 'Delete', 'Restore'].includes(x.action) || x.changesAvailability !== 'NotRecorded') throw invalid()
    } else {
      if (BigInt(event.version) < 2n) throw invalid()
      if (x.action === 'Distribute') {
        if (x.fromStatus !== 'InProgress' || x.toStatus !== 'Distributed' || x.cancellationReason !== null) throw invalid()
      } else if (x.action === 'Cancel' || x.action === 'Restore') {
        if (typeof x.cancellationReason !== 'string' || !x.cancellationReason.trim() || x.cancellationReason.length > 4000 ||
          (x.action === 'Cancel' ? !['InProgress', 'Distributed'].includes(x.fromStatus) || x.toStatus !== 'Cancelled' :
            x.fromStatus !== 'Cancelled' || !['InProgress', 'Distributed'].includes(x.toStatus))) throw invalid()
      } else throw invalid()
    }
  }
  return data
}
export const historyApi = {
  async partner(id: string, query: HistoryQuery = {}, signal?: AbortSignal, sessionIntent?: SessionIntent): Promise<PartnerAuditPage> {
    const expected = queryParams(id, query)
    const { data } = await requestApiEnvelope('partner', `/api/partners/${id}/audit?${expected.params}`, { signal, sessionIntent, cache: 'no-store', redirect: 'error' })

    return page(data, id, query, expected, false)
  },
  async document(id: string, query: HistoryQuery = {}, signal?: AbortSignal, sessionIntent?: SessionIntent): Promise<DocumentLifecyclePage> {
    const expected = queryParams(id, query)
    const { data } = await requestApiEnvelope('document', `/api/v2/documents/${id}/history?${expected.params}`, { signal, sessionIntent, cache: 'no-store', redirect: 'error' })

    return page(data, id, query, expected, true)
  }
}
