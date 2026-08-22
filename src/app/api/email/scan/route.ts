import { NextResponse } from 'next/server'
import tls from 'node:tls'
import { parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

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

// Bóc tách Base64 của tệp PDF THẬT từ raw RFC 822 email
function extractRealPdfBase64(rawEmail: string): { base64Data: string; filename: string } | null {
  try {
    // 1. Tìm boundary và các part trong multipart email
    const lines = rawEmail.split(/\r?\n/)
    let inPdfPart = false
    let isBase64Encoding = false
    let currentFileName = ''
    let base64Buffer = ''

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i]

      if (line.toLowerCase().includes('content-type: application/pdf') || line.toLowerCase().includes('.pdf')) {
        inPdfPart = true
      }

      if (inPdfPart) {
        if (line.toLowerCase().includes('content-transfer-encoding: base64')) {
          isBase64Encoding = true
        }

        if (line.toLowerCase().includes('filename=') || line.toLowerCase().includes('name=')) {
          const match = line.match(/(?:filename|name)=["']?([^"';\r\n]+)["']?/i)
          if (match && match[1]) {
            currentFileName = decodeMimeHeader(match[1]).trim()
          }
        }

        // Dòng trống đầu tiên báo hiệu kết thúc header của part và bắt đầu nội dung base64
        if (line === '' && isBase64Encoding) {
          // Thu thập các dòng base64 tiếp theo cho đến khi gặp boundary tiếp theo
          for (let j = i + 1; j < lines.length; j++) {
            const contentLine = lines[j].trim()
            if (contentLine.startsWith('--')) {
              // Gặp boundary kết thúc part
              break
            }
            if (/^[A-Za-z0-9+/=]+$/.test(contentLine)) {
              base64Buffer += contentLine
            }
          }
          break
        }
      }
    }

    if (base64Buffer.length > 500) {
      return {
        base64Data: `data:application/pdf;base64,${base64Buffer}`,
        filename: currentFileName || 'VanBan_DinhKem_Tu_Email.pdf'
      }
    }
  } catch {}
  return null
}

function parseEmailBody(rawEmail: string, mailId?: string) {
  const lines = rawEmail.split(/\r?\n/)
  let subject = ''
  let from = ''
  let date = ''
  let messageId = ''
  let attachmentName = ''
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
      if (bodyContent.length < 5000) {
        bodyContent += line + ' '
      }
    }
  }

  // Clean from email
  const fromMatch = from.match(/<([^>]+)>/)
  const cleanFrom = fromMatch ? fromMatch[1] : from.trim()

  // Trích xuất file PDF THẬT từ email
  const realPdf = extractRealPdfBase64(rawEmail)
  const hasRealPdf = Boolean(realPdf && realPdf.base64Data)

  // Bóc tách thông tin động từ AI OCR Universal Extractor
  const meta = parseOcrDocumentMetadata({
    title: subject,
    summary: bodyContent,
    senderEmail: cleanFrom,
    attachmentName: realPdf?.filename || ''
  })

  return {
    id: mailId || messageId || `mail-${Date.now()}-${Math.random()}`,
    messageId: messageId || `msg-${Date.now()}-${Math.random()}`,
    subject: subject || 'Công văn tiếp nhận từ hòm thư điện tử',
    sender: cleanFrom || 'vanthu.coquan@domain.gov.vn',
    date: date || new Date().toISOString(),
    attachment: realPdf?.filename || (hasRealPdf ? 'VanBan_DinhKem.pdf' : ''),
    hasPdf: hasRealPdf,
    fileUrl: realPdf?.base64Data || '', // Base64 của PDF THẬT 100%
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

      socket.setTimeout(20000)

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
