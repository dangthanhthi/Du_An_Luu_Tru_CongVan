import type { SessionIntent } from '../api'
import { requestApiEnvelope, requestPdfBytes, ApiRequestError } from '../api'
import { isDocumentGuid } from './documents'
export type PdfInfo = { fileId: string; originalName: string; sizeBytes: number; sha256: string; completion: { isComplete: boolean } }
export type UploadedPdf = { id: string; originalName: string; state: string; canAttach: boolean }
const invalid = () => new ApiRequestError(502, 'Phản hồi PDF không hợp lệ.')
export const documentPdfApi = {
  async uploadInfo(fileId: string): Promise<UploadedPdf> {
    if (!isDocumentGuid(fileId)) throw new ApiRequestError(400, 'Định danh tệp không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('files', `/api/files/${fileId}/info`, { cache: 'no-store' })
    const x = data as UploadedPdf | null

    if (!x || x.id !== fileId || !['Available', 'PendingScan', 'Rejected', 'Missing', 'Failed'].includes(x.state) || typeof x.canAttach !== 'boolean' || typeof x.originalName !== 'string') throw invalid()
    return x
  },
  async info(id: string, signal?: AbortSignal): Promise<PdfInfo> {
    if (!isDocumentGuid(id)) throw new ApiRequestError(400, 'Định danh công văn không hợp lệ.')
    const { data } = await requestApiEnvelope<unknown>('document', `/api/v2/documents/${id}/pdf`, { signal, cache: 'no-store' })
    const x = data as PdfInfo | null

    if (!x || !isDocumentGuid(x.fileId) || typeof x.originalName !== 'string' || !x.originalName.trim() || !Number.isSafeInteger(x.sizeBytes) || x.sizeBytes < 1 || x.sizeBytes > 25 * 1024 * 1024 || !/^[0-9a-f]{64}$/i.test(x.sha256) || typeof x.completion?.isComplete !== 'boolean') throw invalid()
    return x
  },
  async bytes(fileId: string, signal?: AbortSignal) {
    if (!isDocumentGuid(fileId)) throw new ApiRequestError(400, 'Định danh tệp không hợp lệ.')
    return requestPdfBytes(fileId, signal)
  },
  async upload(file: File, sessionIntent?: SessionIntent): Promise<UploadedPdf> {
    if (!file.name.toLowerCase().endsWith('.pdf') || file.size < 1 || file.size > 25 * 1024 * 1024) throw new ApiRequestError(400, 'Chọn một PDF không quá 25 MB.')
    const form = new FormData()

    form.append('file', file)
    const { data } = await requestApiEnvelope<unknown>('files', '/api/files/upload', { sessionIntent, method: 'POST', body: form })
    const x = data as UploadedPdf | null

    if (!x || !isDocumentGuid(x.id) || !['Available', 'PendingScan'].includes(x.state) || typeof x.canAttach !== 'boolean' || typeof x.originalName !== 'string') throw invalid()
    return x
  },
  async replace(id: string, operationId: string, fileId: string, expectedVersion: number, sessionIntent?: SessionIntent) {
    if (![id, operationId, fileId].every(isDocumentGuid) || !Number.isSafeInteger(expectedVersion) || expectedVersion < 1) throw new ApiRequestError(400, 'Yêu cầu thay PDF không hợp lệ.')
    const { data } = await requestApiEnvelope<any>('document', `/api/v2/documents/${id}/pdf`, { sessionIntent, method: 'PUT', body: JSON.stringify({ operationId, fileId, expectedVersion }) })

    if (!data || data.documentId !== id || data.operationId !== operationId || data.fileId !== fileId || !Number.isSafeInteger(data.version) || data.version < expectedVersion) throw invalid()
    return data
  }
}
