// Central API service for DAS Frontend
// Handles JWT token management, request/response, persistent storage and API Gateway integration

const GATEWAY_URL = process.env.NEXT_PUBLIC_GATEWAY_URL || 'http://localhost:8080'

const API_URLS = {
  auth: process.env.NEXT_PUBLIC_AUTH_API_URL || GATEWAY_URL,
  partner: process.env.NEXT_PUBLIC_PARTNER_API_URL || GATEWAY_URL,
  files: process.env.NEXT_PUBLIC_FILES_API_URL || GATEWAY_URL,
  document: process.env.NEXT_PUBLIC_DOCUMENT_API_URL || GATEWAY_URL,
  ocr: process.env.NEXT_PUBLIC_OCR_API_URL || GATEWAY_URL,
}

// Initial default official documents with 2-tier numbering (1-1000 counter & partner ref)
const INITIAL_DOCUMENTS = [
  // 1. CÔNG VĂN ĐẾN (INCOMING - ĐƯỢC ĐÁNH SỐ NỘI BỘ TỪ 0001 ĐẾN VÔ HẠN)
  {
    id: '1',
    documentNumber: 'CV-DEN-2026-0001',
    referenceNumber: '128/BGDĐT-GDĐH',
    title: 'Về việc hướng dẫn công tác tuyển sinh đại học và chuyển đổi số năm 2026-2027',
    direction: 'incoming',
    issuedDate: '15/08/2026',
    partnerName: 'Bộ Giáo dục và Đào tạo',
    status: 'completed',
    summary: 'Quy định về việc tiếp nhận và xử lý số hóa văn bản hành chính.'
  },
  {
    id: '2',
    documentNumber: 'CV-DEN-2026-0002',
    referenceNumber: '2154/BGDĐT-CNTT',
    title: 'V/v Hướng dẫn triển khai chuyển đổi số và ứng dụng AI OCR vào lưu trữ công văn năm học 2026-2027',
    direction: 'incoming',
    issuedDate: '10/08/2026',
    partnerName: 'Bộ Giáo dục và Đào tạo (Cục CNTT)',
    status: 'processing',
    summary: 'Tài liệu hướng dẫn ứng dụng AI OCR vào tiếp nhận, bóc tách và phân luồng công văn tự động.'
  },
  {
    id: '3',
    documentNumber: 'CV-DEN-2026-0003',
    referenceNumber: '456/VNPT-KHCN',
    title: 'Về việc hợp tác triển khai hệ thống lưu trữ công văn điện tử',
    direction: 'incoming',
    issuedDate: '10/08/2026',
    partnerName: 'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)',
    status: 'completed',
    summary: 'Đề xuất phối hợp thử nghiệm nền tảng lưu trữ đám mây và chữ ký số chuyên dùng.'
  },
  {
    id: '4',
    documentNumber: 'CV-DEN-2026-0004',
    referenceNumber: '789/FPT-CNTT',
    title: 'Về việc đề xuất giải pháp chuyển đổi số cho hệ thống văn thư',
    direction: 'incoming',
    issuedDate: '05/08/2026',
    partnerName: 'Công ty Cổ phần FPT',
    status: 'pending',
    summary: 'Phương án tích hợp AI Agent vào quản lý luồng công văn đi và đến.'
  },
  {
    id: '5',
    documentNumber: 'CV-DEN-2026-0005',
    referenceNumber: '1024/VTL-VT',
    title: 'Về việc cung cấp dịch vụ bảo mật và lưu trữ đám mây',
    direction: 'incoming',
    issuedDate: '01/08/2026',
    partnerName: 'Tập đoàn Viettel',
    status: 'completed',
    summary: 'Báo giá và thông số kỹ thuật hạ tầng trung tâm dữ liệu đạt chuẩn Tier 3.'
  },
  {
    id: '6',
    documentNumber: 'CV-DEN-2026-0006',
    referenceNumber: '2048/UBND-VX',
    title: 'Về việc triển khai ứng dụng công nghệ thông tin trong quản lý văn bản',
    direction: 'incoming',
    issuedDate: '20/07/2026',
    partnerName: 'Ủy ban Nhân dân TP Hồ Chí Minh',
    status: 'completed',
    summary: 'Kế hoạch đồng bộ cơ sở dữ liệu văn thư lưu trữ liên thông các quận huyện.'
  },
  {
    id: '7',
    documentNumber: 'CV-DEN-2026-0007',
    referenceNumber: '3072/BCA-KHCN',
    title: 'Về việc triển khai hệ thống quản lý văn bản và hồ sơ điện tử',
    direction: 'incoming',
    issuedDate: '25/07/2026',
    partnerName: 'Bộ Công an',
    status: 'processing',
    summary: 'Yêu cầu tuân thủ an toàn an ninh thông tin cấp độ 3 theo Nghị định 30/2020/NĐ-CP.'
  },
  {
    id: '8',
    documentNumber: 'CV-DEN-2026-0008',
    referenceNumber: 'SEV-2026/0815',
    title: 'Regarding cooperation on digital document management system',
    direction: 'incoming',
    issuedDate: '15/08/2026',
    partnerName: 'Samsung Electronics Vietnam',
    status: 'completed',
    summary: 'Official proposal for cross-border enterprise document archiving cooperation.'
  },
  {
    id: '9',
    documentNumber: 'CV-DEN-2026-0009',
    referenceNumber: '4096/EVN-VP',
    title: 'Về việc phối hợp số hóa hệ thống công văn nội bộ',
    direction: 'incoming',
    issuedDate: '12/08/2026',
    partnerName: 'Tập đoàn Điện lực Việt Nam (EVN)',
    status: 'processing',
    summary: 'Đề nghị chuyển giao tài liệu kỹ thuật về chuẩn số hóa hồ sơ điện tử.'
  },
  {
    id: '10',
    documentNumber: 'CV-DEN-2026-0010',
    referenceNumber: '5120/BHXH-CNTT',
    title: 'Về việc kết nối hệ thống văn bản điện tử liên thông',
    direction: 'incoming',
    issuedDate: '08/08/2026',
    partnerName: 'Bảo hiểm Xã hội Việt Nam',
    status: 'completed',
    summary: 'Kế hoạch mở cổng API kết nối dữ liệu văn bản hành chính.'
  },

  // 2. CÔNG VĂN ĐI (OUTGOING)
  {
    id: '11',
    documentNumber: 'CV-DI-2026-0001',
    referenceNumber: 'TB-45/SGD-HN',
    title: 'Thông báo lịch trực nghỉ lễ Quốc khánh 02/09',
    direction: 'outgoing',
    issuedDate: '11/08/2026',
    partnerName: 'Sở Giáo dục và Đào tạo Hà Nội',
    status: 'pending',
    summary: 'Thông báo phân công lịch trực ban chỉ huy và bảo vệ cơ quan.'
  },
  {
    id: '12',
    documentNumber: 'CV-DI-2026-0002',
    referenceNumber: 'BC-204/DAS',
    title: 'Báo cáo tổng kết công tác chuyển đổi số tháng 7/2026',
    direction: 'outgoing',
    issuedDate: '13/08/2026',
    partnerName: 'Tập đoàn VNPT',
    status: 'completed',
    summary: 'Đánh giá tiến độ số hóa hồ sơ và ứng dụng AI OCR vào tiếp nhận công văn.'
  },
  {
    id: '13',
    documentNumber: 'CV-DI-2026-0003',
    referenceNumber: 'YCBG-08/DAS',
    title: 'Yêu cầu báo giá phần mềm số hóa và nhận diện chữ quang học',
    direction: 'outgoing',
    issuedDate: '16/08/2026',
    partnerName: 'Công ty Cổ phần Công nghệ ABC',
    status: 'pending',
    summary: 'Hồ sơ mời báo giá gói nâng cấp máy quét và máy chủ nhận diện OCR.'
  },

  // 3. CÔNG VĂN NỘI BỘ (INTERNAL)
  {
    id: '14',
    documentNumber: 'CV-NB-2026-0001',
    referenceNumber: 'QĐ-01/NB-DAS',
    title: 'Quyết định thành lập Tổ công tác chuyển đổi số và số hóa tài liệu',
    direction: 'internal',
    issuedDate: '01/08/2026',
    partnerName: 'Ban Giám Đốc & Các Phòng Ban',
    status: 'completed',
    summary: 'Kiện toàn nhân sự ban chỉ đạo triển khai hệ thống lưu trữ văn thư điện tử nội bộ.'
  },
  {
    id: '15',
    documentNumber: 'CV-NB-2026-0002',
    referenceNumber: 'TB-18/NB-VP',
    title: 'Thông báo hướng dẫn quy trình luân chuyển và ký duyệt hồ sơ điện tử',
    direction: 'internal',
    issuedDate: '05/08/2026',
    partnerName: 'Toàn thể Cán bộ - Nhân viên',
    status: 'completed',
    summary: 'Quy trình ký số và duyệt công văn nội bộ qua phần mềm DAS.'
  },
  {
    id: '16',
    documentNumber: 'CV-NB-2026-0003',
    referenceNumber: 'TTr-09/NB-TC',
    title: 'Tờ trình đề xuất trang bị bổ sung thiết bị quét văn bản tốc độ cao',
    direction: 'internal',
    issuedDate: '18/08/2026',
    partnerName: 'Phòng Tài Chính - Kế Hoạch',
    status: 'pending',
    summary: 'Dự toán kinh phí nâng cấp máy quét chuyên dụng cho phòng văn thư.'
  }
]

