import { NextResponse } from 'next/server'
import tls from 'node:tls'
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
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

// Bóc tách Base64 của tệp PDF THẬT từ raw RFC 822 email (Xử lý toàn diện mọi cấu trúc email phức tạp)
function extractRealPdfBase64(rawEmail: string): { base64Data: string; filename: string } | null {
  try {
    const boundaryMatches = [...rawEmail.matchAll(/boundary="?([^"\r\n;]+)"?/gi)]
    const boundaries = boundaryMatches.map(m => m[1].trim()).filter(Boolean)

    let parts: string[] = []
    if (boundaries.length > 0) {
      parts = [rawEmail]
      for (const b of boundaries) {
        const escaped = b.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
        const newParts: string[] = []
        for (const p of parts) {
          const sub = p.split(new RegExp(`--${escaped}(?:--)?`))
          newParts.push(...sub)
        }
        parts = newParts
      }
    } else {
      parts = rawEmail.split(/\r?\n\r?\n/)
    }

    for (const part of parts) {
      const headerEndIdx = part.search(/\r?\n\r?\n/)
      if (headerEndIdx === -1) continue

      const partHeader = part.substring(0, headerEndIdx)
      const partBody = part.substring(headerEndIdx)
      const lowerHeader = partHeader.toLowerCase()

      const isPdfMime = lowerHeader.includes('application/pdf') || lowerHeader.includes('application/x-pdf') || lowerHeader.includes('application/octet-stream')
      const hasPdfExt = lowerHeader.includes('.pdf')

      let filename = 'VanBan_DinhKem.pdf'
      const fnMatch = partHeader.match(/(?:filename\*?|name\*?)=["']?(?:UTF-8''|utf-8'')?([^"';\r\n]+)["']?/i)
      if (fnMatch && fnMatch[1]) {
        filename = decodeMimeHeader(fnMatch[1]).trim()
      }

      const cleanBase64 = partBody.replace(/[^A-Za-z0-9+/=]/g, '')
      if (cleanBase64.length >= 100) {
        let isRealPdf = false
        let finalBase64 = cleanBase64

        const jvIdx = cleanBase64.indexOf('JVBERi')
        if (jvIdx !== -1) {
          isRealPdf = true
          if (jvIdx > 0 && jvIdx < 120) {
            finalBase64 = cleanBase64.substring(jvIdx)
          }
        } else {
          try {
            const headBuf = Buffer.from(cleanBase64.substring(0, 4000), 'base64')
            if (headBuf.includes('%PDF-')) {
              isRealPdf = true
            }
          } catch {}
        }

        if (isRealPdf || ((isPdfMime || hasPdfExt) && cleanBase64.length >= 200)) {
          if (!filename.toLowerCase().endsWith('.pdf')) {
            filename += '.pdf'
          }
          return {
            filename,
            base64Data: `data:application/pdf;base64,${finalBase64}`
          }
        }
      }
    }

    // Fallback toàn diện: Quét trực tiếp chuỗi base64 có magic byte JVBERi trong toàn bộ email
    const jvberiIdx = rawEmail.indexOf('JVBERi')
    if (jvberiIdx !== -1) {
      const remaining = rawEmail.substring(jvberiIdx)
      const match = remaining.match(/^[A-Za-z0-9+/=\r\n\s]+/)
      if (match) {
        const cleanBase64 = match[0].replace(/[^A-Za-z0-9+/=]/g, '')
        if (cleanBase64.length >= 100) {
          let filename = 'VanBan_DinhKem.pdf'
          const before = rawEmail.substring(Math.max(0, jvberiIdx - 500), jvberiIdx)
          const fnMatch = before.match(/(?:filename\*?|name\*?)=["']?(?:UTF-8''|utf-8'')?([^"';\r\n]+)["']?/i)
          if (fnMatch && fnMatch[1]) {
            filename = decodeMimeHeader(fnMatch[1]).trim()
          }
          if (!filename.toLowerCase().endsWith('.pdf')) {
            filename += '.pdf'
          }
          return {
            filename,
            base64Data: `data:application/pdf;base64,${cleanBase64}`
          }
        }
      }
    }
  } catch (err) {
    console.error('Error extracting PDF base64:', err)
  }
  return null
}

