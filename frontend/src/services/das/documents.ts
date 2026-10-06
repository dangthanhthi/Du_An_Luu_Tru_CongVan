import type { SessionIntent } from '../api'
import { requestApiEnvelope, V2ApiError } from './http'
import type { DocumentKind } from '../../types/das/documents'
import type { DocumentDetailV2, DocumentFormOptions, DocumentWriteResult, EditDraft, RegistrationDraft } from '../../types/das/document-v2'
const kinds = ['Incoming', 'Outgoing', 'Internal']
const statuses = ['InProgress', 'Distributed', 'Cancelled']
const actions = ['Edit', 'ReplacePdf', 'Distribute', 'Cancel', 'Restore']
export const isDocumentGuid = (v: unknown): v is string => typeof v === 'string' && /^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(v) && v !== '00000000-0000-0000-0000-000000000000'
const text = (v: unknown, max = 2000): v is string => typeof v === 'string' && v.trim().length > 0 && v.length <= max
const nullableText = (v: unknown, max = 4000) => v === undefined || v === null || typeof v === 'string' && v.length <= max
const version = (v: unknown): v is number => Number.isSafeInteger(v) && Number(v) > 0
const ids = (v: unknown): v is string[] => Array.isArray(v) && v.length <= 200 && v.every(isDocumentGuid) && new Set(v).size === v.length
const bad = () => new V2ApiError(502, 'Phản hồi công văn không hợp lệ.')
const checkId = (id: string) => { if (!isDocumentGuid(id)) throw new V2ApiError(400, 'Định danh công văn không hợp lệ.') }
function write(data: unknown, id?: string): DocumentWriteResult {
  const x = data as DocumentWriteResult | null

  if (!x || !isDocumentGuid(x.id) || id && x.id !== id || !text(x.registrationNumber, 200) || !version(x.version) || !statuses.includes(x.status)) throw bad()
  return x
}
export const documentsV2Api = {
  async detail(id: string, signal?: AbortSignal): Promise<DocumentDetailV2> {
    checkId(id)
    const { data } = await requestApiEnvelope<unknown>('document', `/api/v2/documents/${id}`, { signal, cache: 'no-store' })
    const x = data as DocumentDetailV2 | null, h = x?.header

    if (!x || !h) throw bad()
    write(h, id)
    if (!kinds.includes(h.kind) || typeof h.subject !== 'string' || !text(h.registrationDate, 10) || !text(h.companyCode, 64) ||
        !nullableText(h.issuedDate, 10) || !nullableText(h.departmentName, 200) || !nullableText(h.referenceNumber, 200) ||
        !nullableText(h.senderPartnerName, 200) || !['Normal', 'Confidential'].includes(h.sensitivity ?? '') ||
        h.isComplete !== undefined && typeof h.isComplete !== 'boolean' || !Array.isArray(h.allowedActions) || !h.allowedActions.every(a => actions.includes(a)) ||
        ![x.originatorUserId, x.ownerDepartmentId, x.inputterUserId, x.lastModifierUserId].every(isDocumentGuid) || !nullableText(x.remark) ||
        !ids(x.relatedDocumentIds) || !['None', 'Pending', 'Ready', 'Missing'].includes(x.pdfState) || !Array.isArray(x.recipients) ||
        x.recipients.length > 200 || !x.recipients.every(r => r && ['ExternalEntity', 'DistributionTarget'].includes(r.referenceType) && isDocumentGuid(r.referenceId) && text(r.name, 200))) throw bad()
    if (x.details !== null && (!x.details || typeof x.details !== 'object' ||
        ![x.details.receivingDate, x.details.referenceNumber, x.details.methodCode, x.details.documentTypeCode, x.details.categoryCode, x.details.contractNumber, x.details.otherRecipients, x.details.others, x.details.senderNameSnapshot].every(v => nullableText(v)) ||
        x.details.senderPartnerId != null && !isDocumentGuid(x.details.senderPartnerId))) throw bad()
    return x
  },
  async options(kind: DocumentKind, signal?: AbortSignal): Promise<DocumentFormOptions> {
    if (!kinds.includes(kind)) throw new V2ApiError(400, 'Loại công văn không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('document', `/api/v2/documents/options?kind=${kind}`, { signal, cache: 'no-store' })
    const x = data as DocumentFormOptions | null

    if (!x || x.kind !== kind || !isDocumentGuid(x.userId) || typeof x.canRegister !== 'boolean' || !Array.isArray(x.targets) || x.targets.length > 2000 ||
        !x.targets.every(t => t && isDocumentGuid(t.originatorUserId) && isDocumentGuid(t.departmentId) && text(t.originatorName, 200) && text(t.departmentName, 200) && text(t.departmentCode, 64) && typeof t.isPrimary === 'boolean') ||
        !Array.isArray(x.catalogs) || !x.catalogs.every(c => c && text(c.group, 64) && text(c.code, 64) && text(c.name, 200)) ||
        !Array.isArray(x.distributionTargets) || !x.distributionTargets.every(t => t && isDocumentGuid(t.id) && text(t.name, 200))) throw bad()
    return x
  },
  async register(draft: RegistrationDraft, key: string, sessionIntent?: SessionIntent): Promise<DocumentWriteResult> {
    if (!/^[A-Za-z0-9._:-]{1,128}$/.test(key) || !kinds.includes(draft.kind)) throw new V2ApiError(400, 'Yêu cầu đăng ký không hợp lệ.')
    return write((await requestApiEnvelope<unknown>('document', '/api/v2/documents', {
      sessionIntent, method: 'POST', headers: { 'Idempotency-Key': key }, body: JSON.stringify(draft)
    })).data)
  },
  async edit(id: string, draft: EditDraft, sessionIntent?: SessionIntent): Promise<DocumentWriteResult> {
    checkId(id)
    if (!version(draft.expectedVersion)) throw new V2ApiError(400, 'Phiên bản không hợp lệ.')
    return write((await requestApiEnvelope<unknown>('document', `/api/v2/documents/${id}`, { sessionIntent, method: 'PUT', body: JSON.stringify(draft) })).data, id)
  },
  async status(id: string, expectedVersion: number, action: string, reason?: string, sessionIntent?: SessionIntent): Promise<DocumentWriteResult> {
    checkId(id)
    if (!version(expectedVersion) || !['Distribute', 'Cancel', 'Restore'].includes(action) ||
        action === 'Cancel' && !text(reason, 4000) || action !== 'Cancel' && reason !== undefined) throw new V2ApiError(400, 'Thao tác trạng thái không hợp lệ.')
    return write((await requestApiEnvelope<unknown>('document', `/api/v2/documents/${id}/status`, {
      sessionIntent, method: 'POST', body: JSON.stringify({ expectedVersion, action, ...(reason !== undefined ? { reason } : {}) })
    })).data, id)
  }
}