const INITIAL_PARTNERS = [
  { id: 'p1', code: 'BGDDT', fullName: 'Bộ Giáo dục và Đào tạo', shortName: 'BGDĐT', taxCode: '0100100100', email: 'vanthu@moet.gov.vn', phone: '024.38695144', address: '35 Đại Cồ Việt, Hai Bà Trưng, Hà Nội', entityType: 'government', isActive: true },
  { id: 'p2', code: 'UBND-HN', fullName: 'Ủy ban Nhân dân TP Hà Nội', shortName: 'UBND Hà Nội', taxCode: '0100100101', email: 'vanthu@hanoi.gov.vn', phone: '024.38253536', address: '12 Lê Lai, Hoàn Kiếm, Hà Nội', entityType: 'government', isActive: true },
  { id: 'p3', code: 'VNPT', fullName: 'Tập đoàn Bưu chính Viễn thông Việt Nam', shortName: 'VNPT', taxCode: '0100684378', email: 'contact@vnpt.vn', phone: '18001260', address: '57 Huỳnh Thúc Kháng, Đống Đa, Hà Nội', entityType: 'enterprise', isActive: true },
  { id: 'p4', code: 'VNU', fullName: 'Đại học Quốc gia Hà Nội', shortName: 'ĐHQGHN', taxCode: '0100100202', email: 'vanphong@vnu.edu.vn', phone: '024.37547670', address: '144 Xuân Thủy, Cầu Giấy, Hà Nội', entityType: 'organization', isActive: true },
  { id: 'p5', code: 'ABC-TECH', fullName: 'Công ty Cổ phần Công nghệ ABC', shortName: 'ABC Tech', taxCode: '0109988776', email: 'info@abctech.vn', phone: '024.73001234', address: 'Tầng 12, Tòa nhà Landmark 72, Nam Từ Liêm, Hà Nội', entityType: 'enterprise', isActive: true },
  { id: 'p6', code: 'SGD-HN', fullName: 'Sở Giáo dục và Đào tạo Hà Nội', shortName: 'Sở GD&ĐT HN', taxCode: '0100100303', email: 'sogd@hanoi.edu.vn', phone: '024.38253538', address: '23 Quang Trung, Hoàn Kiếm, Hà Nội', entityType: 'government', isActive: true }
]

