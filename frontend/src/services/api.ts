// Central API service for DAS Frontend
// Handles JWT token management, request/response, persistent storage and API Gateway integration
import { requestLegacyLogin } from './legacyAuth'
import type { DocumentListQuery, DocumentPage } from '../types/das/documents'

const GATEWAY_URL = process.env.NEXT_PUBLIC_GATEWAY_URL || 'http://localhost:8080'

const API_URLS = {
  auth: process.env.NEXT_PUBLIC_AUTH_API_URL || GATEWAY_URL,
  partner: process.env.NEXT_PUBLIC_PARTNER_API_URL || GATEWAY_URL,
  files: process.env.NEXT_PUBLIC_FILES_API_URL || GATEWAY_URL,
  document: process.env.NEXT_PUBLIC_DOCUMENT_API_URL || GATEWAY_URL,
  notification: process.env.NEXT_PUBLIC_NOTIFICATION_API_URL || GATEWAY_URL,
  ocr: process.env.NEXT_PUBLIC_OCR_API_URL || GATEWAY_URL,
}

// Business records and registration numbers are owned by the API.
export class ApiRequestError extends Error {
  constructor(public readonly status: number, message: string, public readonly traceId?: string) {
    super(message)
    this.name = 'ApiRequestError'
  }
}
export type ApiEnvelope<T = any> = { success: true; data: T; message?: string | null; traceId?: string }
const nonempty = (value: unknown): value is string => typeof value === 'string' && value.trim().length > 0

type SessionSnapshot = { generation: number; accessToken: string | null; refreshToken: string | null }
type RefreshedTokens = { accessToken: string; refreshToken: string }
let sessionGeneration = 0
let lastRefresh: { snapshot: SessionSnapshot; promise: Promise<RefreshedTokens | null> } | null = null

function persistTokens(accessToken: string, refreshToken: string) {
  if (typeof window !== 'undefined') {
    localStorage.setItem('das_access_token', accessToken)
    localStorage.setItem('das_refresh_token', refreshToken)
  }
}

export const tokenManager = {
  getToken: (): string | null => {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('das_access_token')
    }
    return null
  },
  getRefreshToken: (): string | null => {
    if (typeof window !== 'undefined') {
      return localStorage.getItem('das_refresh_token')
    }
    return null
  },
  setTokens: (accessToken: string, refreshToken: string) => {
    sessionGeneration++
    lastRefresh = null
    persistTokens(accessToken, refreshToken)
  },
  clearTokens: () => {
    sessionGeneration++
    lastRefresh = null
    if (typeof window !== 'undefined') {
      localStorage.removeItem('das_access_token')
      localStorage.removeItem('das_refresh_token')
      localStorage.removeItem('das_user')
      localStorage.removeItem('das_documents_store')
      localStorage.removeItem('das_partners_store')
      // Pending task bodies live only in this tab's session and must not survive logout.
      try { for (let i = sessionStorage.length - 1; i >= 0; i--) { const key = sessionStorage.key(i); if (key?.startsWith('das_task_request:')) sessionStorage.removeItem(key) } } catch { /* Storage may be disabled; task creation then fails closed. */ }
      if (typeof document !== 'undefined') {
        document.cookie = 'das_access_token=; path=/; max-age=0'
      }
    }
  },
  getUser: () => {
    if (typeof window !== 'undefined') {
      const user = localStorage.getItem('das_user')
      return user ? JSON.parse(user) : null
    }
    return null
  },
  setUser: (user: any) => {
    if (typeof window !== 'undefined') {
      localStorage.setItem('das_user', JSON.stringify(user))
    }
  },
}

function sessionSnapshot(): SessionSnapshot {
  return { generation: sessionGeneration, accessToken: tokenManager.getToken(), refreshToken: tokenManager.getRefreshToken() }
}

function sessionMatches(snapshot: SessionSnapshot, tokens: { accessToken: string | null; refreshToken: string | null } = snapshot) {
  return snapshot.generation === sessionGeneration && tokens.accessToken === tokenManager.getToken() && tokens.refreshToken === tokenManager.getRefreshToken()
}

