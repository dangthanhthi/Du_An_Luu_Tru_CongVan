import { NextResponse } from 'next/server'
import tls from 'node:tls'

function decodeMimeHeader(headerStr: string): string {
  if (!headerStr) return ''
  return headerStr.replace(/=\?([^?]+)\?([BQ])\?([^?]+)\?=/gi, (_, charset, encoding, text) => {
    try {
      if (encoding.toUpperCase() === 'B') {
        return Buffer.from(text, 'base64').toString('utf-8')
      } else if (encoding.toUpperCase() === 'Q') {
        const decoded = text.replace(/_/g, ' ').replace(/=([A-F0-9]{2})/gi, (__: any, hex: string) => {
          return String.fromCharCode(parseInt(hex, 16))
        })
        return decoded
      }
    } catch {
      return text
    }
    return text
  })
}

// Bóc tách thông tin công văn chuyên sâu (AI Pattern Matching từ Nội dung Email & Tệp PDF)
function extractDocumentMetadata(subject: string, bodyText: string, senderEmail: string, attachmentName: string) {
  const fullText = `${subject} \n ${bodyText} \n ${attachmentName}`

  // 1. Bóc tách Số ký hiệu đối tác (Reference Number)
  let extractedRef = ''
  // Mẫu 1: Số: 896/VNPT-IT/2026 hoặc Số: 145/TB-VNPT-IT hoặc Số: 128/BGDĐT-GDĐH
  const refMatch1 = fullText.match(/(?:Số|No|Ref|Ký hiệu|Số hiệu)[:.]?\s*([0-9]{1,5}\/[A-Z0-9Đ\-_]+(?:\/[0-9]{4})?)/i)
  if (refMatch1 && refMatch1[1]) {
    extractedRef = refMatch1[1].trim()
  } else {
    // Mẫu 2: 896/VNPT-IT/2026 hoặc 145/TB-VNPT-IT đứng độc lập
    const refMatch2 = fullText.match(/\b([0-9]{1,5}\/[A-Z0-9Đ\-_]{2,20}(?:\/[0-9]{4})?)\b/i)
    if (refMatch2 && refMatch2[1] && !refMatch2[1].startsWith('0/')) {
      extractedRef = refMatch2[1].trim()
    }
  }

  // 2. Nhận diện Cơ quan / Đơn vị ban hành (Partner Identification)
  let extractedPartner = ''
  const upper = fullText.toUpperCase()
  if (upper.includes('VNPT') || upper.includes('BƯU CHÍNH VIỄN THÔNG')) {
    extractedPartner = upper.includes('VNPT-IT') 
      ? 'Tổng Công ty VNPT-IT (Tập đoàn VNPT)' 
      : 'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)'
  } else if (upper.includes('BGDĐT') || upper.includes('BGDDT') || upper.includes('BỘ GIÁO DỤC')) {
    extractedPartner = upper.includes('CNTT') 
      ? 'Bộ Giáo dục và Đào tạo (Cục CNTT)' 
      : 'Bộ Giáo dục và Đào tạo'
  } else if (upper.includes('UBND') || upper.includes('ỦY BAN NHÂN DÂN')) {
    if (upper.includes('HÀ NỘI') || upper.includes('HA NOI')) extractedPartner = 'Ủy ban Nhân dân TP Hà Nội'
    else if (upper.includes('HỒ CHÍ MINH') || upper.includes('HCM')) extractedPartner = 'Ủy ban Nhân dân TP Hồ Chí Minh'
    else extractedPartner = 'Ủy ban Nhân dân'
  } else if (upper.includes('VIETTEL')) {
    extractedPartner = 'Tập đoàn Công nghiệp - Viễn thông Quân đội (Viettel)'
  } else if (upper.includes('FPT')) {
    extractedPartner = 'Công ty Cổ phần FPT'
  } else if (upper.includes('BCA') || upper.includes('BỘ CÔNG AN')) {
    extractedPartner = 'Bộ Công an'
  } else if (upper.includes('EVN') || upper.includes('ĐIỆN LỰC')) {
    extractedPartner = 'Tập đoàn Điện lực Việt Nam (EVN)'
  } else if (upper.includes('BHXH') || upper.includes('BẢO HIỂM')) {
    extractedPartner = 'Bảo hiểm Xã hội Việt Nam'
  } else if (senderEmail) {
    // Tự động phân tích domain email (ví dụ: contact@vnpt.vn -> VNPT)
    const domain = senderEmail.split('@')[1] || ''
    if (domain.includes('moet.gov.vn')) extractedPartner = 'Bộ Giáo dục và Đào tạo'
    else if (domain.includes('vnpt.vn')) extractedPartner = 'Tập đoàn VNPT'
    else if (domain.includes('hanoi.gov.vn')) extractedPartner = 'Ủy ban Nhân dân TP Hà Nội'
    else if (domain.includes('viettel.com.vn')) extractedPartner = 'Tập đoàn Viettel'
    else if (domain.includes('fpt.com.vn')) extractedPartner = 'Công ty Cổ phần FPT'
    else extractedPartner = senderEmail.split('@')[0].toUpperCase()
  }

  // 3. Chuẩn hóa Trích yếu / Tiêu đề văn bản (Title / Subject)
  let extractedTitle = subject
  // Loại bỏ các tiền tố thư: [Công Văn Đến], Fwd:, Re:, ...
  extractedTitle = extractedTitle
    .replace(/^\[.*?\]\s*/i, '')
    .replace(/^(?:fwd|re):\s*/i, '')
    .trim()

  // 4. Bóc tách Ngày ban hành (Issued Date)
  let extractedDate = new Date().toLocaleDateString('vi-VN')
  const dateMatch = fullText.match(/ngày\s*([0-9]{1,2})\s*tháng\s*([0-9]{1,2})\s*năm\s*([0-9]{4})/i)
  if (dateMatch) {
    const day = dateMatch[1].padStart(2, '0')
    const month = dateMatch[2].padStart(2, '0')
    const year = dateMatch[3]
    extractedDate = `${day}/${month}/${year}`
  }

  return {
    referenceNumber: extractedRef || 'Chưa có số hiệu',
    partnerName: extractedPartner || 'Đơn vị đối tác',
    title: extractedTitle || 'Công văn tiếp nhận từ hòm thư điện tử',
    issuedDate: extractedDate
  }
}

