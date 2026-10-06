export type DirectoryUnitDto = { id: string; name: string; code: string | null; parentId: string | null; isDepartment: boolean; isActive: boolean }
export type DirectoryUserDto = { id: string; displayName: string }
export type DirectoryPageDto<T> = { items: T[]; pageNumber: number; pageSize: number; totalCount: number; authorizationRevision: number; verifiedAt: string }