// Trích xuất các luồng ảnh JPEG được nhúng bên trong tệp PDF scan
function extractJpegStreamsFromPdf(pdfBuffer: Buffer): Buffer[] {
  const jpegs: Buffer[] = []
  let offset = 0

  while (offset < pdfBuffer.length - 4) {
    if (pdfBuffer[offset] === 0xFF && pdfBuffer[offset + 1] === 0xD8 && pdfBuffer[offset + 2] === 0xFF) {
      const start = offset
      offset += 3
      while (offset < pdfBuffer.length - 1) {
        if (pdfBuffer[offset] === 0xFF && pdfBuffer[offset + 1] === 0xD9) {
          const end = offset + 2
          const len = end - start
          if (len > 5120) {
            jpegs.push(pdfBuffer.subarray(start, end))
          }
          offset = end
          break
        }
        offset++
      }
    } else {
      offset++
    }
  }

  return jpegs
}

// Trích xuất văn bản thực tế từ PDF — Hybrid OCR Engine
// Bước 1: Thử pdf-parse (siêu tốc, cho PDF điện tử có lớp text)
// Bước 2: Nếu pdf-parse trả < 30 ký tự → PDF scan ảnh → trích xuất ảnh scan nhúng rồi chạy Tesseract AI (vie+eng)
async function extractTextFromPdfBase64(base64Data: string): Promise<string> {
  try {
    const raw = base64Data.replace(/^data:application\/pdf;base64,/, '')
    const buffer = Buffer.from(raw, 'base64')

    // BƯỚC 1: Thử bóc tách lớp text kỹ thuật số bằng unpdf (Zero-Worker, hoàn toàn tương thích Next.js & Serverless)
    let digitalText = ''
    try {
      const { extractText } = await import('unpdf')
      const { text } = await extractText(new Uint8Array(buffer))
      const joinedText = Array.isArray(text) ? text.join('\n') : (text || '')
      digitalText = joinedText.trim()
    } catch (pdfErr: any) {
      console.warn('[Email Scan] unpdf extraction notice:', pdfErr.message)
    }

    // Nếu pdf-parse trích xuất đủ text (PDF điện tử) → trả về ngay
    if (digitalText.length >= 30) {
      return digitalText
    }

    // BƯỚC 2: PDF scan ảnh (< 30 ký tự) → Trích xuất ảnh scan nhúng rồi chạy Tesseract AI
    console.log(`[Email Hybrid OCR] pdf-parse trích được ${digitalText.length} ký tự → trích xuất ảnh scan...`)
    try {
      const imageBuffers = extractJpegStreamsFromPdf(buffer)
      if (imageBuffers.length > 0) {
        const Tesseract = (await import('tesseract.js')).default || (await import('tesseract.js'))
        const ocrChunks: string[] = []

        for (let i = 0; i < Math.min(imageBuffers.length, 5); i++) {
          try {
            const { data } = await Tesseract.recognize(imageBuffers[i], 'vie+eng', {
              logger: () => {}
            })
            if (data?.text && data.text.trim().length > 0) {
              ocrChunks.push(data.text.trim())
            }
          } catch (err: any) {
            console.warn(`[Email OCR] Lỗi trang ${i + 1}:`, err.message)
          }
        }

        if (ocrChunks.length > 0) {
          return ocrChunks.join('\n\n')
        }
      }
    } catch (ocrErr: any) {
      console.warn('[Email Hybrid OCR] Tesseract fallback notice:', ocrErr.message)
    }

    return digitalText
  } catch (err) {
    console.error('Error in Hybrid OCR extraction:', err)
    return ''
  }
}