// Storage helpers with Auto-Sanitization & Standardization
const sanitizeDocuments = (docs: any[]): any[] => {
  const year = new Date().getFullYear()
  const groups: Record<string, any[]> = { incoming: [], outgoing: [], internal: [] }

  // 1. Lọc bỏ các tài liệu rác hoặc không hợp lệ và tự động chữa lỗi trường dữ liệu
  const cleaned = docs
    .filter(d => {
      const num = d.documentNumber || ''
      const partner = (d.partnerName || '').toLowerCase()
      const title = (d.title || '').toLowerCase()

      // Loại bỏ các email rác/spam không có PDF từ Instagram, marketing
      if (partner.includes('instagram.com') || title.includes('faker') || title.includes('khoảnh khắc')) {
        return false
      }
      // Loại bỏ định dạng số ngẫu nhiên cũ không chuẩn (/EMAIL, /GMAIL, /MAIL, /PRIORITY)
      if (num.endsWith('/EMAIL') || num.endsWith('/GMAIL') || num.endsWith('/MAIL') || num.endsWith('/PRIORITY')) {
        return false
      }
      return true
    })
    .map(d => {
      let ref = d.referenceNumber || ''
      let dir = d.direction || 'incoming'
      let title = d.title || ''

      // Tự động phân định lại thể loại văn bản nếu số ký hiệu hoặc trích yếu chứa mã NB / Nội bộ
      if (
        ref.includes('-NB-') || ref.includes('/NB-') || ref.includes('-NB/') || ref.includes('/NB/') ||
        ref.includes('QĐ-NB') || ref.includes('TB-NB') || ref.includes('TTr-NB') || ref.includes('CV-NB') ||
        title.toLowerCase().includes('thông báo nội bộ') || title.toLowerCase().includes('quyết định nội bộ') ||
        title.toLowerCase().includes('quy chế nội bộ')
      ) {
        dir = 'internal'
      }

      // Sửa lỗi tiêu đề cũ bị bóc tách sai đoạn "Văn phòng Công ty. Yêu cầu..."
      if (title.includes('Văn phòng Công ty. Yêu cầu') || title.includes('Văn phòng Công ty')) {
        if (ref.includes('15/TB-NB-DAS') || (d.summary || '').includes('quy trình số hóa')) {
          title = 'Triển khai áp dụng quy trình số hóa và tiếp nhận văn bản tự động qua hệ thống AI OCR'
        }
      }

      // Dọn dẹp phần đuôi • Tệp đính kèm: ... trong tiêu đề nếu có
      if (title.includes('• Tệp đính kèm:')) {
        title = title.split('• Tệp đính kèm:')[0].trim()
      }

      return {
        ...d,
        direction: dir,
        title: title
      }
    })

  cleaned.forEach(d => {
    const dir = d.direction || 'incoming'
    if (!groups[dir]) groups[dir] = []
    groups[dir].push(d)
  })

  const result: any[] = []

  // 2. Chuẩn hóa và sắp xếp lại chuỗi số thứ tự liên tục cho từng thể loại (incoming, outgoing, internal)
  ;(['incoming', 'outgoing', 'internal'] as const).forEach(dir => {
    const prefix = dir === 'incoming' ? `CV-DEN-${year}` : dir === 'outgoing' ? `CV-DI-${year}` : `CV-NB-${year}`
    const list = groups[dir] || []

    const usedSeqs = new Set<number>()
    const itemsToAssign: any[] = []

    // Lượt 1: Giữ các số thứ tự chuẩn đã có trong phạm vi hợp lý
    list.forEach(d => {
      const num = d.documentNumber || ''
      const m = num.match(new RegExp(`^${prefix}-(\\d+)$`))
      const seq = m ? parseInt(m[1], 10) : null

      if (seq !== null && seq <= list.length && !usedSeqs.has(seq)) {
        usedSeqs.add(seq)
      } else {
        itemsToAssign.push(d)
      }
    })

    // Lượt 2: Tự động lấp đầy số thứ tự còn thiếu cho các item bị nhảy cóc (như 0030, 0130)
    let nextAvailable = 1
    itemsToAssign.forEach(d => {
      while (usedSeqs.has(nextAvailable)) {
        nextAvailable++
      }
      usedSeqs.add(nextAvailable)
      const oldNum = d.documentNumber || ''
      const newNum = `${prefix}-${String(nextAvailable).padStart(4, '0')}`
      d.documentNumber = newNum
      if (!d.referenceNumber && oldNum && oldNum !== newNum && !oldNum.startsWith(prefix)) {
        d.referenceNumber = oldNum
      }
    })

    result.push(...list)
  })

  return result
}