async function refreshSession(snapshot: SessionSnapshot): Promise<RefreshedTokens | null> {
  if (!snapshot.refreshToken || !sessionMatches(snapshot)) return null
  let session: RefreshedTokens | null = null

  try {
    const response = await fetch(`${API_URLS.auth}/api/auth/refresh`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: snapshot.refreshToken })
    })
    const envelope = response.ok ? await response.json() : null
    const candidate = envelope?.success === true ? envelope.data : null

    if (nonempty(candidate?.accessToken) && nonempty(candidate?.refreshToken))
      session = { accessToken: candidate.accessToken, refreshToken: candidate.refreshToken }
  } catch {
    // Preserve the original401; only its still-current session can be cleared.
  }
  if (!sessionMatches(snapshot)) return null
  if (!session) { tokenManager.clearTokens(); return null }
  // Rotation belongs to this generation; replacement/login/logout invalidates it.
  try {
    persistTokens(session.accessToken, session.refreshToken)
  } catch {
    // A failed second write leaves the new access token paired with the old
    // refresh token. Discard only this owned commit; never restore a token the
    // authority has already rotated or clear a replacement login.
    const partial = { accessToken: session.accessToken, refreshToken: snapshot.refreshToken }

    if (sessionMatches(snapshot) || sessionMatches(snapshot, partial)) tokenManager.clearTokens()
    return null
  }
  return session
}

function sharedRefresh(snapshot: SessionSnapshot) {
  const previous = lastRefresh

  if (previous && previous.snapshot.generation === snapshot.generation && previous.snapshot.accessToken === snapshot.accessToken && previous.snapshot.refreshToken === snapshot.refreshToken)
    return previous.promise
  if (!sessionMatches(snapshot)) return Promise.resolve(null)
  const promise = refreshSession(snapshot)

  lastRefresh = { snapshot, promise }
  return promise
}

function waitForRefresh(promise: Promise<RefreshedTokens | null>, signal?: AbortSignal | null) {
  if (!signal) return promise
  if (signal.aborted) return Promise.reject(new DOMException('Request cancelled', 'AbortError'))

  return new Promise<RefreshedTokens | null>((resolve, reject) => {
    const abort = () => { cleanup(); reject(new DOMException('Request cancelled', 'AbortError')) }
    const cleanup = () => signal.removeEventListener('abort', abort)

    signal.addEventListener('abort', abort, { once: true })
    promise.then(value => { cleanup(); resolve(value) }, error => { cleanup(); reject(error) })
  })
}

