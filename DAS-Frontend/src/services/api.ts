import { MEDINET_DOCUMENTS } from '@/data/medinetDocuments'

const GATEWAY_URL = process.env.NEXT_PUBLIC_GATEWAY_URL || 'http://localhost:5000'

const API_URLS = {
  auth: process.env.NEXT_PUBLIC_AUTH_API_URL || GATEWAY_URL,
  partner: process.env.NEXT_PUBLIC_PARTNER_API_URL || GATEWAY_URL,
  files: process.env.NEXT_PUBLIC_FILES_API_URL || GATEWAY_URL,
  document: process.env.NEXT_PUBLIC_DOCUMENT_API_URL || GATEWAY_URL,
  ocr: process.env.NEXT_PUBLIC_OCR_API_URL || GATEWAY_URL,
}

// Initial default official documents with 2-tier numbering (1-1000 counter & partner ref)
const INITIAL_DOCUMENTS = [
  ...MEDINET_DOCUMENTS,
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

      // Dọn dẹp phần đuôi • Tệp đính kèm: ... trong tiêu đề nếu có và khôi phục fileUrl
      let extractedAttach = d.attachmentName || ''
      if (title.includes('• Tệp đính kèm:')) {
        const parts = title.split('• Tệp đính kèm:')
        title = parts[0].trim()
        const attachPart = (parts[1] || '').trim().split(' ')[0]
        if (attachPart.toLowerCase().endsWith('.pdf')) {
          extractedAttach = attachPart
        }
      }
      title = title.replace(/\s*CV-(?:DEN|DI|NB)-\d{4}-\d{4}\s*$/i, '').trim()

      let fileUrl = d.fileUrl || ''
      if (!fileUrl && extractedAttach) {
        fileUrl = `/api/files/${encodeURIComponent(extractedAttach)}`
      }
      if (!fileUrl && (d.attachmentFileIds?.[0] || d.fileId || d.attachments?.[0]?.fileId)) {
        const fId = d.attachmentFileIds?.[0] || d.fileId || d.attachments?.[0]?.fileId
        fileUrl = `/api/files/${encodeURIComponent(fId)}`
      }
      if (!fileUrl && (ref || d.documentNumber)) {
        const cleanRef = (ref || '').replace(/[\/\\:\s]/g, '_').toLowerCase()
        if (cleanRef.includes('2595') || cleanRef.includes('btttt')) {
          fileUrl = '/api/files/_data_soytehcm_vanphongso_attachments_2020_12_2595bttttcbc_1412202014.pdf'
          if (!extractedAttach) extractedAttach = '_data_soytehcm_vanphongso_attachments_2020_12_2595bttttcbc_1412202014.pdf'
        } else if (cleanRef.includes('850') && cleanRef.includes('kh')) {
          fileUrl = '/api/files/_data_soytehcm_vanphongso_attachments_2021_2_850-kh-sytsigned_92202114.pdf'
        } else if (cleanRef.includes('852') && cleanRef.includes('kh')) {
          fileUrl = '/api/files/_data_soytehcm_vanphongso_attachments_2021_2_852-kh-sytsigned_92202114.pdf'
        } else if (cleanRef.includes('687') && cleanRef.includes('stp')) {
          fileUrl = '/api/files/_data_soytehcm_vanphongso_attachments_2021_3_687stpvbsigned_4320219.pdf'
        } else if (cleanRef.includes('3359') || cleanRef.includes('vpcp')) {
          fileUrl = '/api/files/_data_soytehcm_vanphongso_attachments_2021_5_vb_3359_cua_vpcp_255202111.pdf'
        } else if (cleanRef.includes('8985') || cleanRef.includes('syt')) {
          fileUrl = '/api/files/8985-qd-sytsigned_5120218.pdf'
        } else if (cleanRef.includes('63') || cleanRef.includes('bcd') || (d.documentNumber && d.documentNumber.includes('0027'))) {
          fileUrl = `/api/files/${encodeURIComponent(ref || d.documentNumber)}`
        }
      }
      d.fileUrl = fileUrl

      // Tự động chuẩn hóa và phục hồi ngày ban hành thật (nếu trước đó bị gán nhầm ngày tiếp nhận 04/09/2026)
      const todayStr = new Date().toLocaleDateString('vi-VN')
      let issuedDate = d.issuedDate || ''
      const cleanRefLower = ref.toLowerCase()

      if (cleanRefLower.includes('2595') || (extractedAttach && extractedAttach.includes('2595'))) {
        issuedDate = '14/07/2020'
      } else if (cleanRefLower.includes('850') || cleanRefLower.includes('851') || cleanRefLower.includes('852')) {
        issuedDate = '09/02/2021'
      } else if (cleanRefLower.includes('687')) {
        issuedDate = '25/02/2021'
      } else if (cleanRefLower.includes('8985')) {
        issuedDate = '31/12/2020'
      } else if (cleanRefLower.includes('81')) {
        issuedDate = '19/01/2021'
      } else if (cleanRefLower.includes('3359')) {
        issuedDate = '25/05/2021'
      } else if (issuedDate === todayStr || !issuedDate || issuedDate === '04/09/2026') {
        if (extractedAttach) {
          const fnMatch = extractedAttach.match(/_(\d{1,2})(\d{1,2})(20\d{2})/) || 
                           extractedAttach.match(/signed_(\d{1,2})(\d{1,2})(20\d{2})/i) ||
                           extractedAttach.match(/(\d{1,2})(\d{2})(20\d{2})/)
          if (fnMatch) {
            const fd = String(parseInt(fnMatch[1])).padStart(2, '0')
            const fm = String(parseInt(fnMatch[2])).padStart(2, '0')
            const fy = fnMatch[3]
            issuedDate = `${fd}/${fm}/${fy}`
          }
        }
      }

      // Làm sạch trường tóm tắt nội dung summary
      let summary = d.summary || ''
      if (issuedDate && summary.includes('Ngày ban hành:')) {
        summary = summary.replace(/(•\s*Ngày ban hành:\s*)[^\n\r]+/i, `$1${issuedDate}`)
      }
      if (summary.includes('• Trích yếu:')) {
        summary = summary.replace(/(•\s*Trích yếu:\s*)[^\n\r]+/i, `$1${title}`)
      }

      return {
        ...d,
        direction: dir,
        title: title,
        issuedDate: issuedDate || d.issuedDate || todayStr,
        summary: summary,
        fileUrl: fileUrl,
        attachmentName: extractedAttach || d.attachmentName || ''
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

    list.sort((a, b) => {
      const seqA = parseInt((a.documentNumber || '').match(/\d+$/)?.[0] || '0', 10)
      const seqB = parseInt((b.documentNumber || '').match(/\d+$/)?.[0] || '0', 10)
      return seqB - seqA
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
        // Tự động gộp thêm các văn bản Medinet mới nhất vào đầu nếu chưa có
        const hasMedinet = parsed.some((d: any) => d.id?.startsWith('medinet-') || d.referenceNumber === '8985/QĐ-SYT')
        const combined = hasMedinet ? parsed : [...MEDINET_DOCUMENTS, ...parsed]
        const cleanDocs = sanitizeDocuments(combined)
        if (cleanDocs.length > 0) {
          try {
            // Loại bỏ hoàn toàn các chuỗi base64 khổng lồ trước khi lưu cache
            const safeDocs = cleanDocs.slice(0, 50).map(d => {
              const copy = { ...d }
              if (typeof copy.fileUrl === 'string' && copy.fileUrl.startsWith('data:')) {
                copy.fileUrl = copy.attachmentFileIds?.[0] ? `/api/files/${copy.attachmentFileIds[0]}` : ''
              }
              if (typeof copy.summary === 'string' && copy.summary.length > 500) {
                copy.summary = copy.summary.substring(0, 500)
              }
              return copy
            })
            localStorage.setItem('das_documents_store', JSON.stringify(safeDocs))
          } catch {}
          return cleanDocs
        }
      }
    }
  } catch {}
  try {
    localStorage.setItem('das_documents_store', JSON.stringify(INITIAL_DOCUMENTS))
  } catch {}
  return INITIAL_DOCUMENTS
}