async function parseEmailBody(rawEmail: string, mailId?: string) {
  const lines = rawEmail.split(/\r?\n/)
  let subject = ''
  let from = ''
  let date = ''
  let messageId = ''
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

  // 1. Trích xuất file PDF THẬT từ email
  const realPdf = extractRealPdfBase64(rawEmail)
  const hasRealPdf = Boolean(realPdf && realPdf.base64Data)

  // 2. Trích xuất thông tin OCR chuyên sâu từ AI-OCR Service (C# Tesseract 5 + Docnet + DynamicFieldExtractor)
  let ocrData: any = null
  let pdfText = ''
  let persistedFileUrl = ''

  if (hasRealPdf && realPdf?.base64Data) {
    try {
      const base64Str = realPdf.base64Data.replace(/^data:application\/pdf;base64,/, '')
      const pdfBuffer = Buffer.from(base64Str, 'base64')

      // Lưu trữ tệp PDF vật lý vào public/uploads để phục vụ xem trước và tải về ngay lập tức
      try {
        const fileId = crypto.randomUUID()
        const uploadsDir = path.join(process.cwd(), 'public', 'uploads')
        if (!fs.existsSync(uploadsDir)) {
          fs.mkdirSync(uploadsDir, { recursive: true })
        }
        fs.writeFileSync(path.join(uploadsDir, `${fileId}.pdf`), pdfBuffer)
        fs.writeFileSync(path.join(uploadsDir, fileId), pdfBuffer)
        if (realPdf.filename) {
          try {
            fs.writeFileSync(path.join(uploadsDir, realPdf.filename), pdfBuffer)
          } catch {}
        }
        persistedFileUrl = `/api/files/${fileId}`

        // Đồng bộ FilesService ngầm không chặn
        const beFormData = new FormData()
        const blob = new Blob([pdfBuffer], { type: 'application/pdf' })
        beFormData.append('file', blob, realPdf.filename || 'attachment.pdf')
        fetch('http://localhost:5004/api/files/upload', {
          method: 'POST',
          body: beFormData,
          signal: AbortSignal.timeout(4000)
        }).catch(() => {})
      } catch (saveErr) {
        console.warn('[Email Scan] Error persisting PDF file locally:', saveErr)
      }

      const blob = new Blob([pdfBuffer], { type: 'application/pdf' })
      const formData = new FormData()
      formData.append('file', blob, realPdf.filename || 'attachment.pdf')
      if (cleanFrom) {
        formData.append('senderEmail', cleanFrom)
      }

      const ocrRes = await fetch('http://localhost:5006/api/ai-ocr/analyze-file', {
        method: 'POST',
        body: formData,
        signal: AbortSignal.timeout(30000)
      })

      if (ocrRes.ok) {
        const json = await ocrRes.json()
        if (json?.data) {
          ocrData = json.data
          pdfText = json.data.extractedText || ''
        }
      }
    } catch (err: any) {
      console.warn('[Email Scan] ai-ocr-service error or timeout:', err.message)
    }

    // Fallback: Local extractor nếu backend OCR tạm thời bận
    if (!pdfText) {
      pdfText = await extractTextFromPdfBase64(realPdf.base64Data)
    }
  }

  // Nếu email KHÔNG CÓ tệp PDF thực tế (ví dụ: email thông báo mạng xã hội, tin tức, spam):
  // Tuyệt đối không tạo tệp ảo, không sinh URL giả và không gán tên tệp PDF
  if (!hasRealPdf) {
    return {
      id: mailId || messageId || `mail-${Date.now()}`,
      messageId: messageId || `msg-${Date.now()}`,
      subject: subject || 'Email thông báo',
      sender: cleanFrom || 'Chưa xác định',
      date: date || new Date().toISOString(),
      attachment: '',
      hasPdf: false,
      fileUrl: '',
      pdfExtractedLength: 0,
      extractedRefNumber: '',
      extractedPartner: '',
      extractedTitle: subject || 'Email thông báo',
      extractedDate: '',
      extractedDocumentType: 'Email',
      extractedDirection: 'incoming',
      directionRationale: 'Email thông thường không có tệp PDF đính kèm.',
      ocrEngine: 'none'
    }
  }

  // 3. Bóc tách thông tin động từ AI OCR Universal Extractor cho tệp PDF THẬT
  const meta = parseOcrDocumentMetadata({
    title: subject,
    summary: bodyContent,
    senderEmail: cleanFrom,
    attachmentName: realPdf?.filename || '',
    pdfText: pdfText
  })

  const finalRefNumber = ocrData?.extractedReferenceNumber || meta.referenceNumber
  const finalPartner = ocrData?.extractedPartnerName || meta.partnerName
  const finalTitle = ocrData?.extractedSubject || meta.title || subject || 'Công văn tiếp nhận từ hòm thư điện tử'
  const finalDate = ocrData?.extractedDateString || meta.issuedDate
  const finalDocType = ocrData?.extractedDocumentType || meta.documentType
  // Xác định thể loại văn bản (incoming / outgoing / internal)
  let docDirection = meta.direction || 'incoming'
  const combinedContext = `${subject} ${bodyContent} ${realPdf?.filename || ''}`.toLowerCase()
  if (
    combinedContext.includes('noi bo') ||
    combinedContext.includes('nội bộ') ||
    combinedContext.includes('cong van noi bo') ||
    combinedContext.includes('công văn nội bộ')
  ) {
    docDirection = 'internal'
  }

  return {
    id: mailId || messageId || `mail-${Date.now()}`,
    messageId: messageId || `msg-${Date.now()}`,
    subject: finalTitle,
    sender: cleanFrom || 'Chưa xác định',
    date: date || new Date().toISOString(),
    attachment: realPdf?.filename || 'VanBan_DinhKem.pdf',
    hasPdf: true,
    hasRealPdf: true,
    fileUrl: persistedFileUrl || '',
    pdfExtractedLength: pdfText.length,
    // Dynamic Extracted OCR Fields (Từ nội dung PDF thực tế)
    extractedRefNumber: finalRefNumber,
    extractedPartner: finalPartner,
    extractedTitle: finalTitle,
    extractedDate: finalDate,
    extractedDocumentType: finalDocType,
    extractedDirection: docDirection,
    directionRationale: meta.directionRationale || '',
    ocrEngine: ocrData ? 'native-csharp-tesseract5' : 'fallback-local'
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

      // Đặt timeout 120s cho socket IMAP
      socket.setTimeout(120000)

      let step = 0
      let buffer = ''
      let targetIds: string[] = []
      let currentFetchIndex = 0
      const rawEmailList: { id: string; buffer: string }[] = []

      socket.on('data', async data => {
        // Làm mới timeout mỗi khi có dữ liệu truyền đến
        socket.setTimeout(120000)
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
            // Nếu không có email chưa đọc nào (do người dùng đã mở xem trước trên Gmail hoặc do hệ thống quét ngầm),
            // Tự động tìm các email mới nhất trong Hộp thư đến (SEARCH ALL) để không bỏ sót công văn
            step = 35
            socket.write(`A035 SEARCH ALL\r\n`)
          } else {
            // Lấy 5 email chưa đọc mới nhất để quét
            targetIds = ids.slice(-5)
            step = 4
            currentFetchIndex = 0
            fetchNextEmail()
          }
        } else if (step === 35 && buffer.includes('A035 OK')) {
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
              message: 'Hộp thư đến trống (không có email nào).'
            }))
          } else {
            // Lấy 5 email mới nhất trong Hộp thư đến
            targetIds = ids.slice(-5)
            step = 4
            currentFetchIndex = 0
            fetchNextEmail()
          }
        } else if (step === 4) {
          const tag = `F0${currentFetchIndex}`
          if (buffer.includes(`${tag} OK`)) {
            const currentId = targetIds[currentFetchIndex]
            
            // Lưu buffer vào danh sách tải về (tách rời khỏi khâu chạy OCR để không giữ socket IMAP)
            rawEmailList.push({ id: currentId, buffer: buffer })
            buffer = ''

            // Đánh dấu email đã đọc (\Seen)
            socket.write(`S0${currentFetchIndex} STORE ${currentId} +FLAGS (\\Seen)\r\n`)

            currentFetchIndex++

            if (currentFetchIndex < targetIds.length) {
              fetchNextEmail()
            } else {
              // Tải IMAP hoàn tất! Đóng kết nối an toàn ngay lập tức:
              socket.write(`A99 LOGOUT\r\n`)
              socket.end()

              // BƯỚC 2: Bóc tách AI-OCR sau khi đã ngắt socket IMAP -> KHÔNG BAO GIỜ BỊ HẾT THỜI GIAN KẾT NỐI
              try {
                const fetchedMails: any[] = []
                for (const item of rawEmailList) {
                  const parsed = await parseEmailBody(item.buffer, item.id)
                  fetchedMails.push(parsed)
                }

                if (!resolved) {
                  resolved = true
                  resolve(NextResponse.json({
                    success: true,
                    scannedCount: fetchedMails.length,
                    items: fetchedMails,
                    message: `Quét thành công! Đã phát hiện và đọc nội dung PDF từ ${fetchedMails.length} email mới.`
                  }))
                }
              } catch (parseErr: any) {
                if (!resolved) {
                  resolved = true
                  resolve(NextResponse.json({
                    success: false,
                    message: `Lỗi xử lý nội dung email: ${parseErr.message}`
                  }, { status: 500 }))
                }
              }
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
