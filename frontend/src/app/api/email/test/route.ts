import { NextResponse } from 'next/server'
import { testM365Connection, M365_DEFAULTS } from '@/utils/m365Ews'

export async function POST(request: Request) {
  try {
    const body = await request.json().catch(() => ({}))
    const email = (body.email && body.email.trim()) || M365_DEFAULTS.email
    const password = (body.appPassword && body.appPassword.trim()) || M365_DEFAULTS.password

    const result = await testM365Connection(email, password)

    return NextResponse.json(
      {
        success: true,
        message: result.message,
        folderCount: result.folderCount
      },
      { status: 200 }
    )
  } catch (error: any) {
    return NextResponse.json(
      {
        success: false,
        message: error?.message || 'Không thể kết nối đến máy chủ email Microsoft 365.'
      },
      { status: 400 }
    )
  }
}