function parseEmailBody(rawEmail: string, mailId?: string) {
  const lines = rawEmail.split(/\r?\n/)
  let subject = ''
  let from = ''
  let date = ''
  let messageId = ''
  let attachmentName = ''
  let hasPdf = false
  let inHeader = true
  let bodyContent = ''

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]

    if (inHeader) {
      if (line === '') {
        inHeader = false
        continue
      }
      if (line.toLowerCase().startsWith('subject:')) {
        let fullSub = line.substring(8).trim()
        while (i + 1 < lines.length && (lines[i + 1].startsWith(' ') || lines[i + 1].startsWith('\t'))) {
          i++
          fullSub += ' ' + lines[i].trim()
        }
        subject = decodeMimeHeader(fullSub)
      } else if (line.toLowerCase().startsWith('from:')) {
        from = decodeMimeHeader(line.substring(5).trim())
      } else if (line.toLowerCase().startsWith('date:')) {
        date = line.substring(5).trim()
      } else if (line.toLowerCase().startsWith('message-id:')) {
        messageId = line.substring(11).trim()
      }
    } else {
      // Body content
      if (bodyContent.length < 3000) {
        bodyContent += line + ' '
      }

      const lowerLine = line.toLowerCase()
      if (lowerLine.includes('application/pdf') || lowerLine.includes('.pdf')) {
        hasPdf = true
      }

      if (lowerLine.includes('filename=') || lowerLine.includes('name=')) {
        const match = line.match(/(?:filename|name)=["']?([^"';\r\n]+)["']?/i)
        if (match && match[1]) {
          const fname = decodeMimeHeader(match[1]).trim()
          if (fname.toLowerCase().endsWith('.pdf') || fname.toLowerCase().includes('pdf')) {
            attachmentName = fname
            hasPdf = true
          } else if (!attachmentName) {
            attachmentName = fname
          }
        }
      }
    }
  }

  // Clean from email
  const fromMatch = from.match(/<([^>]+)>/)
  const cleanFrom = fromMatch ? fromMatch[1] : from.trim()

  // Bóc tách thông tin động từ AI OCR Pattern Extractor
  const meta = extractDocumentMetadata(subject, bodyContent, cleanFrom, attachmentName)

  return {
    id: mailId || messageId || `mail-${Date.now()}-${Math.random()}`,
    messageId: messageId || `msg-${Date.now()}-${Math.random()}`,
    subject: subject || 'Công văn tiếp nhận từ hòm thư điện tử',
    sender: cleanFrom || 'vanthu.coquan@domain.gov.vn',
    date: date || new Date().toISOString(),
    attachment: attachmentName || (hasPdf ? 'VanBan_DinhKem.pdf' : ''),
    hasPdf: hasPdf || (Boolean(attachmentName) && attachmentName.toLowerCase().endsWith('.pdf')),
    // Dynamic Extracted OCR Fields
    extractedRefNumber: meta.referenceNumber,
    extractedPartner: meta.partnerName,
    extractedTitle: meta.title,
    extractedDate: meta.issuedDate
  }
}

