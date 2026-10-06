export const entityTypes = ['Sender', 'Recipient', 'Both'] as const
export type ExternalEntityType = typeof entityTypes[number]
export type ExternalEntityDraft = {
  fullName: string; shortName?: string | null; entityType?: ExternalEntityType; taxCode?: string | null
  email?: string | null; phone?: string | null; address?: string | null
  contactPerson?: string | null; contactInformation?: string | null
}
export type ExternalEntity = Required<ExternalEntityDraft> & {
  id: string; entityType: ExternalEntityType; isActive: boolean; isDeleted: boolean; version: number
}
export type ExternalEntityPage = { items: ExternalEntity[]; totalCount: number; pageNumber: number; pageSize: number }
export type ExternalEntityQuery = { searchTerm?: string; entityType?: ExternalEntityType; isActive?: boolean; includeDeleted?: boolean; pageNumber?: number; pageSize?: number }
