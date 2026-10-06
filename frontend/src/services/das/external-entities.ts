import { ApiRequestError, requestApiEnvelope } from '../api'
import { entityTypes } from '../../types/das/external-entities'
import type { ExternalEntity, ExternalEntityDraft, ExternalEntityPage, ExternalEntityQuery } from '../../types/das/external-entities'

const guid = (x: unknown): x is string => typeof x === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(x)
const integer = (x: unknown, min: number, max = Number.MAX_SAFE_INTEGER): x is number => Number.isSafeInteger(x) && Number(x) >= min && Number(x) <= max
const invalid = () => new ApiRequestError(502, 'Phản hồi danh mục đơn vị không hợp lệ.')
function item(data: unknown): ExternalEntity {
  const x = data as ExternalEntity | null

  if (!x || !guid(x.id) || typeof x.fullName !== 'string' || !x.fullName.trim() || x.fullName.length > 500 ||
    !entityTypes.includes(x.entityType) || !integer(x.version, 1) || typeof x.isActive !== 'boolean' || typeof x.isDeleted !== 'boolean' || (x.isDeleted && x.isActive)) throw invalid()
  for (const [key, max] of [['shortName', 450], ['taxCode', 450], ['email', 254], ['phone', 50], ['address', 1000], ['contactPerson', 500], ['contactInformation', 2000]] as const) {
    const text = x[key]

    if (text !== null && (typeof text !== 'string' || text.length > max)) throw invalid()
  }
  return x
}
function payload(input: ExternalEntityDraft) {
  const fullName = typeof input.fullName === 'string' ? input.fullName.trim() : ''
  const entityType = input.entityType ?? 'Both'

  if (!fullName || fullName.length > 500 || !entityTypes.includes(entityType)) throw new ApiRequestError(400, 'Tên đầy đủ và loại đơn vị không hợp lệ.')
  const result: Required<ExternalEntityDraft> = { fullName, entityType, shortName: null, taxCode: null, email: null, phone: null, address: null, contactPerson: null, contactInformation: null }

  for (const [key, max] of [['shortName', 450], ['taxCode', 450], ['email', 254], ['phone', 50], ['address', 1000], ['contactPerson', 500], ['contactInformation', 2000]] as const) {
    const value = input[key]

    if (value !== undefined && value !== null && (typeof value !== 'string' || value.length > max)) throw new ApiRequestError(400, 'Thông tin đơn vị vượt giới hạn hoặc không hợp lệ.')
    result[key] = value?.trim() || null
  }
  return result
}
function same(entry: ExternalEntity, submitted: Required<ExternalEntityDraft>) {
  if (Object.entries(submitted).some(([key, value]) => entry[key as keyof ExternalEntityDraft] !== value)) throw invalid()
}
function identity(id: string) { if (!guid(id)) throw new ApiRequestError(400, 'Định danh đơn vị không hợp lệ.') }
export const externalEntityApi = {
  async getOptions(signal?: AbortSignal): Promise<{ canManage: boolean }> {
    const { data } = await requestApiEnvelope<any>('partner', '/api/partners/options', { signal, cache: 'no-store', redirect: 'error' })

    if (!data || typeof data.canManage !== 'boolean') throw invalid()
    return { canManage: data.canManage }
  },
  async getList(query: ExternalEntityQuery = {}, signal?: AbortSignal): Promise<ExternalEntityPage> {
    const pageNumber = query.pageNumber ?? 1, pageSize = query.pageSize ?? 10, searchTerm = query.searchTerm?.trim() ?? ''

    if (!integer(pageNumber, 1, 1000000) || !integer(pageSize, 1, 100) || searchTerm.length > 200 ||
      (query.entityType !== undefined && !entityTypes.includes(query.entityType)) ||
      (query.isActive !== undefined && typeof query.isActive !== 'boolean') || (query.includeDeleted !== undefined && typeof query.includeDeleted !== 'boolean')) throw new ApiRequestError(400, 'Tham số tra cứu không hợp lệ.')
    const params = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize), searchTerm })

    if (query.entityType !== undefined) params.set('entityType', query.entityType)
    if (query.isActive !== undefined) params.set('isActive', String(query.isActive))
    if (query.includeDeleted) params.set('includeDeleted', 'true')
    const { data } = await requestApiEnvelope<ExternalEntityPage>('partner', `/api/partners?${params}`, { signal, cache: 'no-store', redirect: 'error' })

    if (!data || !Array.isArray(data.items) || data.pageNumber !== pageNumber || data.pageSize !== pageSize || !integer(data.totalCount, 0, 2147483647)) throw invalid()
    const entries = data.items.map(item)

    if (entries.length !== Math.min(pageSize, Math.max(0, data.totalCount - (pageNumber - 1) * pageSize)) ||
      new Set(entries.map(x => x.id)).size !== entries.length || entries.some(x => (!query.includeDeleted && x.isDeleted) ||
        (query.isActive !== undefined && x.isActive !== query.isActive) || (query.entityType !== undefined && x.entityType !== query.entityType))) throw invalid()
    return { ...data, items: entries }
  },
  async getById(id: string, signal?: AbortSignal): Promise<ExternalEntity> {
    identity(id)
    const { data } = await requestApiEnvelope('partner', '/api/partners/' + id, { signal, cache: 'no-store', redirect: 'error' })
    const entry = item(data)

    if (entry.id !== id) throw invalid()
    return entry
  },
  async create(draft: ExternalEntityDraft): Promise<ExternalEntity> {
    const submitted = payload(draft)
    const { data } = await requestApiEnvelope('partner', '/api/partners', { method: 'POST', body: JSON.stringify(submitted), cache: 'no-store', redirect: 'error' })
    const entry = item(data)

    same(entry, submitted)
    if (entry.version !== 1 || entry.isDeleted || !entry.isActive) throw invalid()
    return entry
  },
  async update(original: ExternalEntity, draft: ExternalEntityDraft, isActive: boolean): Promise<ExternalEntity> {
    item(original)
    if (original.isDeleted || !integer(original.version, 1, Number.MAX_SAFE_INTEGER - 1) || typeof isActive !== 'boolean') throw new ApiRequestError(400, 'Khôi phục đơn vị trước khi sửa hoặc tải lại phiên bản.')
    const submitted = payload(draft)
    const { data } = await requestApiEnvelope('partner', '/api/partners/' + original.id, {
      method: 'PUT', body: JSON.stringify({ ...submitted, expectedVersion: original.version, isActive }), cache: 'no-store', redirect: 'error'
    })
    const entry = item(data)

    same(entry, submitted)
    if (entry.id !== original.id || entry.version !== original.version + 1 || entry.isActive !== isActive || entry.isDeleted) throw invalid()
    return entry
  },
  async changeDeletion(original: ExternalEntity, deleted: boolean): Promise<ExternalEntity> {
    item(original)
    if (original.isDeleted === deleted || !integer(original.version, 1, Number.MAX_SAFE_INTEGER - 1)) throw new ApiRequestError(400, 'Trạng thái hoặc phiên bản không hợp lệ.')
    const { data } = await requestApiEnvelope('partner', '/api/partners/' + original.id + (deleted ? '' : '/restore'), {
      method: deleted ? 'DELETE' : 'POST', body: JSON.stringify({ expectedVersion: original.version }), cache: 'no-store', redirect: 'error'
    })
    const entry = item(data)

    if (entry.id !== original.id || entry.version !== original.version + 1 || entry.isDeleted !== deleted) throw invalid()
    return entry
  }
}
