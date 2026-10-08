import type { DirectoryPageDto, DirectoryUnitDto, DirectoryUserDto } from '../../types/das/directory'
import { requestApiEnvelope, V2ApiError } from './http'
type PageQuery = { pageNumber?: number; pageSize?: number }
const text = (x: unknown): x is string => typeof x === 'string' && x.trim().length > 0
function normalizePageQuery(query: PageQuery = {}) {
  const pageNumber = query.pageNumber ?? 1, pageSize = query.pageSize ?? 20

  if (!Number.isSafeInteger(pageNumber) || pageNumber < 1 || !Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100)
    throw new V2ApiError(400, 'Thông tin phân trang không hợp lệ.')
  return { pageNumber, pageSize }
}
function page<T extends { id: string }>(
  data: unknown,
  expected: { pageNumber: number; pageSize: number },
  valid: (item: unknown) => boolean
): DirectoryPageDto<T> {
  const value = data as Partial<DirectoryPageDto<T>> | null

  if (
    !value ||
    !Array.isArray(value.items) ||
    !value.items.every(valid) ||
    !Number.isSafeInteger(value.pageNumber) ||
    !Number.isSafeInteger(value.pageSize) ||
    !Number.isSafeInteger(value.totalCount) ||
    !Number.isSafeInteger(value.authorizationRevision) ||
    value.pageNumber !== expected.pageNumber ||
    value.pageSize !== expected.pageSize ||
    value.items.length > value.pageSize ||
    value.totalCount! < value.items.length ||
    value.authorizationRevision! < 1 ||
    !text(value.verifiedAt) ||
    !Number.isFinite(Date.parse(value.verifiedAt))
  ) {
    throw new V2ApiError(502, 'Phản hồi tổ chức không hợp lệ.')
  }

  const ids = new Set<string>()
  for (const item of value.items) {
    if (!item || !text((item as any).id) || ids.has((item as any).id)) {
      throw new V2ApiError(502, 'Phản hồi tổ chức chứa phần tử trùng lặp.')
    }
    ids.add((item as any).id)
  }

  return value as DirectoryPageDto<T>
}
export const directoryApi = {
  getDepartments: async (query: PageQuery & { includeGroups?: boolean } = {}, signal?: AbortSignal): Promise<DirectoryPageDto<DirectoryUnitDto>> => {
    const norm = normalizePageQuery(query)
    const params = new URLSearchParams({
      pageNumber: String(norm.pageNumber),
      pageSize: String(norm.pageSize),
      includeGroups: String(query.includeGroups ?? false)
    })
    const response = await requestApiEnvelope('auth', '/api/v2/directory/departments?' + params, { signal })

    return page<DirectoryUnitDto>(response.data, norm, (item: unknown) => {
      const x = item as DirectoryUnitDto | null

      return !!x && text(x.id) && text(x.name) && (x.code === null || text(x.code)) &&
        (x.parentId === null || text(x.parentId)) && typeof x.isDepartment === 'boolean' && typeof x.isActive === 'boolean'
    })
  },
  getUsers: async (query: PageQuery & { departmentId: string; purpose: 'originator' }, signal?: AbortSignal): Promise<DirectoryPageDto<DirectoryUserDto>> => {
    if (!text(query.departmentId) || query.purpose !== 'originator') throw new V2ApiError(400, 'Phạm vi tra cứu nhân sự không hợp lệ.')
    const norm = normalizePageQuery(query)
    const params = new URLSearchParams({
      pageNumber: String(norm.pageNumber),
      pageSize: String(norm.pageSize),
      departmentId: query.departmentId,
      purpose: query.purpose
    })
    const response = await requestApiEnvelope('auth', '/api/v2/directory/users?' + params, { signal })

    return page<DirectoryUserDto>(response.data, norm, (item: unknown) => {
      const x = item as DirectoryUserDto | null

      return !!x && text(x.id) && text(x.displayName)
    })
  }
}

