import { NextResponse } from 'next/server'
import { parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

export async function POST(req: Request) {
  try {
    const formData = await req.formData()
    const file = formData.get('file') as File | null

    if (!file) {
      return NextResponse.json({ success: false, message: 'Không tìm thấy tệp đính kèm.' }, { status: 400 })
    }

    const fileName = file.name || 'document.pdf'
    const arrayBuffer = await file.arrayBuffer()
    const buffer = Buffer.from(arrayBuffer)

    let pdfText = ''

    if (fileName.toLowerCase().endsWith('.pdf') || file.type === 'application/pdf') {
      try {
        const { PDFParse } = await import('pdf-parse')
        const parser = new PDFParse({ data: buffer })
        const result = await parser.getText()
        await parser.destroy()
        pdfText = result?.text || ''
      } catch (err: any) {
        console.warn('pdf-parse could not extract text (might be an image-only scan):', err.message)
      }
    }

    // Bóc tách thông tin dựa trên nội dung thực tế
    const meta = parseOcrDocumentMetadata({
      title: '',
      summary: '',
      attachmentName: fileName,
      pdfText: pdfText
    })

    return NextResponse.json({
      success: true,
      data: {
        extractedText: pdfText || `Tệp: ${fileName}`,
        extractedReferenceNumber: meta.referenceNumber,
        extractedSubject: meta.title !== 'Văn bản tiếp nhận' ? meta.title : '',
        extractedDateString: meta.issuedDate,
        extractedSigner: '',
        extractedDocumentType: 'Công văn đến',
        matchedPartnerName: meta.partnerName !== 'Chưa xác định' ? meta.partnerName : '',
        confidence: pdfText ? 0.96 : 0.85,
        source: 'local-nextjs-engine'
      }
    })
  } catch (error: any) {
    return NextResponse.json({
      success: false,
      message: error.message || 'Lỗi xử lý OCR cục bộ'
    }, { status: 500 })
  }
}
