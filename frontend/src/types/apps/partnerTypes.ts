export type PartnerType = {
  id: string
  code?: string
  fullName: string
  shortName: string
  entityType?: string
  email?: string
  phone?: string
  address?: string
  taxCode?: string
  isActive?: boolean
  isDeleted?: boolean
  createdAt?: string
  updatedAt?: string
}

export type PartnerFilter = {
  searchTerm?: string
  entityType?: string
  isActive?: boolean
  pageNumber?: number
  pageSize?: number
}
