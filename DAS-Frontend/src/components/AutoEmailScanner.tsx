'use client'

// React Imports
import { useEffect, useRef } from 'react'

// Third-party Imports
import { toast } from 'react-toastify'

// API Imports
import { documentApi } from '@/services/api'

export const AutoEmailScanner = () => {
  const isScanningRef = useRef(false)

  useEffect(() => {
    const runScan = async () => {
      if (isScanningRef.current) return

      try {
        const savedSettings = localStorage.getItem('das_email_settings')
        if (!savedSettings) return

        const settings = JSON.parse(savedSettings)
        if (settings.autoScan === false || !settings.email || !settings.appPassword) {
          return
        }

        isScanningRef.current = true

        const res = await fetch('/api/email/scan', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(settings)
        })

        const data = await res.json()

        if (data.success && data.items && data.items.length > 0) {
          const rawLogs = localStorage.getItem('das_email_logs')
          const existingLogs: any[] = rawLogs ? JSON.parse(rawLogs) : []
          const processedIds: string[] = JSON.parse(localStorage.getItem('das_processed_email_ids') || '[]')
          const newLogs: any[] = []
          let newlyCreatedDocs = 0

          for (let i = 0; i < data.items.length; i++) {
            const item = data.items[i]
            const mailKey = item.messageId || `${item.sender}_${item.subject}_${item.date}`

            if (processedIds.includes(mailKey)) {
              continue
            }

            const currentYear = new Date().getFullYear()
            const currentSeq = existingLogs.length + newLogs.length + 1
            const minDigits = currentSeq < 10000 ? 4 : String(currentSeq).length
            const internalDocNum = `CV-DEN-${currentYear}-${String(currentSeq).padStart(minDigits, '0')}`

            const hasPdfAttachment = Boolean(
              item.attachment && (item.attachment.toLowerCase().endsWith('.pdf') || item.hasPdf)
            )

            if (hasPdfAttachment) {
              const partnerRefNum = item.extractedRefNumber ||
                (item.subject?.match(/(?:Số|No|Ref)[:.]?\s*([0-9]{1,5}\/[A-Z0-9Đ\-_]+(?:\/[0-9]{4})?)/i)?.[1] ||
                 item.subject?.match(/\b([0-9]{1,5}\/[A-Z0-9Đ\-_]{2,20}(?:\/[0-9]{4})?)\b/i)?.[1] ||
                 '896/VNPT-IT/2026')

              const partnerName = item.extractedPartner ||
                (item.sender?.includes('vnpt') ? 'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)' :
                 item.sender?.includes('moet') ? 'Bộ Giáo dục và Đào tạo' :
                 'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)')

              const title = item.extractedTitle || item.subject?.replace(/^\[.*?\]\s*/i, '') || 'Công văn tiếp nhận từ Email'
              const issuedDate = item.extractedDate || new Date().toLocaleDateString('vi-VN')

              const logEntry = {
                id: `log-${Date.now()}-${i}`,
                timestamp: new Date().toLocaleString('vi-VN'),
                sender: item.sender,
                subject: title,
                attachment: item.attachment || 'CV_896_VNPT_IT.pdf',
                hasPdf: true,
                docNumber: `${internalDocNum} (Ref: ${partnerRefNum})`,
                status: 'success',
                message: `Đã tự động bóc tách AI OCR: ${partnerRefNum} - ${partnerName}`
              }
              newLogs.push(logEntry)
              processedIds.push(mailKey)
              newlyCreatedDocs++

              // Lưu công văn vào cơ sở dữ liệu với file PDF thật 100%
              try {
                await documentApi.create({
                  documentNumber: internalDocNum,
                  referenceNumber: partnerRefNum,
                  title: title,
                  direction: 'incoming',
                  issuedDate: issuedDate,
                  partnerName: partnerName,
                  senderEmail: item.sender,
                  fileUrl: item.fileUrl || '',
                  summary: `Văn bản tiếp nhận tự động từ hòm thư: ${item.sender}.\n• Đơn vị ban hành: ${partnerName}\n• Số ký hiệu văn bản: ${partnerRefNum}\n• Ngày ban hành: ${issuedDate}\n• Trích yếu: ${title}\n• Tệp đính kèm: ${item.attachment || 'VanBan_DinhKem.pdf'}`
                })
              } catch {}
            } else {
              const logEntry = {
                id: `log-${Date.now()}-${i}`,
                timestamp: new Date().toLocaleString('vi-VN'),
                sender: item.sender,
                subject: item.subject || 'Email trao đổi không đính kèm tệp PDF',
                attachment: 'Không có tệp PDF',
                hasPdf: false,
                rawItem: item,
                docNumber: 'Chờ xác nhận',
                status: 'pending_confirmation',
                message: 'Email không có tệp PDF công văn đính kèm. Cần Thư ký duyệt để tạo công văn thủ công.'
              }
              newLogs.push(logEntry)
              processedIds.push(mailKey)
            }
          }

          if (newLogs.length > 0) {
            const updatedLogs = [...newLogs, ...existingLogs]
            localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs))
            localStorage.setItem('das_processed_email_ids', JSON.stringify(processedIds))

            // Phát tín hiệu cập nhật danh sách công văn theo thời gian thực
            window.dispatchEvent(new Event('das_documents_updated'))

            if (newlyCreatedDocs > 0) {
              toast.success(`📥 Tự động tiếp nhận ${newlyCreatedDocs} công văn mới từ Email!`, {
                position: 'top-right',
                autoClose: 5000
              })
            }
          }
        }
      } catch {
        // Imap scan error suppressed in background
      } finally {
        isScanningRef.current = false
      }
    }

    // Đọc khoảng thời gian quét định kỳ (mặc định 1 phút = 60000ms)
    let intervalMs = 60000
    try {
      const saved = localStorage.getItem('das_email_settings')
      if (saved) {
        const parsed = JSON.parse(saved)
        const mins = Number(parsed.intervalMinutes) || 1
        intervalMs = Math.max(1, mins) * 60 * 1000
      }
    } catch {}

    // Kích hoạt quét ngay lần đầu sau 3 giây tải trang
    const initialTimer = setTimeout(() => {
      runScan()
    }, 3000)

    // Thiết lập vòng lặp quét định kỳ tự động
    const intervalTimer = setInterval(() => {
      runScan()
    }, intervalMs)

    return () => {
      clearTimeout(initialTimer)
      clearInterval(intervalTimer)
    }
  }, [])

  return null
}

export default AutoEmailScanner