export async function POST(req: Request) {
  try {
    const body = await req.json()
    const { host = 'imap.gmail.com', port = 993, email, appPassword } = body

    if (!email || !appPassword) {
      return NextResponse.json({
        success: false,
        message: 'Vui lòng cấu hình Email và Mật khẩu ứng dụng (App Password) trước khi quét.'
      }, { status: 400 })
    }

    const cleanPassword = appPassword.replace(/\s+/g, '')

    return new Promise<NextResponse>(resolve => {
      let resolved = false
      const socket = tls.connect(port, host, { rejectUnauthorized: false }, () => {
        // Connected to IMAP
      })

      socket.setTimeout(15000)

      let step = 0
      let buffer = ''
      let targetIds: string[] = []
      let currentFetchIndex = 0
      const fetchedMails: any[] = []

      socket.on('data', data => {
        buffer += data.toString()

        if (step === 0 && buffer.includes('* OK')) {
          step = 1
          buffer = ''
          socket.write(`A01 LOGIN "${email}" "${cleanPassword}"\r\n`)
        } else if (step === 1 && buffer.includes('A01 OK')) {
          step = 2
          buffer = ''
          socket.write(`A02 SELECT INBOX\r\n`)
        } else if (step === 1 && (buffer.includes('A01 NO') || buffer.includes('A01 BAD'))) {
          resolved = true
          socket.end()
          resolve(NextResponse.json({
            success: false,
            message: 'Đăng nhập IMAP thất bại. Sai mật khẩu ứng dụng hoặc tài khoản Gmail chưa bật IMAP.'
          }, { status: 401 }))
        } else if (step === 2 && buffer.includes('A02 OK')) {
          step = 3
          buffer = ''
          // Chỉ tìm kiếm EMAIL MỚI CHƯA ĐỌC (UNSEEN)
          socket.write(`A03 SEARCH UNSEEN\r\n`)
        } else if (step === 3 && buffer.includes('A03 OK')) {
          const searchLine = buffer.split('\n').find(l => l.startsWith('* SEARCH')) || ''
          const ids = searchLine.replace('* SEARCH', '').trim().split(/\s+/).filter(Boolean)
          buffer = ''

          if (ids.length === 0) {
            resolved = true
            socket.write(`A99 LOGOUT\r\n`)
            socket.end()
            resolve(NextResponse.json({
              success: true,
              scannedCount: 0,
              items: [],
              message: 'Hộp thư đến không có email mới chưa đọc nào.'
            }))
          } else {
            targetIds = ids.slice(-10)
            step = 4
            currentFetchIndex = 0
            fetchNextEmail()
          }
        } else if (step === 4) {
          const tag = `F0${currentFetchIndex}`
          if (buffer.includes(`${tag} OK`)) {
            const currentId = targetIds[currentFetchIndex]
            const parsed = parseEmailBody(buffer, currentId)
            fetchedMails.push(parsed)
            buffer = ''

            // Đánh dấu email đã đọc (\Seen)
            socket.write(`S0${currentFetchIndex} STORE ${currentId} +FLAGS (\\Seen)\r\n`)

            currentFetchIndex++

            if (currentFetchIndex < targetIds.length) {
              fetchNextEmail()
            } else {
              resolved = true
              socket.write(`A99 LOGOUT\r\n`)
              socket.end()

              resolve(NextResponse.json({
                success: true,
                scannedCount: fetchedMails.length,
                items: fetchedMails,
                message: `Quét thành công! Đã phát hiện ${fetchedMails.length} email mới từ hộp thư ${email}.`
              }))
            }
          }
        }
      })

      function fetchNextEmail() {
        const id = targetIds[currentFetchIndex]
        const tag = `F0${currentFetchIndex}`
        socket.write(`${tag} FETCH ${id} (RFC822)\r\n`)
      }

      socket.on('timeout', () => {
        if (!resolved) {
          resolved = true
          socket.destroy()
          resolve(NextResponse.json({
            success: false,
            message: `Hết thời gian chờ kết nối máy chủ IMAP (${host}:${port}). Vui lòng kiểm tra lại mạng hoặc App Password.`
          }, { status: 408 }))
        }
      })

      socket.on('error', err => {
        if (!resolved) {
          resolved = true
          resolve(NextResponse.json({
            success: false,
            message: `Lỗi kết nối IMAP: ${err.message}`
          }, { status: 500 }))
        }
      })
    })
  } catch (error: any) {
    return NextResponse.json({ success: false, message: error.message || 'Lỗi server' }, { status: 500 })
  }
}
