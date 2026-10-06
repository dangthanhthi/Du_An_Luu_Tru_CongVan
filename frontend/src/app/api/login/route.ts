import { NextResponse } from 'next/server'

// The DAS login UI uses its validated backend client. This template route cannot mint sessions.
export async function POST(_request: Request) {
  return NextResponse.json(
    { success: false, code: 'TEMPLATE_LOGIN_DISABLED', message: 'Đường đăng nhập mẫu đã ngừng sử dụng.' },
    { status: 410, headers: { 'Cache-Control': 'no-store' } }
  )
}
