import { NextResponse } from 'next/server'
import { parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

/**
 * Real AI OCR Analysis API Route (100% Local / On-Premise)
 * =======================================================
 * Hỗ trợ nhận dạng quang học thực tế cho MỌI LOẠI TỆP:
 * 1. Tệp PDF Điện tử: Bóc tách trực tiếp luồng văn bản qua `pdf-parse`
 * 2. Tệp PDF Quét / Ảnh Scan / Tệp Ảnh (PNG, JPG, JPEG, TIFF, BMP):
 *    Chạy mạng nơ-ron nhận dạng ký tự quang học Tesseract AI (`vie+eng`)
 * 3. Bóc tách tự động Số hiệu, Cơ quan, Trích yếu, Ngày tháng theo ngữ pháp hành chính
 */

export async function POST(req: Request) {
  try {
    const formData = await req.formData()
    const file = formData.get('file') as File | null

    if (!file) {
      return NextResponse.json({
        success: false,
        message: 'Vui lòng chọn tệp văn bản PDF hoặc ảnh để quét OCR.'
      }, { status: 400 })
    }

    const fileName = file.name || 'document.pdf'
    const arrayBuffer = await file.arrayBuffer()
    const buffer = Buffer.from(arrayBuffer)
    const lowerName = fileName.toLowerCase()

    let extractedRawText = ''
    let ocrEngineUsed = 'unknown'
    let tesseractConfidence = -1 // -1 = chưa có score thực từ Tesseract

    // BƯỚC 1: XỬ LÝ TỆP PDF
    if (lowerName.endsWith('.pdf') || file.type === 'application/pdf') {
      // 1.1 Thử bóc tách lớp văn bản kỹ thuật số bằng pdf-parse
      try {
        const { PDFParse } = await import('pdf-parse')
        const parser = new PDFParse({ data: buffer })
        const result = await parser.getText()
        await parser.destroy()

        if (result?.text && result.text.trim().length > 30) {
          extractedRawText = result.text.trim()
          ocrEngineUsed = 'digital-pdf-parser'
        }
      } catch (err: any) {
        console.warn('PDF stream extraction notice:', err.message)
      }
    }

    // BƯỚC 2: NẾU LÀ ẢNH HOẶC PDF SCAN KHÔNG CÓ LỚP TEXT -> CHẠY TESSERACT AI OCR THẬT
    const isImageFile = lowerName.endsWith('.png') || lowerName.endsWith('.jpg') ||
                         lowerName.endsWith('.jpeg') || lowerName.endsWith('.tiff') ||
                         lowerName.endsWith('.bmp') || lowerName.endsWith('.webp') ||
                         file.type.startsWith('image/')

    if (!extractedRawText && (isImageFile || lowerName.endsWith('.pdf'))) {
      try {
        const Tesseract = (await import('tesseract.js')).default || (await import('tesseract.js'))
        
        // Chạy nhận dạng ký tự quang học tiếng Việt + tiếng Anh bằng Tesseract AI
        const { data } = await Tesseract.recognize(buffer, 'vie+eng', {
          logger: () => {} // Suppress verbose progress in server logs
        })

        if (data?.text && data.text.trim().length > 0) {
          extractedRawText = data.text.trim()
          ocrEngineUsed = 'tesseract-ai-neural'
          // Lưu confidence score thực từ Tesseract engine
          tesseractConfidence = typeof data.confidence === 'number' ? data.confidence / 100 : 0.85
        }
      } catch (ocrErr: any) {
        console.error('Tesseract recognition warning:', ocrErr.message)
      }
    }

    // BƯỚC 3: BÓC TÁCH THÔNG TIN NGHIỆP VỤ BẰNG NGỮ PHÁP HÀNH CHÍNH VIỆT NAM
    const meta = parseOcrDocumentMetadata({
      title: '',
      summary: extractedRawText,
      attachmentName: fileName,
      pdfText: extractedRawText
    })

    // Xác định độ tin cậy: ưu tiên score thực từ Tesseract, fallback heuristic cho pdf-parse
    let calculatedConfidence = 0.85
    if (tesseractConfidence >= 0) {
      // Dùng confidence score thực tế từ Tesseract AI engine
      calculatedConfidence = Math.max(0.1, Math.min(1.0, tesseractConfidence))
    } else if (ocrEngineUsed === 'digital-pdf-parser') {
      // PDF điện tử: độ tin cậy cao vì text được nhúng sẵn
      calculatedConfidence = extractedRawText.length > 200 ? 0.98 : 0.95
    } else {
      calculatedConfidence = extractedRawText.length > 0 ? 0.88 : 0.5
    }

    return NextResponse.json({
      success: true,
      data: {
        extractedText: extractedRawText || '',
        extractedReferenceNumber: meta.referenceNumber,
        extractedSubject: meta.title !== 'Văn bản tiếp nhận' ? meta.title : '',
        extractedDateString: meta.issuedDate,
        extractedDocumentType: 'Công văn đến',
        matchedPartnerName: meta.partnerName !== 'Chưa xác định' ? meta.partnerName : '',
        confidence: calculatedConfidence,
        engine: ocrEngineUsed,
        charCount: extractedRawText.length
      },
      message: extractedRawText
        ? `Đã nhận diện thành công ${extractedRawText.length} ký tự từ tệp '${fileName}'.`
        : `Đã xử lý tệp '${fileName}'.`
    })
  } catch (error: any) {
    return NextResponse.json({
      success: false,
      message: error.message || 'Lỗi trong quá trình nhận dạng ký tự quang học (AI-OCR).'
    }, { status: 500 })
  }
}
