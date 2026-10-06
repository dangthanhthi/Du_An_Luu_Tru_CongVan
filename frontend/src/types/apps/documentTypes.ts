export type DocumentDirection = 'incoming' | 'outgoing' | 'internal'
export type DocumentStatus = 'pending' | 'processing' | 'completed' | 'rejected' | 'overdue'

export type DocumentType = {
  id: string
  documentNumber: string
  referenceNumber?: string
  title: string
  direction: DocumentDirection
  issuedDate: string
  partnerName: string
  status: DocumentStatus
  summary?: string
}