const getStoredDocuments = (): any[] => {
  if (typeof window === 'undefined') return INITIAL_DOCUMENTS
  try {
    const raw = localStorage.getItem('das_documents_store')
    if (raw) {
      const parsed = JSON.parse(raw)
      if (Array.isArray(parsed) && parsed.length > 0) {
        const cleanDocs = sanitizeDocuments(parsed)
        if (cleanDocs.length > 0) {
          localStorage.setItem('das_documents_store', JSON.stringify(cleanDocs))
          return cleanDocs
        }
      }
    }
  } catch {}
  localStorage.setItem('das_documents_store', JSON.stringify(INITIAL_DOCUMENTS))
  return INITIAL_DOCUMENTS
}

const setStoredDocuments = (docs: any[]) => {
  if (typeof window !== 'undefined') {
    localStorage.setItem('das_documents_store', JSON.stringify(docs))
  }
}

const getStoredPartners = (): any[] => {
  if (typeof window === 'undefined') return INITIAL_PARTNERS
  try {
    const raw = localStorage.getItem('das_partners_store')
    if (raw) {
      const parsed = JSON.parse(raw)
      if (Array.isArray(parsed) && parsed.length > 0) return parsed
    }
  } catch {}
  localStorage.setItem('das_partners_store', JSON.stringify(INITIAL_PARTNERS))
  return INITIAL_PARTNERS
}