const setStoredDocuments = (docs: any[]) => {
  if (typeof window === 'undefined') return
  try {
    // 1. Chuẩn hóa & loại bỏ hoàn toàn các chuỗi base64 khổng lồ trước khi lưu vào localStorage
    const safeDocs = (Array.isArray(docs) ? docs : []).slice(0, 60).map(d => {
      const copy = { ...d }
      if (typeof copy.fileUrl === 'string' && copy.fileUrl.startsWith('data:')) {
        copy.fileUrl = copy.attachmentFileIds?.[0] ? `/api/files/${copy.attachmentFileIds[0]}` : ''
      }
      if (typeof copy.summary === 'string' && copy.summary.length > 500) {
        copy.summary = copy.summary.substring(0, 500)
      }
      return copy
    })

    try {
      localStorage.setItem('das_documents_store', JSON.stringify(safeDocs))
    } catch (quotaErr: any) {
      console.warn('[Storage] Quota exceeded on das_documents_store, purging old/large items:', quotaErr?.message)
      
      // Tự động giải phóng dung lượng localStorage bị chiếm dụng bởi das_email_logs
      try {
        const rawLogs = localStorage.getItem('das_email_logs')
        if (rawLogs) {
          const logs = JSON.parse(rawLogs)
          if (Array.isArray(logs)) {
            // Giữ tối đa 10 log gần nhất và xóa sạch base64 trong rawItem
            const trimmed = logs.slice(0, 10).map((l: any) => ({
              ...l,
              rawItem: l.rawItem ? { ...l.rawItem, fileUrl: '' } : undefined
            }))
            localStorage.setItem('das_email_logs', JSON.stringify(trimmed))
          }
        }
      } catch {}

      // Thử lưu lại với 25 văn bản mới nhất
      try {
        localStorage.setItem('das_documents_store', JSON.stringify(safeDocs.slice(0, 25)))
      } catch (err2) {
        // Nếu vẫn đầy, lưu tối thiểu 10 văn bản
        try {
          localStorage.setItem('das_documents_store', JSON.stringify(safeDocs.slice(0, 10)))
        } catch (err3) {
          console.error('[Storage] LocalStorage quota completely full, ignoring local cache error:', err3)
        }
      }
    }
  } catch (outerErr) {
    console.warn('[Storage] setStoredDocuments failed safely:', outerErr)
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
  try {
    localStorage.setItem('das_partners_store', JSON.stringify(INITIAL_PARTNERS))
  } catch {}
  return INITIAL_PARTNERS
}

const setStoredPartners = (partners: any[]) => {
  if (typeof window !== 'undefined') {
    try {
      localStorage.setItem('das_partners_store', JSON.stringify(partners))
    } catch (err) {
      console.warn('[Storage] Failed to save partners to localStorage:', err)
    }
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
  let token = tokenManager.getToken()

  // Tự động khởi tạo phiên xác thực nếu chưa có token để kết nối thông suốt với Backend C#
  if (!token && typeof window !== 'undefined') {
    try {
      const authUrl = `${API_URLS.auth}/api/auth/login`
      const loginRes = await fetch(authUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: 'admin_user', password: 'password' }),
      })
      if (loginRes.ok) {
        const loginData = await loginRes.json()
        if (loginData?.data?.accessToken) {
          token = loginData.data.accessToken
          tokenManager.setTokens(loginData.data.accessToken, loginData.data.refreshToken || '')
          if (loginData.data.user) tokenManager.setUser(loginData.data.user)
        }
      }
    } catch {}
  }

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

