import type { SessionIntent } from '../api'
import { catalogGroups, editableCatalogGroups } from '../../types/das/catalogs'
import type { CatalogGroup, CatalogItem, CatalogCreate, CatalogEdit, DistributionPage, DistributionTarget, CatalogAdminQuery, CatalogAdminPage } from '../../types/das/catalogs'
import { requestApiEnvelope, V2ApiError } from './http'

const guid = (x: unknown): x is string => typeof x === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(x)
const text = (x: unknown, max: number): x is string => typeof x === 'string' && x.trim().length > 0 && x.length <= max
const integer = (x: unknown, min: number, max = Number.MAX_SAFE_INTEGER): x is number => Number.isSafeInteger(x) && Number(x) >= min && Number(x) <= max
const invalid = () => new V2ApiError(502, 'Phản hồi danh mục không hợp lệ.')
function item(data: unknown): CatalogItem {
  const x = data as CatalogItem | null

  if (!x || !guid(x.id) || !catalogGroups.includes(x.group) || typeof x.code !== 'string' || !/^[A-Za-z0-9_]{1,64}$/.test(x.code) ||
    !text(x.name, 200) || !integer(x.sortOrder, 0, 10000) || typeof x.isActive !== 'boolean' || !integer(x.version, 1)) throw invalid()
  return x
}
function validEdit(edit: CatalogEdit) {
  if (!text(edit.name, 200) || !integer(edit.sortOrder, 0, 10000) || typeof edit.isActive !== 'boolean')
    throw new V2ApiError(400, 'Tên, thứ tự hoặc trạng thái danh mục không hợp lệ.')
}
export const catalogApi = {
  async getOptions(signal?: AbortSignal): Promise<{ canManage: boolean }> {
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/admin/catalogs/options', { signal, cache: 'no-store' })
    const options = data as { canManage: boolean } | null

    if (!options || typeof options !== 'object' || Object.keys(options).length !== 1 || typeof options.canManage !== 'boolean') throw invalid()
    return options
  },
  async getAdminPage(query: CatalogAdminQuery, signal?: AbortSignal): Promise<CatalogAdminPage> {
    const { group } = query
    const activity = query.activity ?? 'Active', pageNumber = query.pageNumber ?? 1, pageSize = query.pageSize ?? 20
    const searchTerm = query.searchTerm?.trim() ?? ''

    if (!catalogGroups.includes(group) || !['Active', 'Inactive', 'All'].includes(activity) ||
      !integer(pageNumber, 1, 1000000) || !integer(pageSize, 1, 100) || searchTerm.length > 200)
      throw new V2ApiError(400, 'Thông tin tra cứu danh mục không hợp lệ.')
    const params = new URLSearchParams({ group, activity, pageNumber: String(pageNumber), pageSize: String(pageSize) })

    if (searchTerm) params.set('searchTerm', searchTerm)
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/admin/catalogs?' + params, { signal, cache: 'no-store' })
    const page = data as CatalogAdminPage | null

    if (!page || page.group !== group || page.activity !== activity || page.pageNumber !== pageNumber || page.pageSize !== pageSize ||
      !integer(page.totalCount, 0, 2147483647) || !Array.isArray(page.items) || page.items.length > pageSize ||
      page.canEditGroup !== editableCatalogGroups.includes(group)) throw invalid()
    const rows = page.items.map(item)

    if (rows.some(x => x.id === '00000000-0000-0000-0000-000000000000' || x.group !== group ||
      (activity !== 'All' && x.isActive !== (activity === 'Active'))) ||
      new Set(rows.map(x => x.id.toLowerCase())).size !== rows.length || new Set(rows.map(x => x.code)).size !== rows.length ||
      rows.length !== Math.min(pageSize, Math.max(0, page.totalCount - (pageNumber - 1) * pageSize))) throw invalid()
    return page
  },
  async getGroup(group: CatalogGroup, signal?: AbortSignal): Promise<CatalogItem[]> {
    if (!catalogGroups.includes(group)) throw new V2ApiError(400, 'Nhóm danh mục không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/catalogs?groups=' + group, { signal, cache: 'no-store' })
    const rows = (data as Record<string, unknown> | null)?.[group]

    if (!data || typeof data !== 'object' || Object.keys(data).length !== 1 || !Array.isArray(rows)) throw invalid()
    const entries = rows.map(item)

    if (entries.some(x => x.group !== group || !x.isActive) || new Set(entries.map(x => x.id)).size !== entries.length ||
      new Set(entries.map(x => x.code)).size !== entries.length) throw invalid()
    return entries
  },
  async getById(id: string, signal?: AbortSignal): Promise<CatalogItem> {
    if (!guid(id)) throw new V2ApiError(400, 'Định danh danh mục không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/catalogs/' + id, { signal, cache: 'no-store' })
    const entry = item(data)

    if (entry.id !== id) throw invalid()
    return entry
  },
  async create(input: CatalogCreate, sessionIntent?: SessionIntent): Promise<CatalogItem> {
    const code = input.code.trim().toUpperCase(), name = input.name.trim()

    if (!editableCatalogGroups.includes(input.group) || !/^[A-Z0-9_]{1,64}$/.test(code) || !text(name, 200))
      throw new V2ApiError(400, 'Nhóm, mã hoặc tên danh mục không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/admin/catalogs', {
      sessionIntent, method: 'POST', body: JSON.stringify({ group: input.group, code, name })
    })
    const entry = item(data)

    if (entry.group !== input.group || entry.code !== code || entry.name !== name || !entry.isActive) throw invalid()
    return entry
  },
  async update(original: CatalogItem, edit: CatalogEdit, sessionIntent?: SessionIntent): Promise<CatalogItem> {
    item(original)
    validEdit(edit)
    if (!editableCatalogGroups.includes(original.group)) throw new V2ApiError(400, 'Nhóm danh mục này được cố định.')
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/admin/catalogs/' + original.id, {
      sessionIntent, method: 'PUT', body: JSON.stringify({ name: edit.name.trim(), sortOrder: edit.sortOrder, isActive: edit.isActive, version: original.version })
    })
    const entry = item(data)

    if (entry.id !== original.id || entry.group !== original.group || entry.code !== original.code || entry.version <= original.version ||
      entry.name !== edit.name.trim() || entry.sortOrder !== edit.sortOrder || entry.isActive !== edit.isActive) throw invalid()
    return entry
  },
  async getDistributionTargets(query: { search?: string; pageNumber?: number; pageSize?: number } = {}, signal?: AbortSignal): Promise<DistributionPage> {
    const pageNumber = query.pageNumber ?? 1, pageSize = query.pageSize ?? 20, search = query.search?.trim() ?? ''

    if (!integer(pageNumber, 1, 2147483647) || !integer(pageSize, 1, 100) || search.length > 200)
      throw new V2ApiError(400, 'Thông tin tra cứu nơi nhận không hợp lệ.')
    const params = new URLSearchParams({ pageNumber: String(pageNumber), pageSize: String(pageSize), search })
    const { data } = await requestApiEnvelope<unknown>('document', '/api/v2/distribution-targets?' + params, { signal, cache: 'no-store' })
    const page = data as DistributionPage | null
    const target = (x: DistributionTarget | null) => !!x && guid(x.id) && text(x.name, 200) && (x.initial === null || text(x.initial, 32)) && x.mappingState === 'Pending' && integer(x.version, 1)

    if (!page || !Array.isArray(page.items) || !page.items.every(target) || page.pageNumber !== pageNumber || page.pageSize !== pageSize ||
      !integer(page.totalCount, 0, 2147483647) || page.items.length > pageSize || page.totalCount < (page.items.length ? (pageNumber - 1) * pageSize + page.items.length : 0) ||
      new Set(page.items.map(x => x.id)).size !== page.items.length) throw invalid()
    return page
  }
}