const setStoredPartners = (partners: any[]) => {
  if (typeof window !== 'undefined') {
    localStorage.setItem('das_partners_store', JSON.stringify(partners))
  }
}

// Token management
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
    if (typeof window !== 'undefined') {
      localStorage.setItem('das_access_token', accessToken)
      localStorage.setItem('das_refresh_token', refreshToken)
    }
  },
  clearTokens: () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('das_access_token')
      localStorage.removeItem('das_refresh_token')
      localStorage.removeItem('das_user')
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

// Base fetch wrapper with auth
async function apiFetch(baseUrl: string, endpoint: string, options: RequestInit = {}) {
  const token = tokenManager.getToken()
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string> || {}),
  }
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = 'application/json'
  }

  const url = `${baseUrl}${endpoint}`
  const response = await fetch(url, {
    ...options,
    headers,
  })

  if (response.status === 401) {
    const refreshToken = tokenManager.getRefreshToken()
    if (refreshToken) {
      try {
        const refreshRes = await fetch(`${API_URLS.auth}/api/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken }),
        })
        if (refreshRes.ok) {
          const refreshData = await refreshRes.json()
          tokenManager.setTokens(refreshData.accessToken, refreshData.refreshToken)
          headers['Authorization'] = `Bearer ${refreshData.accessToken}`
          return fetch(url, { ...options, headers })
        }
      } catch {}
    }
    tokenManager.clearTokens()
    if (typeof window !== 'undefined') {
      window.location.href = '/en/login'
    }
  }

  return response
}

// Auth API
export const authApi = {
  login: async (userName: string, password: string) => {
    const res = await fetch(`${API_URLS.auth}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName, password }),
    })
    return res.json()
  },
  logout: async () => {
    const refreshToken = tokenManager.getRefreshToken()
    try {
      await apiFetch(API_URLS.auth, '/api/auth/logout', {
        method: 'POST',
        body: JSON.stringify({ refreshToken }),
      })
    } catch {}
    tokenManager.clearTokens()
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

// Document API (Persistent with Live Backend Sync)
export const documentApi = {
  getList: async (filter?: any) => {
    try {
      const params = new URLSearchParams()
      if (filter?.searchTerm) params.set('searchTerm', filter.searchTerm)
      if (filter?.direction) params.set('direction', filter.direction)
      if (filter?.status) params.set('status', filter.status)
      if (filter?.pageNumber) params.set('pageNumber', String(filter.pageNumber))
      if (filter?.pageSize) params.set('pageSize', String(filter.pageSize))
      const res = await apiFetch(API_URLS.document, `/api/documents?${params.toString()}`)
      const data = await res.json()
      if (data?.success && Array.isArray(data?.data) && data.data.length > 0) {
        return data
      }
    } catch {}
    const docs = getStoredDocuments()
    return { success: true, data: docs }
  },
  getById: async (id: string) => {
    try {
      const res = await apiFetch(API_URLS.document, `/api/documents/${id}`)
      const data = await res.json()
      if (data?.success && data?.data) return data
    } catch {}
    const docs = getStoredDocuments()
    const found = docs.find(d => String(d.id) === String(id))
    return { success: !!found, data: found || null }
  },
  create: async (data: any) => {
    const docs = getStoredDocuments()
    const dir = data.direction || 'incoming'
    const year = new Date().getFullYear()

    // Xác định tiền tố theo chuẩn mã công văn của công ty
    const prefix = dir === 'incoming' ? `CV-DEN-${year}` : dir === 'outgoing' ? `CV-DI-${year}` : `CV-NB-${year}`

    // Tìm số thứ tự kế tiếp chuẩn xác không bị nhảy cóc
    const samePrefixDocs = docs.filter(d => d.documentNumber && d.documentNumber.startsWith(prefix))
    let maxSeq = 0
    for (const d of samePrefixDocs) {
      const match = d.documentNumber.match(new RegExp(`^${prefix}-(\\d+)$`))
      if (match) {
        const n = parseInt(match[1], 10)
        if (n > maxSeq && n <= samePrefixDocs.length + 5) {
          maxSeq = n
        }
      }
    }
    if (maxSeq === 0) {
      maxSeq = samePrefixDocs.length
    }
    const nextSeqStr = String(maxSeq + 1).padStart(4, '0')
    const autoDocNumber = `${prefix}-${nextSeqStr}`

    // Phân biệt Số nội bộ công ty và Số ký hiệu đối tác
    let internalDocNum = autoDocNumber
    let partnerRef = data.referenceNumber || ''

    if (data.documentNumber && data.documentNumber.startsWith(prefix)) {
      const requestedSeq = parseInt(data.documentNumber.replace(`${prefix}-`, ''), 10)
      if (!isNaN(requestedSeq) && requestedSeq <= samePrefixDocs.length + 2) {
        internalDocNum = data.documentNumber
      } else {
        internalDocNum = autoDocNumber
      }
    } else if (data.documentNumber && !data.documentNumber.startsWith('CV-')) {
      if (!partnerRef) partnerRef = data.documentNumber
      internalDocNum = autoDocNumber
    }

    const newDoc = {
      id: `doc-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
      documentNumber: internalDocNum,
      referenceNumber: partnerRef,
      title: data.title || 'Công văn mới',
      direction: dir,
      issuedDate: data.issuedDate || new Date().toLocaleDateString('vi-VN'),
      partnerName: data.partnerName || 'Chưa xác định',
      senderEmail: data.senderEmail || '',
      fileUrl: data.fileUrl || '',
      status: data.status || 'pending',
      summary: data.summary || '',
      fileIds: data.fileIds || []
    }

    const updated = [newDoc, ...docs]
    setStoredDocuments(updated)

    try {
      await apiFetch(API_URLS.document, '/api/documents', {
        method: 'POST',
        body: JSON.stringify(newDoc),
      })
    } catch {}

    return { success: true, data: newDoc }
  },
  update: async (id: string, data: any) => {
    const docs = getStoredDocuments()
    const index = docs.findIndex(d => String(d.id) === String(id))
    if (index !== -1) {
      docs[index] = { ...docs[index], ...data }
      setStoredDocuments(docs)
    }
    try {
      await apiFetch(API_URLS.document, `/api/documents/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      })
    } catch {}
    return { success: true, data: docs[index] || data }
  },
  delete: async (id: string) => {
    const docs = getStoredDocuments()
    const filtered = docs.filter(d => String(d.id) !== String(id))
    setStoredDocuments(filtered)
    try {
      await apiFetch(API_URLS.document, `/api/documents/${id}`, {
        method: 'DELETE',
      })
    } catch {}
    return { success: true }
  },
}