// Base fetch wrapper with auth
async function apiFetch(baseUrl: string, endpoint: string, options: RequestInit = {}) {
  options.signal?.throwIfAborted()
  const headers = new Headers(options.headers)
  const snapshot = sessionSnapshot()
  const token = snapshot.accessToken

  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (!(options.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  const url = `${baseUrl}${endpoint}`
  const response = await fetch(url, { ...options, headers })

  if (response.status === 401) {
    options.signal?.throwIfAborted()
    const session = await waitForRefresh(sharedRefresh(snapshot), options.signal)

    if (session && sessionMatches(snapshot, session)) {
      options.signal?.throwIfAborted()
      headers.set('Authorization', `Bearer ${session.accessToken}`)
      // A retry may have committed a write before losing its response. Preserve
      // the network failure so callers can reconcile rather than report 401.
      const retry = await fetch(url, { ...options, headers })

      if (retry.status === 401 && sessionMatches(snapshot, session)) tokenManager.clearTokens()
      return retry
    }
    if (sessionMatches(snapshot)) tokenManager.clearTokens()
  }
  return response
}

export async function requestApiEnvelope<T = any>(service: keyof typeof API_URLS, endpoint: string, options: RequestInit = {}): Promise<ApiEnvelope<T>> {
  let response: Response

  try { response = await apiFetch(API_URLS[service], endpoint, options) }
  catch (error) {
    if ((error as { name?: string }).name === 'AbortError') throw error
    throw new ApiRequestError(0, 'Không thể kết nối đến máy chủ. Vui lòng thử lại.')
  }
  let result: any

  try { result = await response.json() }
  catch (error) {
    if ((error as { name?: string }).name === 'AbortError') throw error
    throw new ApiRequestError(response.ok ? 502 : response.status, 'Phản hồi từ máy chủ không hợp lệ.')
  }
  if (!response.ok || result?.success !== true) {
    const message = response.status >= 500 ? 'Dịch vụ đang không khả dụng. Vui lòng thử lại.'
      : nonempty(result?.message) ? result.message : 'Không thể hoàn thành yêu cầu.'

    throw new ApiRequestError(response.ok ? 502 : response.status, message, typeof result?.traceId === 'string' ? result.traceId : undefined)
  }
  return result
}

// Authenticated content request; never put a bearer token into a URL/iframe.
export async function requestReportWorkbook(endpoint: string, signal?: AbortSignal): Promise<Blob> {
  const response = await apiFetch(API_URLS.document, endpoint, { signal, cache: 'no-store', redirect: 'error' })
  if (!response.ok) throw new ApiRequestError(response.status, 'Không thể tải báo cáo.')
  if (response.headers.get('content-type')?.split(';')[0] !== 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet') throw new ApiRequestError(502, 'Định dạng báo cáo không hợp lệ.')
  const maximum = 5 * 1024 * 1024
  if (Number(response.headers.get('content-length')) > maximum || !response.body) throw new ApiRequestError(502, 'Báo cáo vượt giới hạn tải.')
  const reader = response.body.getReader(), chunks: Uint8Array<ArrayBuffer>[] = []
  let length = 0
  try {
    while (true) {
      const result = await reader.read()
      if (result.done) break
      length += result.value.byteLength
      if (length > maximum) { await reader.cancel(); throw new ApiRequestError(502, 'Báo cáo vượt giới hạn tải.') }
      chunks.push(new Uint8Array(result.value))
    }
  } finally { reader.releaseLock() }
  const blob = new Blob(chunks, { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
  const signature = new Uint8Array(await blob.slice(0, 4).arrayBuffer())
  if (signature[0] !== 80 || signature[1] !== 75 || signature[2] !== 3 || signature[3] !== 4) throw new ApiRequestError(502, 'Tệp báo cáo không hợp lệ.')
  return blob
}

export async function requestPdfBytes(fileId: string, signal?: AbortSignal): Promise<Blob> {
  let response: Response

  try { response = await apiFetch(API_URLS.files, `/api/files/${fileId}`, { signal, cache: 'no-store', redirect: 'error' }) }
  catch (error) {
    if ((error as { name?: string }).name === 'AbortError') throw error
    throw new ApiRequestError(0, 'Không thể tải PDF.')
  }
  if (!response.ok) throw new ApiRequestError(response.status, 'Không thể đọc PDF. Hãy tải lại công văn để kiểm tra quyền và trạng thái tệp.')
  const limit = 25 * 1024 * 1024

  if (response.headers.get('content-type')?.split(';')[0].trim() !== 'application/pdf' || Number(response.headers.get('content-length') ?? 0) > limit || !response.body)
    throw new ApiRequestError(502, 'Phản hồi PDF không hợp lệ.')
  const reader = response.body.getReader(), chunks: Uint8Array<ArrayBuffer>[] = []
  let length = 0

  try {
    while (true) {
      const { value, done } = await reader.read()

      if (done) break
      length += value.byteLength
      if (length > limit) throw new ApiRequestError(502, 'PDF vượt giới hạn kích thước.')
      chunks.push(new Uint8Array(value))
    }
  } catch (error) { await reader.cancel(); throw error }
  finally { reader.releaseLock() }
  const blob = new Blob(chunks, { type: 'application/pdf' })

  if ((await blob.slice(0, 5).text()) !== '%PDF-') throw new ApiRequestError(502, 'Phản hồi không phải PDF.')
  return blob
}

const requireRecord = (envelope: ApiEnvelope): ApiEnvelope => {
  if (!envelope.data || !nonempty(envelope.data.id)) throw new ApiRequestError(502, 'Phản hồi thiếu định danh do máy chủ cấp.')
  return envelope
}
const requireList = (envelope: ApiEnvelope): ApiEnvelope => {
  const data = envelope.data

  if (Array.isArray(data) || (data && Array.isArray(data.items) && Number.isInteger(data.totalCount) && data.totalCount >= 0)) return envelope
  throw new ApiRequestError(502, 'Phản hồi danh sách không hợp lệ.')
}

// Auth API
export const authApi = {
  login: async (userName: string, password: string) => {
    tokenManager.clearTokens()
    const attempt = sessionSnapshot()

    try {
      const session = await requestLegacyLogin(API_URLS.auth, userName, password)

      if (!sessionMatches(attempt)) throw new ApiRequestError(409, 'Yêu cầu đăng nhập đã hết hiệu lực. Vui lòng đăng nhập lại nếu cần.')
      // These writes are synchronous. Roll back an owned commit if storage fails,
      // including failures after setTokens has advanced the generation.
      try {
        tokenManager.setTokens(session.accessToken, session.refreshToken)
        tokenManager.setUser({ ...session.user, role: session.user.roles[0] ?? null })
      } catch {
        tokenManager.clearTokens()
        throw new ApiRequestError(0, 'Không thể lưu phiên đăng nhập. Vui lòng kiểm tra quyền lưu trữ của trình duyệt.')
      }
      return session
    } catch (error) {
      if (sessionMatches(attempt)) tokenManager.clearTokens()
      throw error
    }
  },
  logout: async () => {
    const snapshot = sessionSnapshot()

    tokenManager.clearTokens()
    try {
      await fetch(`${API_URLS.auth}/api/auth/logout`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...(snapshot.accessToken ? { Authorization: `Bearer ${snapshot.accessToken}` } : {}) },
        body: JSON.stringify({ refreshToken: snapshot.refreshToken }),
      })
    } catch {}
  },
  me: async () => {
    const res = await apiFetch(API_URLS.auth, '/api/auth/me')
    return res.json()
  },
  getUsers: async (role?: string) => {
    const query = role ? `?role=${role}` : ''
    const res = await apiFetch(API_URLS.auth, `/api/users${query}`)
    return res.json()
  },
}

// Legacy document adapter; scoped v2 lists use their own endpoint.
export const documentApi = {
  getList: async (filter?: any) => {
    const params = new URLSearchParams()
    const hasV2Scope = filter?.kind !== undefined || filter?.view !== undefined

    if (hasV2Scope) {
      if (!['Incoming', 'Outgoing', 'Internal'].includes(filter.kind) || !['all', 'mine', 'department', 'cancelled'].includes(filter.view))
        throw new ApiRequestError(400, 'Loại công văn hoặc phạm vi xem không hợp lệ.')
      params.set('kind', filter.kind)
      params.set('view', filter.view)
      const pageNumber = filter.pageNumber ?? 1, pageSize = filter.pageSize ?? 20

      if (!Number.isSafeInteger(pageNumber) || pageNumber < 1 || !Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100)
        throw new ApiRequestError(400, 'Thông tin phân trang không hợp lệ.')
      if (filter.status && !['InProgress', 'Distributed', 'Cancelled'].includes(filter.status))
        throw new ApiRequestError(400, 'Trạng thái công văn không hợp lệ.')
      params.set('pageNumber', String(pageNumber))
      params.set('pageSize', String(pageSize))
    } else if (filter?.direction) {
      const legacyKind: Record<string,string> = { incoming: 'INCOMING', outgoing: 'OUTGOING', internal: 'INTERNAL' }

      if (!legacyKind[filter.direction]) throw new ApiRequestError(400, 'Loại công văn không hợp lệ.')
      params.set('docType', legacyKind[filter.direction])
    }
    for (const key of ['searchTerm', 'status', 'pageNumber', 'pageSize']) if (filter?.[key] !== undefined && filter[key] !== '') params.set(key, String(filter[key]))
    const response = requireList(await requestApiEnvelope('document', `${hasV2Scope ? '/api/v2/documents' : '/api/documents'}?${params}`, { signal: filter?.signal, cache: 'no-store' }))

    if (hasV2Scope) {
      const page = response.data as DocumentPage | null
      const optionalText = (value: unknown) => value === undefined || value === null || typeof value === 'string'

      if (!page || Array.isArray(page) || !Array.isArray(page.items) || !Number.isSafeInteger(page.totalCount) ||
          page.totalCount < page.items.length || page.pageNumber !== (filter.pageNumber ?? 1) || page.pageSize !== (filter.pageSize ?? 20) ||
          page.items.length > page.pageSize || !page.items.every(item => item && nonempty(item.id) && nonempty(item.registrationNumber) &&
            typeof item.subject === 'string' && item.kind === filter.kind && ['InProgress', 'Distributed', 'Cancelled'].includes(item.status) &&
            (filter.view !== 'cancelled' || item.status === 'Cancelled') && (!filter.status || item.status === filter.status) &&
            (item.allowedActions === undefined || (Array.isArray(item.allowedActions) && item.allowedActions.every(action => typeof action === 'string'))) &&
            (item.isComplete === undefined || typeof item.isComplete === 'boolean') &&
            [item.referenceNumber, item.issuedDate, item.registrationDate, item.senderPartnerName, item.companyCode, item.departmentName, item.inputterDisplayName].every(optionalText) &&
            (item.sensitivity === undefined || ['Normal', 'Confidential'].includes(item.sensitivity))))
        throw new ApiRequestError(502, 'Phản hồi danh sách công văn không hợp lệ.')
    }
    return response
  },
  getScopedList: async (query: DocumentListQuery, signal?: AbortSignal): Promise<DocumentPage> =>
    (await documentApi.getList({ ...query, signal })).data,
  getById: async (id: string, signal?: AbortSignal) => requireRecord(await requestApiEnvelope('document', `/api/documents/${encodeURIComponent(id)}`, { signal, cache: 'no-store' })),
  // This legacy write route is replaced with typed v2 contracts at G3.
  // Do not allocate registration numbers or report browser-only saves.
  create: async (data: any): Promise<{ success: boolean; data?: { id: string; documentNumber: string; [key: string]: unknown }; message?: string }> => {
    try {
      const response = await apiFetch(API_URLS.document, '/api/documents', {
        method: 'POST',
        body: JSON.stringify(data)
      })
      if (!response.ok) {
        return { success: false, message: `Không thể lưu công văn (HTTP ${response.status}).` }
      }
      const result = await response.json()
      if (result?.success !== true || typeof result.data?.id !== 'string' || !result.data.id.trim() ||
          typeof result.data.documentNumber !== 'string' || !result.data.documentNumber.trim()) {
        return { success: false, message: 'Phản hồi lưu công văn không hợp lệ.' }
      }
      return { success: true, data: result.data }
    } catch {
      return { success: false, message: 'Không thể kết nối hoặc nhận phản hồi hợp lệ từ dịch vụ công văn.' }
    }
  },
  update: async (id: string, data: any) => requireRecord(await requestApiEnvelope('document', `/api/documents/${encodeURIComponent(id)}`, {
    method: 'PUT', body: JSON.stringify(data)
  })),
  delete: async (id: string) => requestApiEnvelope('document', `/api/documents/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

// Partner API (Persistent with Live Backend Sync)
export const partnerApi = {
  getList: async (filter?: any) => {
    const params = new URLSearchParams()

    for (const key of ['searchTerm', 'entityType', 'isActive', 'pageNumber', 'pageSize'])
      if (filter?.[key] !== undefined && filter[key] !== '') params.set(key, String(filter[key]))
    return requireList(await requestApiEnvelope('partner', `/api/partners?${params}`, { signal: filter?.signal }))
  },
  getById: async (id: string) => requireRecord(await requestApiEnvelope('partner', `/api/partners/${encodeURIComponent(id)}`)),
  create: async (data: any) => requireRecord(await requestApiEnvelope('partner', '/api/partners', { method: 'POST', body: JSON.stringify(data) })),
  update: async (id: string, data: any) => requireRecord(await requestApiEnvelope('partner', `/api/partners/${encodeURIComponent(id)}`, {
    method: 'PUT', body: JSON.stringify(data)
  })),
  delete: async (id: string) => requestApiEnvelope('partner', `/api/partners/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

// Files API
export const fileApi = {
  upload: async (file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    const res = await apiFetch(API_URLS.files, '/api/files/upload', {
      method: 'POST',
      body: formData,
    })
    return res.json()
  },
  download: (fileId: string) => {
    return `${API_URLS.files}/api/files/${fileId}`
  },
}

// OCR API
export const ocrApi = {
  analyze: async (fileId: string, senderEmail?: string) => {
    const body: Record<string, string> = { fileId }
    if (senderEmail) body.senderEmail = senderEmail
    const res = await apiFetch(API_URLS.ocr, '/api/ai-ocr/analyze', {
      method: 'POST',
      body: JSON.stringify(body),
    })
    return res.json()
  },
}
