import { NextResponse } from 'next/server'
import { scanM365Inbox, M365_DEFAULTS } from '@/utils/m365Ews'

export async function POST(request: Request) {
  try {
    const body = await request.json().catch(() => ({}))
    const email = (body.email && body.email.trim()) || M365_DEFAULTS.email
    const password = (body.appPassword && body.appPassword.trim()) || M365_DEFAULTS.password

    const emails = await scanM365Inbox(email, password, 20)

    const mappedLogs = emails.map(m => {
      const pdfAttachment = m.attachments.find(a => a.isPdf)
      const firstAttachment = m.attachments[0]
      const hasPdf = Boolean(pdfAttachment)

      return {
        id: m.id,
        timestamp: m.date,
        sender: m.from,
        subject: m.subject,
        attachment: pdfAttachment?.name || firstAttachment?.name || 'Không có PDF',
        hasPdf,
        docNumber: undefined,
        status: hasPdf ? 'pending_confirmation' : 'no_pdf',
        message: hasPdf
          ? `Đính kèm tệp PDF: ${pdfAttachment?.name} (${Math.round((pdfAttachment?.size || 0) / 1024)} KB). Sẵn sàng tiếp nhận.`
          : 'Thư không chứa tệp công văn PDF đính kèm.'
      }
    })

    return NextResponse.json(
      {
        success: true,
        count: mappedLogs.length,
        logs: mappedLogs,
        message: `Quét hòm thư Microsoft Exchange 365 thành công! Tìm thấy ${mappedLogs.length} email mới.`
      },
      { status: 200 }
    )
  } catch (error: any) {
    return NextResponse.json(
      {
        success: false,
        message: error?.message || 'Không thể quét email từ máy chủ Microsoft 365.'
      },
      { status: 400 }
    )
  }
}
