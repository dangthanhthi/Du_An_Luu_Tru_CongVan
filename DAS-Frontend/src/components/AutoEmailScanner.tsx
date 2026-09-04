'use client'

// React Imports
import { useEffect, useRef } from 'react'

// Third-party Imports
import { toast } from 'react-toastify'

export const AutoEmailScanner = () => {
  const isScanningRef = useRef(false)

  useEffect(() => {
    let intervalTimer: NodeJS.Timeout | null = null
    let initialTimer: NodeJS.Timeout | null = null

    const runScan = async () => {
      if (isScanningRef.current) {
        console.log('[AutoEmailScanner] ⏳ Quét đang diễn ra, bỏ qua chu kỳ này...')
        return
      }

      try {
        const savedSettings = localStorage.getItem('das_email_settings')
        if (!savedSettings) return

        const settings = JSON.parse(savedSettings)
        if (settings.autoScan === false || !settings.email || !settings.appPassword) {
          console.log('[AutoEmailScanner] ⏸️ Tự động quét tạm dừng: Chưa cấu hình Email hoặc App Password.')
          return
        }

        isScanningRef.current = true
        console.log(`[AutoEmailScanner] 🔄 [${new Date().toLocaleTimeString('vi-VN')}] Bắt đầu tự động quét Gmail (${settings.email})...`)

        const res = await fetch('/api/email/scan', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(settings),
          signal: AbortSignal.timeout(45000)
        })

        const data = await res.json()
        const scanTimestamp = new Date().toISOString()
        localStorage.setItem('das_email_last_scan_time', scanTimestamp)

        if (typeof window !== 'undefined') {
          window.dispatchEvent(new CustomEvent('das_email_scan_completed', { detail: { timestamp: scanTimestamp, data } }))
        }

        console.log(`[AutoEmailScanner] ✅ [${new Date().toLocaleTimeString('vi-VN')}] Quét hoàn tất: ${data.items?.length || 0} email mới`)

        if (data.success && data.items && data.items.length > 0) {
          const rawLogs = localStorage.getItem('das_email_logs')
          const existingLogs: any[] = rawLogs ? JSON.parse(rawLogs) : []
          const processedIds: string[] = JSON.parse(localStorage.getItem('das_processed_email_ids') || '[]')
          const newLogs: any[] = []
          let currentLogs = [...existingLogs]

          for (let i = 0; i < data.items.length; i++) {
            const item = data.items[i]
            const mailKey = item.messageId || `${item.sender}_${item.subject}_${item.date}`

            const senderLower = (item.sender || '').toLowerCase()
            const isSocialOrSpam = /facebookmail\.com|youtube\.com|shopee\.|tiktok\.com|instagram\.com|linkedin\.com|twitter\.com|x\.com|pinterest\.com/i.test(senderLower)

            const hasPdfAttachment = Boolean(
              !isSocialOrSpam &&
              (item.hasRealPdf === true || item.hasPdf === true) &&
              item.attachment &&
              typeof item.attachment === 'string' &&
              item.attachment.toLowerCase().endsWith('.pdf') &&
              !item.attachment.toLowerCase().includes('không có') &&
              !item.attachment.toLowerCase().includes('no pdf')
            )

            const partnerRefNum = item.extractedRefNumber || ''
            const partnerName = item.extractedPartner || 'Chưa xác định'
            const title = item.extractedTitle || item.subject?.replace(/^\[.*?\]\s*/i, '') || 'Công văn tiếp nhận từ Email'
            const attachmentFile = hasPdfAttachment ? item.attachment : 'Không có tệp PDF'

            const existingIdx = currentLogs.findIndex(
              l => (l.messageId && item.messageId && l.messageId === item.messageId) ||
                   (l.id === item.id) ||
                   (l.sender === item.sender && l.subject === title)
            )

            if (existingIdx !== -1) {
              if (hasPdfAttachment && (!currentLogs[existingIdx].hasPdf || currentLogs[existingIdx].status === 'no_pdf')) {
                currentLogs[existingIdx] = {
                  ...currentLogs[existingIdx],
                  attachment: attachmentFile,
                  hasPdf: true,
                  docNumber: 'Chờ phân loại & tiếp nhận',
                  status: 'pending_intake',
                  rawItem: {
                    ...item,
                    hasPdf: true,
                    hasRealPdf: true,
                    fileUrl: item.fileUrl && !item.fileUrl.startsWith('data:') ? item.fileUrl : currentLogs[existingIdx]?.rawItem?.fileUrl || ''
                  },
                  message: partnerRefNum ? `AI OCR bóc tách: ${partnerRefNum} - ${partnerName}` : `AI OCR bóc tách: ${partnerName}`
                }
                newLogs.push(currentLogs[existingIdx])
              }
              continue
            }

            const logEntry = {
              id: `log-${Date.now()}-${i}`,
              messageId: item.messageId,
              timestamp: new Date().toLocaleString('vi-VN'),
              sender: item.sender,
              subject: title,
              attachment: attachmentFile,
              hasPdf: hasPdfAttachment,
              rawItem: {
                ...item,
                hasPdf: hasPdfAttachment,
                hasRealPdf: hasPdfAttachment,
                fileUrl: hasPdfAttachment && item.fileUrl && !item.fileUrl.startsWith('data:') ? item.fileUrl : ''
              },
              docNumber: hasPdfAttachment ? 'Chờ phân loại & tiếp nhận' : 'Không có PDF (Bỏ qua)',
              status: hasPdfAttachment ? 'pending_intake' : 'no_pdf',
              suggestedType: item.extractedDirection || 'incoming',
              message: hasPdfAttachment 
                ? (partnerRefNum ? `AI OCR bóc tách: ${partnerRefNum} - ${partnerName}` : `AI OCR bóc tách: ${partnerName}`)
                : 'Email thông thường hoặc thông báo tự động, không có tệp PDF công văn đính kèm.'
            }
            newLogs.push(logEntry)
            currentLogs.unshift(logEntry)
            if (!processedIds.includes(mailKey)) {
              processedIds.push(mailKey)
            }
          }

          if (newLogs.length > 0) {
            const updatedLogs = currentLogs.slice(0, 25)
            try {
              localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs))
            } catch (storageErr) {
              console.warn('[AutoEmailScanner] Storage quota reached, saving 10 items:', storageErr)
              try {
                localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs.slice(0, 10)))
              } catch {}
            }
            try {
              localStorage.setItem('das_processed_email_ids', JSON.stringify(processedIds.slice(-50)))
            } catch {}

            // Phát tín hiệu cập nhật danh sách hòm thư tiếp nhận
            window.dispatchEvent(new Event('das_email_logs_updated'))

            const newPdfCount = newLogs.filter(l => l.hasPdf).length
            if (newPdfCount > 0) {
              toast.info(`📥 Hòm thư đã nhận ${newPdfCount} công văn PDF mới! Vui lòng vào phân loại và lưu.`, {
                position: 'top-right',
                autoClose: 6000
              })
            }
          }
        }
      } catch (err: any) {
        console.warn('[AutoEmailScanner] Thông báo quét ngầm:', err?.message || err)
      } finally {
        isScanningRef.current = false
      }
    }

    const setupScanner = () => {
      if (intervalTimer) clearInterval(intervalTimer)
      if (initialTimer) clearTimeout(initialTimer)

      let intervalMs = 60000
      try {
        const saved = localStorage.getItem('das_email_settings')
        if (saved) {
          const parsed = JSON.parse(saved)
          const mins = Number(parsed.intervalMinutes) || 1
          intervalMs = Math.max(1, mins) * 60 * 1000
        }
      } catch {}

      console.log(`[AutoEmailScanner] ⏱️ Thiết lập chu kỳ tự động quét: ${intervalMs / 1000}s`)

      // Kích hoạt quét ngay lần đầu sau 2 giây
      initialTimer = setTimeout(() => {
        runScan()
      }, 2000)

      // Thiết lập vòng lặp quét định kỳ tự động
      intervalTimer = setInterval(() => {
        runScan()
      }, intervalMs)
    }

    setupScanner()

    const handleSettingsUpdated = () => {
      console.log('[AutoEmailScanner] 🔄 Nhận tín hiệu cấu hình email mới, nạp lại chu kỳ quét...')
      setupScanner()
    }

    window.addEventListener('das_email_settings_updated', handleSettingsUpdated)
    window.addEventListener('storage', handleSettingsUpdated)

    return () => {
      if (initialTimer) clearTimeout(initialTimer)
      if (intervalTimer) clearInterval(intervalTimer)
      window.removeEventListener('das_email_settings_updated', handleSettingsUpdated)
      window.removeEventListener('storage', handleSettingsUpdated)
    }
  }, [])

  return null
}

export default AutoEmailScanner
