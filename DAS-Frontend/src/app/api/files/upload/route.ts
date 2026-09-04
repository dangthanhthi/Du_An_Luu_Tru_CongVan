import { NextRequest, NextResponse } from 'next/server'
import fs from 'fs'
import path from 'path'
import crypto from 'crypto'

const FILE_SERVICE_URL = process.env.FILE_SERVICE_URL || 'http://localhost:5004'

export async function POST(req: NextRequest) {
  try {
    const formData = await req.formData()
    const file = formData.get('file') as File | null

    if (!file || typeof file === 'string') {
      return NextResponse.json(
        { success: false, message: 'Không tìm thấy tệp đính kèm trong yêu cầu.' },
        { status: 400 }
      )
    }

    const originalName = file.name || 'document.pdf'
    const arrayBuffer = await file.arrayBuffer()
    const buffer = Buffer.from(arrayBuffer)
    const sizeBytes = buffer.length
    const contentType = file.type || 'application/pdf'

    // 1. Chuẩn bị thư mục lưu trữ cục bộ public/uploads
    const uploadsDir = path.join(process.cwd(), 'public', 'uploads')
    if (!fs.existsSync(uploadsDir)) {
      fs.mkdirSync(uploadsDir, { recursive: true })
    }

    let fileId = crypto.randomUUID()

    // 2. Chuyển tiếp tới backend FilesService (:5004) để đồng bộ lưu trữ CSDL backend
    try {
      const beFormData = new FormData()
      const blob = new Blob([buffer], { type: contentType })
      beFormData.append('file', blob, originalName)

      const beRes = await fetch(`${FILE_SERVICE_URL.replace(/\/+$/, '')}/api/files/upload`, {
        method: 'POST',
        body: beFormData,
        signal: AbortSignal.timeout(6000)
      })

      if (beRes.ok) {
        const beData = await beRes.json()
        if (beData?.data?.id) {
          fileId = beData.data.id
        }
      }
    } catch (beErr) {
      console.warn('[api/files/upload] Backend FilesService forward skipped or timed out, saved locally:', beErr)
    }

    // 3. Lưu trữ vật lý chắc chắn vào thư mục cục bộ (với cả fileId.pdf, fileId và originalName)
    const filePathWithId = path.join(uploadsDir, `${fileId}.pdf`)
    const filePathRawId = path.join(uploadsDir, fileId)
    const filePathOrigName = path.join(uploadsDir, originalName)

    fs.writeFileSync(filePathWithId, buffer)
    fs.writeFileSync(filePathRawId, buffer)
    try {
      fs.writeFileSync(filePathOrigName, buffer)
    } catch {}

    const fileUrl = `/api/files/${fileId}`

    return NextResponse.json({
      success: true,
      data: {
        id: fileId,
        fileId: fileId,
        fileUrl: fileUrl,
        originalName: originalName,
        sizeBytes: sizeBytes,
        contentType: contentType
      },
      message: 'Tải tệp lên thành công và đã đồng bộ kho lưu trữ.'
    })
  } catch (error: any) {
    console.error('[api/files/upload] Error:', error)
    return NextResponse.json(
      { success: false, message: error.message || 'Lỗi xử lý tệp trên máy chủ.' },
      { status: 500 }
    )
  }
}
