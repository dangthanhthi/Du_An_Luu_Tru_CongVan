import { NextResponse } from 'next/server'
import tls from 'node:tls'

function decodeMimeHeader(headerStr: string): string {
  if (!headerStr) return ''
  // Decode =?UTF-8?B?...?= or =?UTF-8?Q?...?=
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

function parseEmailBody(rawEmail: string) {
  const lines = rawEmail.split(/\r?\n/)
  let subject = ''
  let from = ''
  let date = ''
  let attachmentName = ''
  let inHeader = true

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
      }
    } else {
      // Look for attachment filename in body headers
      if (line.toLowerCase().includes('filename=') || line.toLowerCase().includes('name=')) {
        const match = line.match(/(?:filename|name)=["']?([^"';\r\n]+)["']?/i)
        if (match && match[1] && (match[1].toLowerCase().endsWith('.pdf') || match[1].toLowerCase().endsWith('.png') || match[1].toLowerCase().endsWith('.jpg'))) {
          attachmentName = decodeMimeHeader(match[1])
        }
      }
    }
  }

  // Clean from email
  const fromMatch = from.match(/<([^>]+)>/)
  const cleanFrom = fromMatch ? fromMatch[1] : from

  return {
    subject: subject || 'Công văn tiếp nhận từ hòm thư điện tử',
    sender: cleanFrom || 'vanthu.coquan@domain.gov.vn',
    date: date || new Date().toISOString(),
    attachment: attachmentName || 'CongVan_DinhKem.pdf'
  }
}

export async function POST(req: Request) {
  try {
    const body = await req.json()
    const { host = 'imap.gmail.com', port = 993, email, appPassword, allowedSenderDomains = '' } = body

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

      socket.setTimeout(12000)

      let step = 0
      let buffer = ''
      let unseenIds: string[] = []
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
          // Search for unseen emails first
          socket.write(`A03 SEARCH UNSEEN\r\n`)
        } else if (step === 3 && buffer.includes('A03 OK')) {
          const searchLine = buffer.split('\n').find(l => l.startsWith('* SEARCH')) || ''
          const ids = searchLine.replace('* SEARCH', '').trim().split(/\s+/).filter(Boolean)
          buffer = ''

          if (ids.length === 0) {
            // If no unseen, search latest 5 messages
            step = 4
            socket.write(`A04 SEARCH ALL\r\n`)
          } else {
            unseenIds = ids.slice(-5) // Take last 5
            step = 5
            currentFetchIndex = 0
            fetchNextEmail()
          }
        } else if (step === 4 && buffer.includes('A04 OK')) {
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
              message: 'Hộp thư đến đang trống, không tìm thấy email nào.'
            }))
          } else {
            unseenIds = ids.slice(-3) // Take latest 3 emails
            step = 5
            currentFetchIndex = 0
            fetchNextEmail()
          }
        } else if (step === 5) {
          const tag = `F0${currentFetchIndex}`
          if (buffer.includes(`${tag} OK`)) {
            const parsed = parseEmailBody(buffer)
            fetchedMails.push(parsed)
            buffer = ''
            currentFetchIndex++

            if (currentFetchIndex < unseenIds.length) {
              fetchNextEmail()
            } else {
              // Finished fetching all
              resolved = true
              socket.write(`A99 LOGOUT\r\n`)
              socket.end()

              resolve(NextResponse.json({
                success: true,
                scannedCount: fetchedMails.length,
                items: fetchedMails,
                message: `Quét thành công! Đã tìm thấy ${fetchedMails.length} email thực tế từ hộp thư ${email}.`
              }))
            }
          }
        }
      })

      function fetchNextEmail() {
        const id = unseenIds[currentFetchIndex]
        const tag = `F0${currentFetchIndex}`
        socket.write(`${tag} FETCH ${id} (BODY.PEEK[])\r\n`)
      }

      socket.on('timeout', () => {
        if (!resolved) {
          resolved = true
          socket.destroy()
          resolve(NextResponse.json({
            success: false,
            message: `Hết thời gian chờ kết nối máy chủ IMAP (${host}:${port}).`
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