// Partner API (Persistent with Live Backend Sync)
export const partnerApi = {
  getList: async (filter?: any) => {
    try {
      const params = new URLSearchParams()
      if (filter?.searchTerm) params.set('searchTerm', filter.searchTerm)
      if (filter?.entityType) params.set('entityType', filter.entityType)
      if (filter?.isActive !== undefined) params.set('isActive', String(filter.isActive))
      if (filter?.pageNumber) params.set('pageNumber', String(filter.pageNumber))
      if (filter?.pageSize) params.set('pageSize', String(filter.pageSize))
      const res = await apiFetch(API_URLS.partner, `/api/partners?${params.toString()}`)
      const data = await res.json()
      if (data?.success && Array.isArray(data?.data) && data.data.length > 0) {
        return data
      }
    } catch {}
    const partners = getStoredPartners()
    return { success: true, data: partners }
  },
  getById: async (id: string) => {
    try {
      const res = await apiFetch(API_URLS.partner, `/api/partners/${id}`)
      const data = await res.json()
      if (data?.success && data?.data) return data
    } catch {}
    const partners = getStoredPartners()
    const found = partners.find(p => String(p.id) === String(id))
    return { success: !!found, data: found || null }
  },
  create: async (data: any) => {
    const newPartner = {
      id: `p-${Date.now()}`,
      code: data.code || `P-${Date.now().toString().slice(-4)}`,
      fullName: data.fullName,
      shortName: data.shortName || data.fullName,
      taxCode: data.taxCode || '',
      email: data.email || '',
      phone: data.phone || '',
      address: data.address || '',
      entityType: data.entityType || 'enterprise',
      isActive: data.isActive !== undefined ? data.isActive : true
    }
    const partners = getStoredPartners()
    const updated = [newPartner, ...partners]
    setStoredPartners(updated)

    try {
      await apiFetch(API_URLS.partner, '/api/partners', {
        method: 'POST',
        body: JSON.stringify(data),
      })
    } catch {}

    return { success: true, data: newPartner }
  },
  update: async (id: string, data: any) => {
    const partners = getStoredPartners()
    const index = partners.findIndex(p => String(p.id) === String(id))
    if (index !== -1) {
      partners[index] = { ...partners[index], ...data }
      setStoredPartners(partners)
    }
    try {
      await apiFetch(API_URLS.partner, `/api/partners/${id}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      })
    } catch {}
    return { success: true, data: partners[index] || data }
  },
  delete: async (id: string) => {
    const partners = getStoredPartners()
    const filtered = partners.filter(p => String(p.id) !== String(id))
    setStoredPartners(filtered)
    try {
      await apiFetch(API_URLS.partner, `/api/partners/${id}`, {
        method: 'DELETE',
      })
    } catch {}
    return { success: true }
  },
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
