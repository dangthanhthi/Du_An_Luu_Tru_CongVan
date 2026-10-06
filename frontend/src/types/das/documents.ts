export type DocumentKind = 'Incoming' | 'Outgoing' | 'Internal'
export type DocumentView = 'all' | 'mine' | 'department' | 'cancelled'
export type DocumentStatus = 'InProgress' | 'Distributed' | 'Cancelled'
export type DocumentListItem = {
  id: string
  kind: DocumentKind
  registrationNumber: string
  subject: string
  status: DocumentStatus
  referenceNumber?: string | null
  issuedDate?: string | null
  registrationDate?: string | null
  senderPartnerName?: string | null
  companyCode?: string | null
  departmentName?: string | null
  inputterDisplayName?: string | null
  sensitivity?: 'Normal' | 'Confidential'
  isComplete?: boolean
  allowedActions?: string[]
}
export type DocumentPage = { items: DocumentListItem[]; totalCount: number; pageNumber: number; pageSize: number }
export type DocumentListQuery = {
  kind: DocumentKind
  view: DocumentView
  searchTerm?: string
  status?: DocumentStatus | ''
  pageNumber?: number
  pageSize?: number
}