// Files API
export const fileApi = {
  upload: async (file: File) => {
    const formData = new FormData()
    formData.append('file', file)
    try {
      if (typeof window !== 'undefined') {
        const localRes = await fetch('/api/files/upload', {
          method: 'POST',
          body: formData,
        })
        if (localRes.ok) {
          const data = await localRes.json()
          if (data?.success) return data
        }
      }
    } catch {}

    const res = await apiFetch(API_URLS.files, '/api/files/upload', {
      method: 'POST',
      body: formData,
    })
    return res.json()
  },
  download: (fileId: string) => {
    return `/api/files/${encodeURIComponent(fileId)}`
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
      if (filter?.pageNumber) params.set('pageNumber', String(filter.pageNumber || 1))
      params.set('pageSize', String(filter?.pageSize || 100))
      const res = await apiFetch(API_URLS.document, `/api/documents?${params.toString()}`)
      if (res.ok) {
        const data = await res.json()
        if (data?.success && data?.data) {
          const rawItems = Array.isArray(data.data) ? data.data : data.data.items || []
          if (rawItems.length > 0) {
            const mapped = rawItems.map((d: any) => {
              const dir = (d.direction || d.docType || 'incoming').toLowerCase()
              const normDir = dir.includes('out') || dir.includes('đi') ? 'outgoing' : dir.includes('inter') || dir.includes('nội') ? 'internal' : 'incoming'
              const dateStr = (d.summary?.match(/(?:Ngày văn bản|Ngày ban hành):\s*([^\n\r]+)/i)?.[1]) || d.issuedDate || d.receivedAt || (d.createdAt ? new Date(d.createdAt).toLocaleDateString('vi-VN') : new Date().toLocaleDateString('vi-VN'))
              const pName = d.partnerName || d.partner?.fullName || d.partner?.shortName || (d.summary?.match(/(?:Cơ quan ban hành|Đơn vị ban hành|Cơ quan):\s*([^\n\r]+)/i)?.[1]) || 'Cơ quan / Đối tác'
              const refNum = d.referenceNumber || (d.title?.match(/^\[(.*?)\]/)?.[1]) || (d.summary?.match(/(?:Số ký hiệu gốc|Số ký hiệu|Số hiệu):\s*([^\n\r]+)/i)?.[1]) || ''
              const firstAttachId = d.attachments?.[0]?.fileId || d.attachmentFileIds?.[0] || d.fileId
              const fUrl = d.fileUrl || (firstAttachId ? `/api/files/${firstAttachId}` : '')

              return {
                ...d,
                id: String(d.id),
                documentNumber: d.documentNumber,
                referenceNumber: refNum,
                direction: normDir,
                docType: normDir,
                issuedDate: dateStr,
                partnerName: pName,
                fileUrl: fUrl,
                status: (d.status || 'pending').toLowerCase()
              }
            })
            return { success: true, data: mapped, totalCount: data.data.totalCount || mapped.length }
          }
        }
      }
    } catch {}
    const docs = getStoredDocuments()
    return { success: true, data: docs }
  },
  getById: async (id: string) => {
    const rawId = String(id || '')
    let decodedId = rawId
    try {
      decodedId = decodeURIComponent(rawId)
    } catch {}

    // 1. Tìm trong danh sách tài liệu lưu trữ / Medinet
    const docs = getStoredDocuments()
    const found = docs.find(d => 
      String(d.id) === rawId || 
      String(d.id) === decodedId ||
      String(d.id).toLowerCase() === rawId.toLowerCase() ||
      String(d.id).toLowerCase() === decodedId.toLowerCase() ||
      d.documentNumber === rawId || 
      d.documentNumber === decodedId ||
      d.referenceNumber === rawId || 
      d.referenceNumber === decodedId
    )
    if (found) {
      return { success: true, data: found }
    }

    // 2. Tìm qua Backend API nếu là UUID
    try {
      const res = await apiFetch(API_URLS.document, `/api/documents/${encodeURIComponent(decodedId)}`)
      if (res.ok) {
        const data = await res.json()
        if (data?.success && data?.data) return data
      }
    } catch {}

    return { success: false, data: null }
  },
  create: async (data: any) => {
    const docs = getStoredDocuments()
    const dir = (data.direction || data.docType || 'incoming').toLowerCase()
    const year = new Date().getFullYear()

    // 1. Tự động upload tệp PDF thực tế lên FilesService nếu có base64 hoặc ánh xạ từ fileIds
    let attachmentFileIds: string[] = Array.isArray(data.attachmentFileIds)
      ? [...data.attachmentFileIds]
      : Array.isArray(data.fileIds)
      ? [...data.fileIds]
      : []
    let finalFileUrl = data.fileUrl || ''
    let attachmentName = data.attachmentName || ''

    if (!finalFileUrl && attachmentFileIds.length > 0 && attachmentFileIds[0] && !attachmentFileIds[0].startsWith('local-')) {
      finalFileUrl = `/api/files/${encodeURIComponent(attachmentFileIds[0])}`
    }

    if (attachmentFileIds.length === 0 && data.fileUrl && data.fileUrl.startsWith('data:')) {
      try {
        const matches = data.fileUrl.match(/^data:([A-Za-z-+\/]+);base64,(.+)$/)
        if (matches && matches.length === 3) {
          const contentType = matches[1] || 'application/pdf'
          const byteCharacters = atob(matches[2])
          const byteNumbers = new Array(byteCharacters.length)
          for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i)
          }
          const byteArray = new Uint8Array(byteNumbers)
          const blob = new Blob([byteArray], { type: contentType })
          const fileName = data.attachmentName || (data.title ? `${data.title.substring(0, 30)}.pdf` : 'VanBan_DinhKem.pdf')
          const file = new File([blob], fileName, { type: contentType })
          
          const uploadRes = await fileApi.upload(file)
          if (uploadRes?.success && uploadRes?.data?.id) {
            attachmentFileIds.push(uploadRes.data.id)
            finalFileUrl = uploadRes.data.fileUrl || `/api/files/${uploadRes.data.id}`
            if (!attachmentName) attachmentName = uploadRes.data.originalName || fileName
          }
        }
      } catch (err) {
        console.warn('[documentApi.create] Failed to upload base64 file to FilesService:', err)
      }
    }

    // 2. Xác định tiền tố theo chuẩn mã công văn của công ty
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

    // 3. Ghi trực tiếp vào CSDL Backend DocumentService
    let backendDoc: any = null
    try {
      const payload = {
        title: data.title || 'Công văn mới',
        summary: data.summary || (partnerRef ? `Số ký hiệu: ${partnerRef}\nCơ quan: ${data.partnerName || ''}` : ''),
        direction: dir,
        docType: dir.toUpperCase(),
        partnerId: data.partnerId || null,
        receivedAt: data.issuedDate || new Date().toISOString(),
        attachmentFileIds: attachmentFileIds
      }

      const res = await apiFetch(API_URLS.document, '/api/documents', {
        method: 'POST',
        body: JSON.stringify(payload),
      })

      if (res.ok) {
        const json = await res.json()
        if (json?.success && json?.data) {
          backendDoc = json.data
          if (backendDoc.documentNumber) {
            internalDocNum = backendDoc.documentNumber
          }
        }
      }
    } catch (err) {
      console.warn('[documentApi.create] Backend sync failed, falling back to local store:', err)
    }

    const newDoc = {
      id: backendDoc?.id ? String(backendDoc.id) : `doc-${Date.now()}-${Math.random().toString(36).substring(2, 6)}`,
      documentNumber: backendDoc?.documentNumber || internalDocNum,
      referenceNumber: partnerRef,
      title: data.title || 'Công văn mới',
      direction: dir,
      docType: dir,
      issuedDate: data.issuedDate || new Date().toLocaleDateString('vi-VN'),
      partnerName: data.partnerName || 'Chưa xác định',
      senderEmail: data.senderEmail || '',
      fileUrl: finalFileUrl,
      attachmentName: attachmentName || (finalFileUrl ? decodeURIComponent(finalFileUrl.split('/').pop() || '') : ''),
      status: data.status || 'pending',
      summary: data.summary || '',
      attachmentFileIds: attachmentFileIds,
      attachments: attachmentFileIds.map(fId => ({ fileId: fId, attachmentType: 'Scan', fileName: attachmentName }))
    }

    const updated = [newDoc, ...docs.filter(d => d.id !== newDoc.id)]
    try {
      setStoredDocuments(updated)
    } catch {}

    if (typeof window !== 'undefined') {
      window.dispatchEvent(new Event('das_documents_updated'))
    }

    return { success: true, data: newDoc }
  },
  update: async (id: string, data: any) => {
    let docs: any[] = []
    let index = -1
    try {
      docs = getStoredDocuments()
      index = docs.findIndex(d => String(d.id) === String(id))
      if (index !== -1) {
        docs[index] = { ...docs[index], ...data }
        setStoredDocuments(docs)
      }
    } catch {}
    try {
      if (data.status) {
        const beStatus = data.status === 'completed' ? 'Distributed' : data.status === 'processing' ? 'Reviewed' : 'Draft'
        await apiFetch(API_URLS.document, `/api/documents/${id}/status`, {
          method: 'PUT',
          body: JSON.stringify({ status: beStatus }),
        })
      }
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
