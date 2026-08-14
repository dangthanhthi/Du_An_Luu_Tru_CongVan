import { NextResponse } from 'next/server'
import tls from 'node:tls'

export async function POST(req: Request) {
  try {
    const body = await req.json()
    const { host = 'imap.gmail.com', port = 993, email, appPassword } = body

    if (!email || !appPassword) {
      return NextResponse.json({ success: false, message: 'Vui lòng cung cấp đầy đủ Email và Mật khẩu ứng dụng (App Password).' }, { status: 400 })
    }

    const cleanPassword = appPassword.replace(/\s+/g, '')

    return new Promise<NextResponse>(resolve => {
      let resolved = false
      const socket = tls.connect(port, host, { rejectUnauthorized: false }, () => {
        // Connected
      })

      socket.setTimeout(8000)

      let step = 0
      let buffer = ''

      socket.on('data', data => {
        buffer += data.toString()

        if (step === 0 && buffer.includes('* OK')) {
          step = 1
          buffer = ''
          socket.write(`A01 LOGIN "${email}" "${cleanPassword}"\r\n`)
        } else if (step === 1) {
          if (buffer.includes('A01 OK')) {
            resolved = true
            socket.write(`A02 LOGOUT\r\n`)
            socket.end()
            resolve(NextResponse.json({
              success: true,
              message: `Kết nối máy chủ IMAP ${host}:${port} và xác thực tài khoản '${email}' thành công!`
            }))
          } else if (buffer.includes('A01 NO') || buffer.includes('A01 BAD')) {
            resolved = true
            socket.end()
            resolve(NextResponse.json({
              success: false,
              message: 'Xác thực IMAP thất bại. Mật khẩu ứng dụng (App Password) hoặc Email không chính xác.'
            }, { status: 401 }))
          }
        }
      })

      socket.on('timeout', () => {
        if (!resolved) {
          resolved = true
          socket.destroy()
          resolve(NextResponse.json({
            success: false,
            message: `Hết thời gian chờ kết nối máy chủ IMAP (${host}:${port}). Vui lòng kiểm tra lại mạng hoặc thông số Host/Port.`
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
