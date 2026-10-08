import type { DocumentKind, DocumentListItem, DocumentStatus } from './documents'
export type KindDetails = {
  receivingDate?: string | null; senderPartnerId?: string | null; senderNameSnapshot?: string | null
  referenceNumber?: string | null; methodCode?: string | null; documentTypeCode?: string | null
  categoryCode?: string | null; contractNumber?: string | null; otherRecipients?: string | null; others?: string | null
  recipientPartnerIds?: string[]; distributionTargetIds?: string[]
}
export type RegistrationDraft = {
  kind: DocumentKind; companyCode: string; subject: string; originatorUserId: string; ownerDepartmentId: string
  sensitivity: 'Normal' | 'Confidential'; issuedDate?: string | null; remark?: string | null
  details?: KindDetails | null; relatedDocumentIds?: string[]
}
// Labels are historical server snapshots, separate from editable codes.
export type ReadKindDetails = KindDetails & {
  methodNameSnapshot?: string | null; documentTypeNameSnapshot?: string | null; categoryNameSnapshot?: string | null
}
export type EditDraft = Omit<RegistrationDraft, 'kind' | 'relatedDocumentIds'> & {
  expectedVersion: number; relations?: { addedIds: string[]; removedIds: string[] }
}
export type DocumentWriteResult = { id: string; registrationNumber: string; version: number; status: DocumentStatus }
export type DocumentDetailV2 = {
  header: DocumentListItem & { version: number; registrationDate: string; companyCode: string; allowedActions: string[] }
  originatorUserId: string; ownerDepartmentId: string; inputterUserId: string; lastModifierUserId: string
  remark: string | null; details: ReadKindDetails | null
  recipients: { referenceType: 'ExternalEntity' | 'DistributionTarget'; referenceId: string; name: string }[]
  relatedDocumentIds: string[]; pdfState: 'None' | 'Pending' | 'Ready' | 'Missing'
}
export type RegistrationTarget = { originatorUserId: string; originatorName: string; departmentId: string; departmentCode: string; departmentName: string; isPrimary: boolean }
export type DocumentFormOptions = {
  kind: DocumentKind; userId: string; canRegister: boolean; targets: RegistrationTarget[]
  catalogs: { group: string; code: string; name: string }[]; distributionTargets: { id: string; name: string }[]
}
