'use client'

// React Imports
import { useState, useEffect } from 'react'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Switch from '@mui/material/Switch'
import FormControlLabel from '@mui/material/FormControlLabel'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Paper from '@mui/material/Paper'
import MenuItem from '@mui/material/MenuItem'
import Tab from '@mui/material/Tab'
import TabContext from '@mui/lab/TabContext'
import TabList from '@mui/lab/TabList'
import TabPanel from '@mui/lab/TabPanel'
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import IconButton from '@mui/material/IconButton'
import Divider from '@mui/material/Divider'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { documentApi, fileApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'
import DocumentPDFPreview from '@/components/DocumentPDFPreview'
import { parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

// Initial Email Configuration
const DEFAULT_EMAIL_SETTINGS = {
  host: 'imap.gmail.com',
  port: 993,
  useSsl: true,
  email: '',
  appPassword: '',
  autoScan: true,
  intervalMinutes: 5,
  allowedSenderDomains: 'gmail.com, gov.vn, moe.gov.vn, moet.edu.vn, vnpt.vn, vnu.edu.vn',
  scanAttachmentsOnly: true
}

const DEFAULT_SCAN_LOGS: any[] = []

// Kiểm tra nghiêm ngặt xem email có đính kèm tệp PDF công văn thực tế hay không
export const hasValidPdfAttachment = (log: any): boolean => {
  if (!log) return false
  const raw = log.rawItem || log

  const sender = (log.sender || raw.sender || '').toLowerCase()
  // 1. Loại bỏ các tên miền mạng xã hội / thông báo / quảng cáo tự động
  if (
    sender.includes('facebookmail.com') ||
    sender.includes('youtube.com') ||
    sender.includes('shopee.vn') ||
    sender.includes('shopee.com') ||
    sender.includes('tiktok.com') ||
    sender.includes('instagram.com') ||
    sender.includes('linkedin.com') ||
    sender.includes('twitter.com') ||
    sender.includes('x.com') ||
    sender.includes('pinterest.com')
  ) {
    return false
  }

  // 2. Cờ tường minh no_pdf hoặc hasPdf = false
  if (log.status === 'no_pdf' || log.hasPdf === false || raw.hasPdf === false) {
    return false
  }

  const att = (log.attachment || raw.attachment || '').toLowerCase().trim()
  if (!att || att.includes('không có') || att.includes('no pdf') || att.includes('không đính kèm')) {
    return false
  }

  // 3. Loại bỏ tên tệp ảo VanBan_DinhKem.pdf do hệ thống cũ sinh ra nếu không có dung lượng/PDF thực tế
  if (att === 'vanban_dinhkem.pdf' || att === 'vanban_dinhkem_tu_email.pdf') {
    if (!raw.hasRealPdf && (!raw.pdfExtractedLength || raw.pdfExtractedLength <= 0)) {
      return false
    }
  }

  // 4. Bắt buộc tệp phải có đuôi .pdf
  if (!att.endsWith('.pdf')) {
    return false
  }

  // 5. Cờ xác nhận có PDF thật
  if (log.hasPdf === true || raw.hasPdf === true || raw.hasRealPdf === true) {
    return true
  }

  if (raw.fileUrl && typeof raw.fileUrl === 'string') {
    const u = raw.fileUrl.toLowerCase()
    if (u.startsWith('data:application/pdf') || (u.startsWith('/api/files/') && att.endsWith('.pdf') && att !== 'vanban_dinhkem.pdf')) {
      return true
    }
  }

  return false
}

const EmailIntegrationView = () => {
  const { t, isEn } = useAppDictionary()
  const [settings, setSettings] = useState(DEFAULT_EMAIL_SETTINGS)
  const [logs, setLogs] = useState(DEFAULT_SCAN_LOGS)
  const [tabValue, setTabValue] = useState('intake')
  const [isTesting, setIsTesting] = useState(false)
  const [isScanning, setIsScanning] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [notification, setNotification] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null)
  const [lastScanTime, setLastScanTime] = useState<string>('')

  // Intake & Classification Modal State
  const [intakeModalOpen, setIntakeModalOpen] = useState(false)
  const [selectedLog, setSelectedLog] = useState<any>(null)
  const [intakeDirection, setIntakeDirection] = useState<'incoming' | 'outgoing' | 'internal'>('incoming')
  const [intakeDocType, setIntakeDocType] = useState('Công văn')
  const [intakeRefNumber, setIntakeRefNumber] = useState('')
  const [intakeTitle, setIntakeTitle] = useState('')
  const [intakePartnerName, setIntakePartnerName] = useState('')
  const [intakeIssuedDate, setIntakeIssuedDate] = useState('')
  const [isSavingDoc, setIsSavingDoc] = useState(false)
  const [customFileUrl, setCustomFileUrl] = useState<string>('')
  const [customFileName, setCustomFileName] = useState<string>('')
  const [isUploadingCustomFile, setIsUploadingCustomFile] = useState(false)

  // PDF Preview Modal State
  const [pdfPreviewOpen, setPdfPreviewOpen] = useState(false)
  const [previewPdfUrl, setPreviewPdfUrl] = useState('')
  const [previewPdfName, setPreviewPdfName] = useState('')

  // Đọc và tự động chuẩn hóa logs (chuyển các email không có PDF sang status: 'no_pdf' và gỡ bỏ base64 nặng)
  const loadAndNormalizeLogs = () => {
    try {
      const savedLogsStr = localStorage.getItem('das_email_logs')
      if (savedLogsStr) {
        const parsed = JSON.parse(savedLogsStr)
        if (Array.isArray(parsed)) {
          let changed = false
          // Giữ tối đa 25 log gần nhất và loại bỏ hoàn toàn các chuỗi base64 khổng lồ
          const normalized = parsed.slice(0, 25).map((l: any) => {
            let itemChanged = false
            let raw = l.rawItem
            if (raw?.fileUrl && typeof raw.fileUrl === 'string' && raw.fileUrl.startsWith('data:')) {
              raw = { ...raw, fileUrl: '' }
              itemChanged = true
            }
            const hasPdf = hasValidPdfAttachment(l)
            if (!hasPdf) {
              if (
                l.status === 'pending_intake' ||
                l.status === 'pending_confirmation' ||
                l.hasPdf !== false ||
                l.attachment !== 'Không có tệp PDF' ||
                l.docNumber !== 'Không có PDF (Bỏ qua)'
              ) {
                itemChanged = true
                changed = true
              }
              return {
                ...l,
                rawItem: raw ? { ...raw, hasPdf: false, hasRealPdf: false, fileUrl: '' } : raw,
                attachment: 'Không có tệp PDF',
                hasPdf: false,
                status: 'no_pdf',
                docNumber: 'Không có PDF (Bỏ qua)',
                message: 'Email thông thường hoặc thông báo tự động, không có tệp PDF công văn đính kèm.'
              }
            }
            if (itemChanged) changed = true
            return itemChanged ? { ...l, rawItem: raw } : l
          })
          if (changed || parsed.length > 25) {
            try {
              localStorage.setItem('das_email_logs', JSON.stringify(normalized))
            } catch {}
          }
          setLogs(normalized)
          return
        }
      }
    } catch {}
  }

  // Load persisted settings & logs
  useEffect(() => {
    try {
      const savedSettings = localStorage.getItem('das_email_settings')
      if (savedSettings) setSettings(JSON.parse(savedSettings))
      const savedLastScan = localStorage.getItem('das_email_last_scan_time')
      if (savedLastScan) setLastScanTime(savedLastScan)
      loadAndNormalizeLogs()
    } catch {}

    const handleLogsUpdated = () => {
      loadAndNormalizeLogs()
    }

    const handleScanCompleted = (e: any) => {
      const ts = e?.detail?.timestamp || localStorage.getItem('das_email_last_scan_time')
      if (ts) setLastScanTime(ts)
      loadAndNormalizeLogs()
    }

    if (typeof window !== 'undefined') {
      window.addEventListener('das_email_logs_updated', handleLogsUpdated)
      window.addEventListener('das_email_scan_completed', handleScanCompleted)
      return () => {
        window.removeEventListener('das_email_logs_updated', handleLogsUpdated)
        window.removeEventListener('das_email_scan_completed', handleScanCompleted)
      }
    }
  }, [])

  // Save Settings Handler
  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault()
    setIsSaving(true)
    try {
      localStorage.setItem('das_email_settings', JSON.stringify(settings))
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new Event('das_email_settings_updated'))
      }
      setNotification({
        type: 'success',
        message: isEn
          ? `IMAP mailbox settings saved! Auto-scanner re-armed for every ${settings.intervalMinutes || 1} minute(s).`
          : `Đã lưu thành công cấu hình! Bộ tự động quét đã được thiết lập chu kỳ ${settings.intervalMinutes || 1} phút/lần.`
      })
    } catch {
      setNotification({
        type: 'error',
        message: isEn ? 'Failed to save settings.' : 'Không thể lưu cài đặt.'
      })
    } finally {
      setIsSaving(false)
    }
  }

  // Test IMAP Connection via real API
  const handleTestConnection = async () => {
    setIsTesting(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json()

      if (data.success) {
        setNotification({
          type: 'success',
          message: data.message || (isEn ? 'Connected to IMAP server successfully!' : 'Kết nối máy chủ IMAP thành công!')
        })
      } else {
        setNotification({
          type: 'error',
          message: data.message || (isEn ? 'Failed to connect to IMAP server.' : 'Không thể kết nối máy chủ IMAP.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err.message || (isEn ? 'Network error connecting to IMAP.' : 'Lỗi kết nối tới máy chủ IMAP.')
      })
    } finally {
      setIsTesting(false)
    }
  }

  // Trigger Real IMAP Email Scan into Intake Queue
  const handleTriggerScan = async () => {
    setIsScanning(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json()

      if (data.success && data.items && data.items.length > 0) {
        const newLogs: any[] = []
        let skippedDuplicateCount = 0

        const processedIds: string[] = JSON.parse(localStorage.getItem('das_processed_email_ids') || '[]')

        let currentLogs = [...logs]
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

          // Kiểm tra xem email này đã tồn tại trong danh sách logs chưa
          const existingIdx = currentLogs.findIndex(
            l => (l.messageId && item.messageId && l.messageId === item.messageId) ||
                 (l.id === item.id) ||
                 (l.sender === item.sender && l.subject === title)
          )

          if (existingIdx !== -1) {
            // Nếu đã tồn tại nhưng trước đó bị nhận diện nhầm là no_pdf, giờ phát hiện có PDF thật -> Cập nhật ngay!
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
            } else {
              skippedDuplicateCount++
            }
            continue
          }

          // Email mới hoàn toàn -> thêm vào danh sách
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

        const updatedLogs = currentLogs.slice(0, 25)
        setLogs(updatedLogs)
        try {
          localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs))
        } catch (storageErr) {
          console.warn('[Email Scan] Quota reached, pruning logs:', storageErr)
          try {
            localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs.slice(0, 10)))
          } catch {}
        }
        try {
          localStorage.setItem('das_processed_email_ids', JSON.stringify(processedIds.slice(-50)))
        } catch {}

        const newPdfCount = newLogs.filter(l => l.hasPdf).length
        if (newPdfCount > 0) {
          // Chuyển sang tab Hàng đợi tiếp nhận khi có công văn PDF mới
          setTabValue('intake')
          setNotification({
            type: 'success',
            message: isEn
              ? `Successfully scanned ${newPdfCount} new email(s) with PDF documents! Please review, select document type and save to registry.`
              : `Quét thành công! Đã phát hiện ${newPdfCount} công văn có tệp PDF đính kèm vào hàng đợi tiếp nhận.`
          })
        } else if (newLogs.length > 0) {
          setNotification({
            type: 'info',
            message: isEn
              ? `Scanned ${newLogs.length} new email(s), but none contained PDF attachments (kept in Logs tab).`
              : `Đã quét ${newLogs.length} email mới, tất cả đều là thư thông thường không có tệp PDF (được chuyển sang tab Nhật ký).`
          })
        } else if (skippedDuplicateCount > 0) {
          setNotification({
            type: 'info',
            message: isEn
              ? 'All scanned emails were already processed.'
              : 'Hộp thư không có email mới (các email trước đó đã được tải về hàng đợi).'
          })
        }
      } else if (data.success) {
        setNotification({
          type: 'info',
          message: data.message || (isEn ? 'No new unread emails found in mailbox.' : 'Hộp thư không có email mới chưa đọc.')
        })
      } else {
        setNotification({
          type: 'error',
          message: data.message || (isEn ? 'Error scanning email mailbox.' : 'Lỗi khi quét hòm thư. Vui lòng kiểm tra lại Email và Mật khẩu ứng dụng.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err.message || (isEn ? 'Error connecting to mail scanner.' : 'Lỗi trong quá trình kết nối và quét hộp thư.')
      })
    } finally {
      setIsScanning(false)
    }
  }

  // Mở Modal Phân Loại & Tiếp Nhận
  const handleOpenIntakeModal = (log: any) => {
    setSelectedLog(log)
    const raw = log.rawItem || {}

    // Xác định loại công văn gợi ý
    const dir = (log.suggestedType || raw.extractedDirection || 'incoming').toLowerCase()
    setIntakeDirection(dir.includes('out') ? 'outgoing' : dir.includes('inter') ? 'internal' : 'incoming')

    // Thể loại văn bản (Quyết định, Kế hoạch, Thông báo, Công văn...)
    setIntakeDocType(raw.extractedDocumentType || 'Công văn')

    // Số ký hiệu
    const refNum = raw.extractedRefNumber || 
      (log.docNumber?.match(/\(Ref:\s*([^)]+)\)/)?.[1]) || 
      (log.subject?.match(/(?:Số|No|Ref)[:.]?\s*([0-9]{1,5}\/[A-Z0-9Đ\-_]+(?:\/[0-9]{4})?)/i)?.[1]) || 
      ''
    setIntakeRefNumber(refNum)

    // Tiêu đề (Làm sạch các đoạn nối đính kèm hoặc mã công văn cũ nếu có)
    let rawTitle = raw.extractedTitle || log.subject || 'Công văn tiếp nhận từ Email'
    rawTitle = rawTitle.replace(/•\s*Tệp\s*đính\s*kèm:[\s\S]*$/i, '').replace(/\s*CV-(?:DEN|DI|NB)-\d{4}-\d{4}\s*$/i, '').trim()
    setIntakeTitle(rawTitle)

    // Cơ quan ban hành
    setIntakePartnerName(raw.extractedPartner || log.sender?.split('@')[1]?.split('.')[0]?.toUpperCase() || 'Chưa xác định')

    // Ngày ban hành (Ưu tiên tuyệt đối ngày bóc tách từ PDF, con dấu ký số, hoặc tên file máy quét)
    let autoDate = raw.extractedDate
    const todayStr = new Date().toLocaleDateString('vi-VN')
    if (!autoDate || autoDate === todayStr) {
      const meta = parseOcrDocumentMetadata({
        attachmentName: log.attachment || raw.attachment,
        title: rawTitle,
        summary: log.message,
        referenceNumber: refNum
      })
      if (meta.issuedDate) {
        autoDate = meta.issuedDate
      }
    }
    setIntakeIssuedDate(autoDate || '')

    setCustomFileUrl('')
    setCustomFileName('')
    setIntakeModalOpen(true)
  }

  // Tải lên hoặc thay thế tệp PDF trực tiếp trong Modal Tiếp Nhận
  const handleModalFileUpload = async (file: File) => {
    setIsUploadingCustomFile(true)
    try {
      const uploadRes = await fileApi.upload(file)
      const fId = uploadRes?.data?.id || uploadRes?.data?.fileId || uploadRes?.id
      const newFileUrl = uploadRes?.data?.fileUrl || (fId ? `/api/files/${fId}` : '')
      if (newFileUrl) {
        setCustomFileUrl(newFileUrl)
        setCustomFileName(uploadRes?.data?.originalName || file.name)
        setNotification({
          type: 'success',
          message: `Đã gắn tệp PDF '${file.name}' thành công! Hệ thống đang bóc tách nội dung...`
        })

        // Tự động quét AI OCR cho tệp mới đính kèm
        try {
          const formData = new FormData()
          formData.append('file', file)
          const ocrRes = await fetch('/api/ocr/analyze', { method: 'POST', body: formData })
          if (ocrRes.ok) {
            const ocrData = await ocrRes.json()
            if (ocrData?.success && ocrData?.data) {
              const d = ocrData.data
              if (d.extractedReferenceNumber) setIntakeRefNumber(d.extractedReferenceNumber)
              if (d.extractedSubject) setIntakeTitle(d.extractedSubject)
              if (d.matchedPartnerName) setIntakePartnerName(d.matchedPartnerName)
              if (d.extractedDateString) setIntakeIssuedDate(d.extractedDateString)
              if (d.extractedDocumentType) setIntakeDocType(d.extractedDocumentType)
              if (d.extractedDirection) setIntakeDirection(d.extractedDirection)
            }
          }
        } catch {}
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: `Lỗi tải file: ${err.message || 'Không thể tải tệp lên'}`
      })
    } finally {
      setIsUploadingCustomFile(false)
    }
  }

  // Thực hiện Lưu Công Văn Chính Thức
  const handleSaveDocument = async () => {
    if (!selectedLog) return
    setIsSavingDoc(true)

    const raw = selectedLog.rawItem || {}
    const logIndex = logs.findIndex(l => l.id === selectedLog.id)

    try {
      const activeFileUrl = customFileUrl || raw.fileUrl || ''
      const activeAttachment = customFileName || selectedLog.attachment || 'VanBan_DinhKem.pdf'

      // 1. Chuẩn bị payload tạo công văn
      const payload: any = {
        title: intakeTitle.trim() || 'Công văn tiếp nhận từ Email',
        summary: `Văn bản tiếp nhận từ hòm thư điện tử: ${selectedLog.sender}.\n• Đơn vị ban hành: ${intakePartnerName}\n• Số ký hiệu văn bản: ${intakeRefNumber || 'Chưa xác định'}\n• Thể loại: ${intakeDocType}\n• Ngày ban hành: ${intakeIssuedDate}\n• Trích yếu: ${intakeTitle}\n• Tệp đính kèm: ${activeAttachment}`,
        direction: intakeDirection,
        referenceNumber: intakeRefNumber.trim(),
        partnerName: intakePartnerName.trim(),
        issuedDate: intakeIssuedDate,
        senderEmail: selectedLog.sender,
        fileUrl: activeFileUrl,
        attachmentName: activeAttachment
      }

      // 2. Gọi documentApi.create (Đã được tích hợp upload PDF vào FilesService và lưu vào DocumentService CSDL)
      const res = await documentApi.create(payload)

      if (res.success && res.data) {
        const assignedDocNumber = res.data.documentNumber || 'CV-DEN-2026-0001'

        // 3. Cập nhật trạng thái trong danh sách Logs & Hàng đợi
        const updatedLogs = [...logs]
        if (logIndex !== -1) {
          updatedLogs[logIndex] = {
            ...selectedLog,
            status: 'success',
            docNumber: assignedDocNumber,
            direction: intakeDirection,
            savedDocId: res.data.id,
            attachment: activeAttachment,
            rawItem: {
              ...raw,
              fileUrl: (activeFileUrl && !activeFileUrl.startsWith('data:')) ? activeFileUrl : ''
            },
            message: `Đã tiếp nhận thành công vào sổ: ${assignedDocNumber}`
          }
          setLogs(updatedLogs)
          try {
            localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs))
          } catch {}
        }

        // 4. Bắn sự kiện cập nhật danh sách công văn toàn hệ thống
        window.dispatchEvent(new Event('das_documents_updated'))

        setNotification({
          type: 'success',
          message: isEn
            ? `Successfully registered document ${assignedDocNumber} into ${intakeDirection === 'incoming' ? 'Incoming' : intakeDirection === 'outgoing' ? 'Outgoing' : 'Internal'} documents!`
            : `Đã lưu thành công công văn số "${assignedDocNumber}" vào sổ "${intakeDirection === 'incoming' ? 'Công văn đến' : intakeDirection === 'outgoing' ? 'Công văn đi' : 'Công văn nội bộ'}"!`
        })

        setIntakeModalOpen(false)
      } else {
        throw new Error((res as any).message || 'Không thể tạo công văn vào cơ sở dữ liệu')
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: isEn
          ? `Failed to save document: ${err.message}`
          : `Lỗi khi lưu công văn vào sổ: ${err.message || 'Không rõ nguyên nhân'}`
      })
    } finally {
      setIsSavingDoc(false)
    }
  }

  // Mở Xem Trước Tệp PDF Thật
  const handleOpenPdfPreview = (log: any) => {
    const raw = log.rawItem || {}
    const pdfUrl = raw.fileUrl || ''
    if (!pdfUrl) {
      setNotification({
        type: 'info',
        message: isEn ? 'No PDF attached to this email.' : 'Email này không chứa tệp đính kèm PDF.'
      })
      return
    }
    setPreviewPdfUrl(pdfUrl)
    setPreviewPdfName(log.attachment || 'VanBan_DinhKem.pdf')
    setPdfPreviewOpen(true)
  }

  // HÀNG ĐỢI TIẾP NHẬN: BẮT BUỘC PHẢI CÓ TỆP PDF MỚI VÀO ĐÂY
  const intakeLogs = logs.filter(l => hasValidPdfAttachment(l))

  // Số lượng công văn PDF thực tế đang chờ phân loại & tiếp nhận
  const pendingCount = intakeLogs.filter(
    l => l.status === 'pending_intake' || l.status === 'pending_confirmation' || (l.docNumber && l.docNumber.includes('AUTO'))
  ).length

  // Xóa toàn bộ các email rác / thông báo không có PDF ra khỏi danh sách
  const handlePurgeNoPdfLogs = () => {
    const onlyPdfLogs = logs.filter(l => hasValidPdfAttachment(l))
    const removedCount = logs.length - onlyPdfLogs.length
    setLogs(onlyPdfLogs)
    try {
      localStorage.setItem('das_email_logs', JSON.stringify(onlyPdfLogs))
    } catch {}
    setNotification({
      type: 'success',
      message: isEn
        ? `Successfully removed ${removedCount} non-PDF emails from history!`
        : `Đã dọn dẹp thành công ${removedCount} email không có tệp PDF đính kèm khỏi hệ thống!`
    })
  }

  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <div className='flex flex-wrap items-center justify-between gap-4'>
          <div>
            <Typography variant='h4' className='font-bold flex items-center gap-2'>
              <i className='tabler-mail-spark text-2xl text-primary' />
              {isEn ? 'Email Integration & Document Intake Stream' : 'Tích Hợp Email & Hộp Thư Tiếp Nhận Công Văn'}
            </Typography>
            <Typography variant='body2' color='text.secondary'>
              {isEn 
                ? 'Monitors mailbox, extracts real PDF content via AI OCR, and stages incoming documents for officer review and classification.'
                : 'Giám sát hòm thư Gmail, tự động tải tệp PDF thật, bóc tách AI OCR và xếp vào luồng tiếp nhận để người nhận phân loại trước khi lưu.'}
            </Typography>
          </div>

          <div className='flex items-center gap-3'>
            {logs.some(l => !hasValidPdfAttachment(l)) && (
              <Button
                variant='tonal'
                color='error'
                startIcon={<i className='tabler-trash text-base' />}
                onClick={handlePurgeNoPdfLogs}
                sx={{ textTransform: 'none', fontWeight: 600 }}
              >
                {isEn ? 'Purge Non-PDF Emails' : 'Dọn Dẹp Email Không Có PDF'}
              </Button>
            )}

            <Button
              variant='contained'
              color='primary'
              disabled={isScanning}
              startIcon={isScanning ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-scan-eye' />}
              onClick={handleTriggerScan}
            >
              {isScanning ? (isEn ? 'Scanning...' : 'Đang Quét Hộp Thư...') : (isEn ? 'Scan Mailbox Now' : 'Kích Hoạt Quét Mail Ngay')}
            </Button>
          </div>
        </div>
      </Grid>

      {notification && (
        <Grid size={{ xs: 12 }}>
          <Alert severity={notification.type} onClose={() => setNotification(null)}>
            {notification.message}
          </Alert>
        </Grid>
      )}

      {/* 3 Thẻ Trạng Thái Hệ Thống */}
      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-primary'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.monitoredMailbox}</Typography>
              <Typography variant='h6' className='font-semibold'>{settings.email || 'Chưa cấu hình email'}</Typography>
              <div className='flex flex-wrap items-center gap-1.5 mbs-1'>
                <Chip
                  label={settings.autoScan !== false ? `Auto-Scan: ${settings.intervalMinutes || 1}p/lần` : 'Auto-Scan: Tắt'}
                  size='small'
                  color={settings.autoScan !== false ? 'success' : 'default'}
                  variant='tonal'
                />
                {lastScanTime ? (
                  <Chip
                    label={`Lần quét cuối: ${new Date(lastScanTime).toLocaleTimeString('vi-VN')}`}
                    size='small'
                    color='info'
                    variant='tonal'
                  />
                ) : (
                  <Chip label='IMAP SSL (Port 993)' size='small' color='primary' variant='tonal' />
                )}
              </div>
            </div>
            <i className='tabler-mail text-3xl text-primary opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-warning'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>
                {isEn ? 'Pending Intake Queue' : 'Hàng Đợi Chờ Phân Loại & Lưu'}
              </Typography>
              <Typography variant='h6' className='font-semibold text-warning'>
                {pendingCount} {isEn ? 'PDF documents waiting' : 'Công văn PDF chờ tiếp nhận'}
              </Typography>
              <Chip
                label={pendingCount > 0 ? (isEn ? 'Requires Officer Action' : 'Cần người nhận duyệt & chọn loại') : (isEn ? 'All Cleared' : 'Đã xử lý xong')}
                size='small'
                color={pendingCount > 0 ? 'warning' : 'success'}
                variant='tonal'
                className='mbs-1'
              />
            </div>
            <i className='tabler-inbox text-3xl text-warning opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-success'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.totalReceived}</Typography>
              <Typography variant='h6' className='font-semibold'>{logs.length} {isEn ? 'Total Scanned' : 'Tổng email đã quét'}</Typography>
              <Chip label='100% On-Premise AI OCR' size='small' color='success' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-file-check text-3xl text-success opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      {/* Tabs Điều Hướng */}
      <Grid size={{ xs: 12 }}>
        <Card>
          <TabContext value={tabValue}>
            <div className='border-b border-divider px-4'>
              <TabList onChange={(_, val) => setTabValue(val)}>
                <Tab 
                  label={
                    <div className='flex items-center gap-2'>
                      <span>{isEn ? 'Intake & Classification Queue' : 'Hàng Đợi Tiếp Nhận & Phân Loại'}</span>
                      {pendingCount > 0 && (
                        <Chip label={pendingCount} size='small' color='warning' className='h-5 text-xs font-bold' />
                      )}
                    </div>
                  } 
                  value='intake' 
                  icon={<i className='tabler-inbox' />} 
                  iconPosition='start' 
                />
                <Tab label={t.email.tabSettings} value='settings' icon={<i className='tabler-settings' />} iconPosition='start' />
                <Tab label={t.email.tabLogs} value='logs' icon={<i className='tabler-history' />} iconPosition='start' />
              </TabList>
            </div>

            {/* TAB 1: HÀNG ĐỢI TIẾP NHẬN & PHÂN LOẠI (LUỒNG RIÊNG - CHỈ CHỨA EMAIL CÓ PDF) */}
            <TabPanel value='intake'>
              <div className='mb-4 p-4 rounded-lg bg-primary/5 border border-primary/20 flex flex-wrap items-center justify-between gap-3'>
                <div className='flex items-center gap-3'>
                  <div className='p-2.5 rounded-full bg-primary/10 text-primary'>
                    <i className='tabler-shield-check text-2xl' />
                  </div>
                  <div>
                    <Typography variant='subtitle1' className='font-bold text-primary'>
                      {isEn ? 'Officer Document Intake & Verification Stream (PDF Only)' : 'Luồng Riêng Tiếp Nhận & Phân Loại Công Văn (Chỉ Tệp PDF)'}
                    </Typography>
                    <Typography variant='body2' color='text.secondary'>
                      {isEn 
                        ? 'Strict Filter Active: Only emails with genuine attached PDF documents are admitted here for official registration.'
                        : 'Quy tắc nghiêm ngặt: Chỉ các email có tệp đính kèm PDF công văn mới được đưa vào hàng đợi này để kiểm tra và phân loại.'}
                    </Typography>
                  </div>
                </div>

                {logs.some(l => !hasValidPdfAttachment(l)) && (
                  <Button
                    size='small'
                    variant='tonal'
                    color='error'
                    startIcon={<i className='tabler-trash text-sm' />}
                    onClick={handlePurgeNoPdfLogs}
                    sx={{ textTransform: 'none', fontWeight: 600 }}
                  >
                    {isEn ? 'Purge Non-PDF Emails' : 'Dọn Dẹp Email Không Có PDF'}
                  </Button>
                )}
              </div>

              {intakeLogs.length === 0 ? (
                <div className='text-center py-12'>
                  <div className='p-4 rounded-full bg-primary/10 text-primary inline-flex mb-3'>
                    <i className='tabler-file-certificate text-5xl' />
                  </div>
                  <Typography variant='h6' color='text.primary' className='font-bold mb-1'>
                    {isEn ? 'No PDF Documents Waiting for Intake' : 'Hiện Không Có Công Văn PDF Nào Chờ Tiếp Nhận'}
                  </Typography>
                  <Typography variant='body2' color='text.secondary' className='max-w-md mx-auto mb-4'>
                    {isEn 
                      ? 'The intake queue strictly accepts emails with valid PDF document attachments. Non-PDF emails (social notifications, spam) are filtered to the Logs tab.'
                      : 'Hàng đợi tiếp nhận chỉ lưu giữ các email có đính kèm tệp PDF công văn thực tế. Các email thông thường hoặc thông báo không có tệp PDF đã được lọc sang tab Nhật ký.'}
                  </Typography>
                  <Button variant='contained' color='primary' startIcon={<i className='tabler-scan-eye' />} onClick={handleTriggerScan} disabled={isScanning}>
                    {isEn ? 'Scan Mailbox Now' : 'Kích Hoạt Quét Mail Ngay'}
                  </Button>
                </div>
              ) : (
                <TableContainer component={Paper} elevation={0} className='border border-divider rounded-lg'>
                  <Table>
                    <TableHead>
                      <TableRow>
                        <TableCell width={140}>{isEn ? 'Received Time' : 'Thời Gian'}</TableCell>
                        <TableCell width={180}>{isEn ? 'Sender' : 'Người Gửi'}</TableCell>
                        <TableCell>{isEn ? 'Title / Subject' : 'Tiêu Đề / Trích Yếu'}</TableCell>
                        <TableCell width={180}>{isEn ? 'Attachment (PDF)' : 'Tệp Đính Kèm (PDF)'}</TableCell>
                        <TableCell width={190}>{isEn ? 'AI OCR Recognition' : 'AI OCR Bóc Tách'}</TableCell>
                        <TableCell width={180} align='center'>{isEn ? 'Intake Action' : 'Hành Động Tiếp Nhận'}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {intakeLogs.map(log => {
                        const raw = log.rawItem || {}
                        const isPending = log.status === 'pending_intake' || log.status === 'pending_confirmation' || (log.docNumber && log.docNumber.includes('AUTO'))
                        const refNum = raw.extractedRefNumber || (log.docNumber?.match(/\(Ref:\s*([^)]+)\)/)?.[1]) || ''

                        return (
                          <TableRow key={log.id} hover sx={{ bgcolor: isPending ? 'action.hover' : 'inherit' }}>
                            <TableCell>
                              <Typography variant='caption' className='font-mono block'>{log.timestamp}</Typography>
                            </TableCell>
                            <TableCell>
                              <Typography variant='body2' className='font-semibold truncate max-w-[170px]' title={log.sender}>
                                {log.sender}
                              </Typography>
                            </TableCell>
                            <TableCell>
                              <Typography variant='body2' className='font-medium line-clamp-2 max-w-[320px]' title={log.subject}>
                                {log.subject}
                              </Typography>
                              {raw.extractedPartner && (
                                <Typography variant='caption' color='text.secondary' className='block truncate max-w-[320px]'>
                                  Cơ quan: {raw.extractedPartner}
                                </Typography>
                              )}
                            </TableCell>
                            <TableCell>
                              <div className='flex flex-col gap-1 items-start'>
                                <div className='flex items-center gap-1.5 text-error'>
                                  <i className='tabler-file-type-pdf text-lg' />
                                  <Typography variant='caption' className='font-semibold truncate max-w-[140px]' title={log.attachment}>
                                    {log.attachment || 'VanBan.pdf'}
                                  </Typography>
                                </div>
                                {raw.fileUrl && (
                                  <Button
                                    size='small'
                                    variant='text'
                                    color='error'
                                    startIcon={<i className='tabler-eye text-xs' />}
                                    onClick={() => handleOpenPdfPreview(log)}
                                    sx={{ textTransform: 'none', p: 0, fontSize: '0.75rem', minWidth: 'auto' }}
                                  >
                                    {isEn ? 'Preview PDF' : 'Xem trước PDF'}
                                  </Button>
                                )}
                              </div>
                            </TableCell>
                            <TableCell>
                              {refNum ? (
                                <div className='flex flex-col gap-1'>
                                  <Chip
                                    label={`Số: ${refNum}`}
                                    size='small'
                                    color='secondary'
                                    variant='tonal'
                                    className='font-mono text-xs'
                                  />
                                  {raw.extractedDate && (
                                    <Typography variant='caption' color='text.secondary'>
                                      Ngày: {raw.extractedDate}
                                    </Typography>
                                  )}
                                </div>
                              ) : (
                                <Typography variant='caption' color='text.disabled'>
                                  {isEn ? 'Not detected' : 'Chưa nhận diện số'}
                                </Typography>
                              )}
                            </TableCell>
                            <TableCell align='center'>
                              {isPending ? (
                                <Button
                                  size='small'
                                  variant='contained'
                                  color='primary'
                                  startIcon={<i className='tabler-check-up text-sm' />}
                                  onClick={() => handleOpenIntakeModal(log)}
                                  sx={{ textTransform: 'none', py: 0.75, px: 2, fontWeight: 600, whiteSpace: 'nowrap' }}
                                >
                                  {isEn ? 'Select Type & Save' : 'Tiếp Nhận & Lưu CV'}
                                </Button>
                              ) : (
                                <div className='flex flex-col items-center gap-1'>
                                  <Chip
                                    label={`Đã lưu: ${log.docNumber}`}
                                    size='small'
                                    color='success'
                                    icon={<i className='tabler-circle-check text-xs' />}
                                    variant='tonal'
                                    className='font-semibold'
                                  />
                                  <Button
                                    size='small'
                                    variant='text'
                                    color='primary'
                                    href='/apps/documents/list'
                                    sx={{ textTransform: 'none', p: 0, fontSize: '0.72rem' }}
                                  >
                                    {isEn ? 'View in Registry →' : 'Mở sổ công văn →'}
                                  </Button>
                                </div>
                              )}
                            </TableCell>
                          </TableRow>
                        )
                      })}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </TabPanel>

            {/* TAB 2: CẤU HÌNH HÒM THƯ (SETTINGS) */}
            <TabPanel value='settings'>
              <form onSubmit={handleSaveSettings}>
                <Grid container spacing={5}>
                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle1' className='font-semibold text-primary flex items-center gap-2'>
                      <i className='tabler-server-2' />
                      {isEn ? 'Incoming Mail Server Parameters (IMAP)' : 'Thông Số Máy Chủ Nhận Thư (IMAP Server)'}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      label={`${t.email.imapHost} *`}
                      placeholder='imap.gmail.com'
                      value={settings.host}
                      onChange={e => setSettings({ ...settings, host: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 3 }}>
                    <CustomTextField
                      fullWidth
                      type='number'
                      label={`${t.email.port} *`}
                      placeholder='993'
                      value={settings.port}
                      onChange={e => setSettings({ ...settings, port: Number(e.target.value) })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 3 }} className='flex items-center'>
                    <FormControlLabel
                      control={
                        <Switch
                          checked={settings.useSsl}
                          onChange={e => setSettings({ ...settings, useSsl: e.target.checked })}
                          color='primary'
                        />
                      }
                      label={t.email.useSsl}
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      type='email'
                      label={`${t.email.emailAccount} *`}
                      placeholder='vanthu.benhvien@gmail.com'
                      value={settings.email}
                      onChange={e => setSettings({ ...settings, email: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      type='password'
                      label={`${t.email.appPassword} *`}
                      placeholder='••••••••••••••••'
                      value={settings.appPassword}
                      onChange={e => setSettings({ ...settings, appPassword: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle1' className='font-semibold text-primary flex items-center gap-2 mbs-2'>
                      <i className='tabler-filter' />
                      {isEn ? 'Filter Rules & Scheduling' : 'Quy Tắc Lọc & Tự Động Quét'}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      label={t.email.whitelistedDomains}
                      placeholder='gov.vn, moet.edu.vn, vnpt.vn'
                      value={settings.allowedSenderDomains}
                      onChange={e => setSettings({ ...settings, allowedSenderDomains: e.target.value })}
                      helperText={isEn ? 'Only intake and OCR documents from trusted domains' : 'Chỉ tiếp nhận và bóc tách công văn từ các miền tin cậy'}
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }} className='flex items-center'>
                    <FormControlLabel
                      control={
                        <Switch
                          checked={settings.autoScan !== false}
                          onChange={e => setSettings({ ...settings, autoScan: e.target.checked })}
                          color='primary'
                        />
                      }
                      label={isEn ? 'Enable Background Auto-Scanning' : 'Bật Tự Động Quét Ngầm Định Kỳ'}
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      select
                      label={t.email.scanInterval}
                      value={settings.intervalMinutes}
                      disabled={settings.autoScan === false}
                      onChange={e => setSettings({ ...settings, intervalMinutes: Number(e.target.value) })}
                    >
                      <MenuItem value={1}>{isEn ? '1 minute (Real-time)' : '1 phút / lần (Thời gian thực)'}</MenuItem>
                      <MenuItem value={5}>{isEn ? '5 minutes (Recommended)' : '5 phút / lần (Khuyến nghị)'}</MenuItem>
                      <MenuItem value={15}>{isEn ? '15 minutes' : '15 phút / lần'}</MenuItem>
                      <MenuItem value={30}>{isEn ? '30 minutes' : '30 phút / lần'}</MenuItem>
                      <MenuItem value={60}>{isEn ? '60 minutes' : '60 phút / lần'}</MenuItem>
                    </CustomTextField>
                  </Grid>

                  <Grid size={{ xs: 12 }} className='flex flex-wrap items-center gap-4 mbs-3'>
                    <Button
                      type='submit'
                      variant='contained'
                      color='primary'
                      disabled={isSaving}
                      startIcon={isSaving ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy text-lg' />}
                    >
                      {isSaving ? (isEn ? 'Saving...' : 'Đang Lưu...') : t.email.saveConfig}
                    </Button>

                    <Button
                      type='button'
                      variant='tonal'
                      color='primary'
                      disabled={isTesting}
                      startIcon={isTesting ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-plug-connected text-lg' />}
                      onClick={handleTestConnection}
                    >
                      {isTesting ? (isEn ? 'Testing...' : 'Đang Thử Kết Nối...') : t.email.testConnection}
                    </Button>
                  </Grid>
                </Grid>
              </form>
            </TabPanel>

            {/* TAB 3: LỊCH SỬ & NHẬT KÝ (LOGS) */}
            <TabPanel value='logs'>
              <TableContainer component={Paper} elevation={0} className='border border-divider rounded-lg'>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>{isEn ? 'Received Time' : 'Thời Gian Nhận'}</TableCell>
                      <TableCell>{isEn ? 'Sender (Email)' : 'Người Gửi (Email)'}</TableCell>
                      <TableCell>{isEn ? 'Email Subject' : 'Tiêu Đề Email'}</TableCell>
                      <TableCell>{isEn ? 'Attachment' : 'Tệp Đính Kèm'}</TableCell>
                      <TableCell>{isEn ? 'Registry Status' : 'Số Đã Vào Sổ'}</TableCell>
                      <TableCell>{isEn ? 'Audit Note' : 'Ghi Chú Xử Lý'}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {logs.map(log => (
                      <TableRow key={log.id} hover>
                        <TableCell>
                          <Typography variant='body2' className='font-mono'>{log.timestamp}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant='body2' className='font-semibold'>{log.sender}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant='body2' className='max-w-[240px] truncate'>{log.subject}</Typography>
                        </TableCell>
                        <TableCell>
                          <Typography variant='caption'>{log.attachment}</Typography>
                        </TableCell>
                        <TableCell>
                          <Chip
                            label={log.docNumber}
                            size='small'
                            color={log.status === 'success' ? 'success' : log.status === 'no_pdf' ? 'secondary' : 'warning'}
                            variant='tonal'
                          />
                        </TableCell>
                        <TableCell>
                          <Typography variant='caption' color='text.secondary'>{log.message}</Typography>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </TabPanel>
          </TabContext>
        </Card>
      </Grid>

      {/* ========================================================================= */}
      {/* DIALOG: TIẾP NHẬN & PHÂN LOẠI CÔNG VĂN (MODAL RIÊNG THEO YÊU CẦU CỦA BẠN) */}
      {/* ========================================================================= */}
      <Dialog
        open={intakeModalOpen}
        onClose={() => setIntakeModalOpen(false)}
        maxWidth='md'
        fullWidth
        scroll='paper'
      >
        <DialogTitle className='flex items-center justify-between pb-2 border-b border-divider'>
          <div className='flex items-center gap-2 text-primary'>
            <i className='tabler-mail-spark text-2xl' />
            <Typography variant='h5' className='font-bold text-primary'>
              {isEn ? 'Intake & Classify Document from Email' : 'Tiếp Nhận & Phân Loại Công Văn Từ Gmail'}
            </Typography>
          </div>
          <IconButton onClick={() => setIntakeModalOpen(false)} size='small'>
            <i className='tabler-x' />
          </IconButton>
        </DialogTitle>

        <DialogContent dividers className='p-6'>
          <Grid container spacing={5}>
            {/* THÔNG TIN NGUỒN GMAIL */}
            <Grid size={{ xs: 12 }}>
              <Alert severity='info' icon={<i className='tabler-mail' />} className='text-sm'>
                <strong>Người gửi:</strong> {selectedLog?.sender} | <strong>Tệp đính kèm:</strong> {selectedLog?.attachment}
              </Alert>
            </Grid>

            {/* PHẦN 1: CHỌN LOẠI CÔNG VĂN (BẮT BUỘC) */}
            <Grid size={{ xs: 12 }}>
              <Typography variant='subtitle1' className='font-bold text-textPrimary flex items-center gap-1.5 mb-2'>
                <i className='tabler-category text-primary text-xl' />
                1. Chọn Loại Công Văn Vào Sổ <span className='text-error'>*</span>
              </Typography>
              <Grid container spacing={3}>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <Card
                    onClick={() => setIntakeDirection('incoming')}
                    className={`cursor-pointer transition-all border-2 p-3.5 text-center rounded-lg ${
                      intakeDirection === 'incoming'
                        ? 'border-primary bg-primary/10 shadow-md'
                        : 'border-divider hover:border-primary/50'
                    }`}
                  >
                    <i className={`tabler-arrow-down-left text-3xl mb-1 ${intakeDirection === 'incoming' ? 'text-primary' : 'text-textSecondary'}`} />
                    <Typography variant='subtitle1' className={`font-bold ${intakeDirection === 'incoming' ? 'text-primary' : 'text-textPrimary'}`}>
                      📥 Công Văn Đến
                    </Typography>
                    <Typography variant='caption' color='text.secondary'>
                      Văn bản từ cơ quan bên ngoài gửi đến
                    </Typography>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, sm: 4 }}>
                  <Card
                    onClick={() => setIntakeDirection('outgoing')}
                    className={`cursor-pointer transition-all border-2 p-3.5 text-center rounded-lg ${
                      intakeDirection === 'outgoing'
                        ? 'border-success bg-success/10 shadow-md'
                        : 'border-divider hover:border-success/50'
                    }`}
                  >
                    <i className={`tabler-arrow-up-right text-3xl mb-1 ${intakeDirection === 'outgoing' ? 'text-success' : 'text-textSecondary'}`} />
                    <Typography variant='subtitle1' className={`font-bold ${intakeDirection === 'outgoing' ? 'text-success' : 'text-textPrimary'}`}>
                      📤 Công Văn Đi
                    </Typography>
                    <Typography variant='caption' color='text.secondary'>
                      Văn bản phát hành gửi ra bên ngoài
                    </Typography>
                  </Card>
                </Grid>

                <Grid size={{ xs: 12, sm: 4 }}>
                  <Card
                    onClick={() => setIntakeDirection('internal')}
                    className={`cursor-pointer transition-all border-2 p-3.5 text-center rounded-lg ${
                      intakeDirection === 'internal'
                        ? 'border-warning bg-warning/10 shadow-md'
                        : 'border-divider hover:border-warning/50'
                    }`}
                  >
                    <i className={`tabler-arrows-left-right text-3xl mb-1 ${intakeDirection === 'internal' ? 'text-warning' : 'text-textSecondary'}`} />
                    <Typography variant='subtitle1' className={`font-bold ${intakeDirection === 'internal' ? 'text-warning' : 'text-textPrimary'}`}>
                      🔄 Công Văn Nội Bộ
                    </Typography>
                    <Typography variant='caption' color='text.secondary'>
                      Văn bản lưu hành nội bộ đơn vị
                    </Typography>
                  </Card>
                </Grid>
              </Grid>
            </Grid>

            <Grid size={{ xs: 12 }}>
              <Divider />
            </Grid>

            {/* PHẦN 2: THÔNG TIN BÓC TÁCH VÀ HIỆU CHỈNH */}
            <Grid size={{ xs: 12 }}>
              <Typography variant='subtitle1' className='font-bold text-textPrimary flex items-center gap-1.5 mb-2'>
                <i className='tabler-edit text-primary text-xl' />
                2. Kiểm Tra & Chuẩn Hóa Thông Tin Văn Bản
              </Typography>
            </Grid>

            {/* Hình thức văn bản */}
            <Grid size={{ xs: 12, sm: 6 }}>
              <CustomTextField
                fullWidth
                select
                label='Thể Loại Văn Bản (Tên Loại) *'
                value={intakeDocType}
                onChange={e => setIntakeDocType(e.target.value)}
              >
                <MenuItem value='Công văn'>Công văn (CV)</MenuItem>
                <MenuItem value='Quyết định'>Quyết định (QĐ)</MenuItem>
                <MenuItem value='Kế hoạch'>Kế hoạch (KH)</MenuItem>
                <MenuItem value='Thông báo'>Thông báo (TB)</MenuItem>
                <MenuItem value='Chỉ thị'>Chỉ thị (CT)</MenuItem>
                <MenuItem value='Tờ trình'>Tờ trình (TTr)</MenuItem>
                <MenuItem value='Báo cáo'>Báo cáo (BC)</MenuItem>
                <MenuItem value='Nghị quyết'>Nghị quyết (NQ)</MenuItem>
                <MenuItem value='Hợp đồng'>Hợp đồng (HĐ)</MenuItem>
                <MenuItem value='Biên bản'>Biên bản (BB)</MenuItem>
                <MenuItem value='Quy chế'>Quy chế (QC)</MenuItem>
                <MenuItem value='Hướng dẫn'>Hướng dẫn (HD)</MenuItem>
              </CustomTextField>
            </Grid>

            {/* Số ký hiệu */}
            <Grid size={{ xs: 12, sm: 6 }}>
              <CustomTextField
                fullWidth
                label='Số Ký Hiệu Văn Bản (Reference Number) *'
                placeholder='Ví dụ: 2595/BTTTT-CBC hoặc 850/KH-SYT'
                value={intakeRefNumber}
                onChange={e => setIntakeRefNumber(e.target.value)}
                helperText='Số ký hiệu gốc do cơ quan ban hành phát hành'
              />
            </Grid>

            {/* Cơ quan ban hành */}
            <Grid size={{ xs: 12, sm: 6 }}>
              <CustomTextField
                fullWidth
                label='Cơ Quan Ban Hành / Đơn Vị Gửi *'
                placeholder='Ví dụ: Bộ Thông tin và Truyền thông, Sở Y tế TP.HCM'
                value={intakePartnerName}
                onChange={e => setIntakePartnerName(e.target.value)}
              />
            </Grid>

            {/* Ngày ban hành */}
            <Grid size={{ xs: 12, sm: 6 }}>
              <CustomTextField
                fullWidth
                label='Ngày Ban Hành *'
                placeholder='dd/MM/yyyy'
                value={intakeIssuedDate}
                onChange={e => setIntakeIssuedDate(e.target.value)}
              />
            </Grid>

            {/* Trích yếu tiêu đề */}
            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                multiline
                rows={2}
                label='Trích Yếu Nội Dung / Tiêu Đề Văn Bản *'
                placeholder='Nhập nội dung trích yếu của văn bản...'
                value={intakeTitle}
                onChange={e => setIntakeTitle(e.target.value)}
              />
            </Grid>

            {/* Tệp đính kèm PDF thật */}
            <Grid size={{ xs: 12 }}>
              <div className='p-4 rounded-lg border border-divider bg-actionHover flex flex-wrap items-center justify-between gap-3'>
                <div className='flex items-center gap-3'>
                  <i className='tabler-file-type-pdf text-3xl text-error' />
                  <div>
                    <Typography variant='subtitle2' className='font-semibold'>
                      {customFileName || selectedLog?.attachment || 'VanBan_DinhKem.pdf'}
                    </Typography>
                    <Typography variant='caption' color='text.secondary'>
                      {customFileName ? 'Đã đính kèm tệp PDF mới (đã đồng bộ lưu trữ và tự động bóc tách thông tin).' : 'Tệp PDF thật 100% đính kèm trong email sẽ được tự động lưu trữ vào Files Service.'}
                    </Typography>
                  </div>
                </div>

                <div className='flex items-center gap-2'>
                  <Button
                    component='label'
                    variant='tonal'
                    color='primary'
                    size='small'
                    disabled={isUploadingCustomFile}
                    startIcon={isUploadingCustomFile ? <CircularProgress size={16} color='inherit' /> : <i className='tabler-paperclip' />}
                  >
                    {isUploadingCustomFile ? 'Đang tải tệp...' : 'Đính kèm / Đổi tệp PDF'}
                    <input
                      type='file'
                      hidden
                      accept='application/pdf'
                      onChange={(e) => {
                        const f = e.target.files?.[0]
                        if (f) handleModalFileUpload(f)
                      }}
                    />
                  </Button>

                  {(customFileUrl || selectedLog?.rawItem?.fileUrl) && (
                    <Button
                      variant='outlined'
                      color='error'
                      size='small'
                      startIcon={<i className='tabler-eye' />}
                      onClick={() => {
                        setPreviewPdfUrl(customFileUrl || selectedLog?.rawItem?.fileUrl || '')
                        setPreviewPdfName(customFileName || selectedLog?.attachment || 'VanBan_DinhKem.pdf')
                        setPdfPreviewOpen(true)
                      }}
                    >
                      Xem Trước File PDF
                    </Button>
                  )}
                </div>
              </div>
            </Grid>
          </Grid>
        </DialogContent>

        <DialogActions className='p-4 border-t border-divider flex items-center justify-between'>
          <Button variant='tonal' color='secondary' onClick={() => setIntakeModalOpen(false)} disabled={isSavingDoc}>
            {isEn ? 'Cancel' : 'Hủy Bỏ'}
          </Button>

          <Button
            variant='contained'
            color='primary'
            disabled={isSavingDoc || !intakeTitle.trim()}
            startIcon={isSavingDoc ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy' />}
            onClick={handleSaveDocument}
          >
            {isSavingDoc ? (isEn ? 'Saving to Registry...' : 'Đang Lưu Vào Sổ...') : (isEn ? 'Confirm & Save into Registry' : 'Xác Nhận & Lưu Vào Sổ Công Văn')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ========================================================================= */}
      {/* MODAL: XEM TRƯỚC TỆP PDF THẬT                                              */}
      {/* ========================================================================= */}
      <Dialog
        open={pdfPreviewOpen}
        onClose={() => setPdfPreviewOpen(false)}
        maxWidth='lg'
        fullWidth
      >
        <DialogTitle className='flex items-center justify-between pb-2 border-b border-divider'>
          <div className='flex items-center gap-2 text-error'>
            <i className='tabler-file-type-pdf text-2xl' />
            <Typography variant='h6' className='font-bold'>
              {previewPdfName || 'Xem Trước Tệp PDF Thực Tế'}
            </Typography>
          </div>
          <IconButton onClick={() => setPdfPreviewOpen(false)} size='small'>
            <i className='tabler-x' />
          </IconButton>
        </DialogTitle>
        <DialogContent className='p-4'>
          <DocumentPDFPreview
            pdfUrl={previewPdfUrl}
            fileName={previewPdfName}
          />
        </DialogContent>
      </Dialog>
    </Grid>
  )
}

export default EmailIntegrationView
