'use client'

// React Imports
import { useState } from 'react'
import { useRouter, useParams } from 'next/navigation'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import Typography from '@mui/material/Typography'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { getLocalizedUrl } from '@/utils/i18n'
import type { Locale } from '@configs/i18n'
import { documentApi, fileApi, ocrApi, partnerApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

const AddDocumentForm = () => {
  const router = useRouter()
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  const [formData, setFormData] = useState({
    documentNumber: '',
    title: '',
    direction: 'incoming',
    issuedDate: new Date().toISOString().split('T')[0],
    partnerId: '',
    partnerName: '',
    summary: '',
    fileIds: [] as string[]
  })

  const [selectedFile, setSelectedFile] = useState<File | null>(null)
  const [ocrLoading, setOcrLoading] = useState(false)
  const [ocrResult, setOcrResult] = useState<{ text?: string; confidence?: number; matchedPartnerId?: string } | null>(null)
  const [submitLoading, setSubmitLoading] = useState(false)
  const [alertInfo, setAlertInfo] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null)

  // Handle OCR Scan — Kết hợp Backend AI-OCR Engine và Local Next.js PDF Engine
  const handleOcrScan = async () => {
    if (!selectedFile) {
      setAlertInfo({ type: 'error', message: 'Vui lòng chọn tệp PDF hoặc ảnh công văn trước khi quét OCR.' })
      return
    }

    setOcrLoading(true)
    setAlertInfo(null)

    let isProcessed = false

    // 1. Thử gọi Backend Microservice qua API Gateway
    try {
      const uploadRes = await fileApi.upload(selectedFile)
      const fileId = uploadRes?.data?.fileId || uploadRes?.data?.id || uploadRes?.fileId || uploadRes?.id

      if (fileId) {
        setFormData(prev => ({ ...prev, fileIds: [fileId] }))
        const ocrRes = await ocrApi.analyze(fileId)

        if (ocrRes?.success && ocrRes?.data) {
          const {
            extractedText,
            extractedReferenceNumber,
            extractedSubject,
            extractedDateString,
            extractedSigner,
            extractedDocumentType,
            matchedPartnerId,
            confidence
          } = ocrRes.data

          setOcrResult({ text: extractedText, confidence, matchedPartnerId })

          setFormData(prev => ({
            ...prev,
            documentNumber: extractedReferenceNumber || prev.documentNumber || '',
            title: extractedSubject || prev.title || '',
            issuedDate: extractedDateString ? convertDateToISO(extractedDateString) : prev.issuedDate,
            summary: extractedText ? extractedText.trim().substring(0, 2000) : prev.summary
          }))

          if (matchedPartnerId) {
            try {
              const partnerDetail = await partnerApi.getById(matchedPartnerId)
              const name = partnerDetail?.data?.fullName || partnerDetail?.fullName
              if (name) {
                setFormData(prev => ({ ...prev, partnerId: matchedPartnerId, partnerName: name }))
              }
            } catch {
              setFormData(prev => ({ ...prev, partnerId: matchedPartnerId }))
            }
          }

          const infoLines = []
          if (extractedReferenceNumber) infoLines.push(`Số hiệu: ${extractedReferenceNumber}`)
          if (extractedSubject) infoLines.push(`Trích yếu: ${extractedSubject.substring(0, 60)}...`)
          if (extractedDocumentType) infoLines.push(`Loại: ${extractedDocumentType}`)
          if (extractedSigner) infoLines.push(`Người ký: ${extractedSigner}`)
          if (confidence) infoLines.push(`Độ tin cậy: ${(confidence * 100).toFixed(0)}%`)

          setAlertInfo({
            type: 'success',
            message: `Quét AI OCR thành công! ${infoLines.length > 0 ? infoLines.join(' • ') : `Trích xuất ${(extractedText || '').length} ký tự.`}`
          })
          isProcessed = true
        }
      }
    } catch {
      // Backend microservice offline -> Chuyển sang Local Next.js Engine
    }

    // 2. Fallback sang Local Next.js Engine (Đọc nội dung PDF thực bằng pdf-parse)
    if (!isProcessed) {
      try {
        const localFormData = new FormData()
        localFormData.append('file', selectedFile)

        const localRes = await fetch('/api/ocr/analyze', {
          method: 'POST',
          body: localFormData
        })

        const localData = await localRes.json()

        if (localData.success && localData.data) {
          const {
            extractedText,
            extractedReferenceNumber,
            extractedSubject,
            extractedDateString,
            matchedPartnerName,
            confidence
          } = localData.data

          setOcrResult({ text: extractedText, confidence })

          setFormData(prev => ({
            ...prev,
            documentNumber: extractedReferenceNumber || prev.documentNumber || '',
            title: extractedSubject || prev.title || '',
            partnerName: matchedPartnerName || prev.partnerName || '',
            issuedDate: extractedDateString ? convertDateToISO(extractedDateString) : prev.issuedDate,
            summary: extractedText ? extractedText.trim().substring(0, 2000) : prev.summary,
            fileIds: prev.fileIds.length > 0 ? prev.fileIds : ['local-' + Date.now()]
          }))

          const infoLines = []
          if (extractedReferenceNumber) infoLines.push(`Số hiệu: ${extractedReferenceNumber}`)
          if (matchedPartnerName) infoLines.push(`Đơn vị: ${matchedPartnerName}`)
          if (extractedSubject) infoLines.push(`Trích yếu: ${extractedSubject.substring(0, 50)}...`)

          setAlertInfo({
            type: 'success',
            message: `Quét AI OCR thành công! ${infoLines.length > 0 ? infoLines.join(' • ') : `Đã bóc tách nội dung từ ${selectedFile.name}.`}`
          })
          isProcessed = true
        } else {
          throw new Error(localData.message || 'Lỗi bóc tách PDF')
        }
      } catch (err: any) {
        setAlertInfo({
          type: 'error',
          message: `Lỗi xử lý file: ${err.message || 'Không thể bóc tách nội dung.'}. Vui lòng nhập thông tin thủ công.`
        })
      }
    }

    setOcrLoading(false)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitLoading(true)
    setAlertInfo(null)

    try {
      const payload = {
        documentNumber: formData.documentNumber,
        title: formData.title,
        direction: formData.direction,
        issuedDate: formData.issuedDate,
        partnerId: formData.partnerId || undefined,
        partnerName: formData.partnerName || undefined,
        summary: formData.summary,
        fileIds: formData.fileIds
      }

      const res = await documentApi.create(payload)
      if (res?.success) {
        setAlertInfo({ type: 'success', message: 'Lưu công văn thành công! Đang chuyển hướng...' })
        setTimeout(() => {
          router.push(getLocalizedUrl('/apps/documents/list', locale as Locale))
        }, 1000)
      } else {
        throw new Error(res?.message || 'Không thể tạo công văn vào hệ thống.')
      }
    } catch (err: any) {
      setAlertInfo({ type: 'error', message: err.message || 'Lỗi khi lưu công văn.' })
      setSubmitLoading(false)
    }
  }

  return (
    <Card>
      <CardHeader
        title={t.documents.addTitle}
        subheader={t.documents.addSub}
      />
      <CardContent>
        {alertInfo && (
          <Alert severity={alertInfo.type} className='mbe-5' onClose={() => setAlertInfo(null)}>
            {alertInfo.message}
          </Alert>
        )}

        {/* OCR Scan Section */}
        <div className='p-4 rounded-lg bg-primary/[0.04] border border-primary/20 mbe-6'>
          <Typography variant='subtitle1' className='font-semibold flex items-center gap-2 mbe-2 text-primary'>
            <i className='tabler-sparkles text-xl' />
            {t.documents.ocrSectionTitle}
          </Typography>
          <Typography variant='body2' color='text.secondary' className='mbe-4'>
            {t.documents.ocrSectionDesc}
          </Typography>

          <div className='flex flex-wrap items-center gap-4'>
            <input
              type='file'
              accept='.pdf,.png,.jpg,.jpeg,.tiff'
              id='ocr-upload-file'
              className='hidden'
              onChange={e => {
                if (e.target.files && e.target.files[0]) {
                  setSelectedFile(e.target.files[0])
                }
              }}
            />
            <label htmlFor='ocr-upload-file'>
              <Button variant='outlined' component='span' startIcon={<i className='tabler-upload' />}>
                {selectedFile ? selectedFile.name : t.documents.chooseFile}
              </Button>
            </label>

            <Button
              variant='contained'
              color='primary'
              disabled={!selectedFile || ocrLoading}
              onClick={handleOcrScan}
              startIcon={ocrLoading ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-scan' />}
            >
              {ocrLoading ? t.documents.scanningBtn : t.documents.scanBtn}
            </Button>

            {ocrResult && ocrResult.confidence && (
              <Chip
                label={`${t.documents.confidenceBadge}: ${(ocrResult.confidence * 100).toFixed(0)}%`}
                color={ocrResult.confidence >= 0.95 ? 'success' : 'primary'}
                variant='tonal'
                icon={<i className='tabler-check' />}
              />
            )}
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          <Grid container spacing={5}>
            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                label={`${t.documents.docNumber} *`}
                placeholder='VD: 128/QĐ-BGDĐT'
                value={formData.documentNumber}
                onChange={e => setFormData({ ...formData, documentNumber: e.target.value })}
                required
              />
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                select
                label={`${t.documents.type} *`}
                value={formData.direction}
                onChange={e => setFormData({ ...formData, direction: e.target.value })}
                required
              >
                <MenuItem value='incoming'>{t.documents.incoming}</MenuItem>
                <MenuItem value='outgoing'>{t.documents.outgoing}</MenuItem>
              </CustomTextField>
            </Grid>

            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                label={`${t.documents.title} *`}
                placeholder='VD: Về việc hướng dẫn triển khai nhiệm vụ năm học mới'
                value={formData.title}
                onChange={e => setFormData({ ...formData, title: e.target.value })}
                required
              />
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                type='date'
                label={`${t.documents.issuedDate} *`}
                InputLabelProps={{ shrink: true }}
                value={formData.issuedDate}
                onChange={e => setFormData({ ...formData, issuedDate: e.target.value })}
                required
              />
            </Grid>

            <Grid size={{ xs: 12, md: 6 }}>
              <CustomTextField
                fullWidth
                label={t.documents.partner}
                placeholder='VD: Bộ Giáo dục và Đào tạo'
                value={formData.partnerName}
                onChange={e => setFormData({ ...formData, partnerName: e.target.value })}
              />
            </Grid>

            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                multiline
                rows={5}
                label={t.documents.summary}
                placeholder={t.documents.summaryPlaceholder}
                value={formData.summary}
                onChange={e => setFormData({ ...formData, summary: e.target.value })}
              />
            </Grid>

            <Grid size={{ xs: 12 }} className='flex items-center gap-4'>
              <Button
                type='submit'
                variant='contained'
                disabled={submitLoading}
                startIcon={submitLoading ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy' />}
              >
                {submitLoading ? t.documents.savingDoc : t.documents.saveDoc}
              </Button>
              <Button variant='tonal' color='secondary' onClick={() => router.back()}>
                {t.documents.cancel}
              </Button>
            </Grid>
          </Grid>
        </form>
      </CardContent>
    </Card>
  )
}

/** Chuyển ngày DD/MM/YYYY sang YYYY-MM-DD cho input date HTML */
function convertDateToISO(dateStr: string): string {
  const parts = dateStr.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/)
  if (parts) {
    return `${parts[3]}-${parts[2].padStart(2, '0')}-${parts[1].padStart(2, '0')}`
  }
  if (/^\d{4}-\d{2}-\d{2}$/.test(dateStr)) return dateStr
  return dateStr
}

export default AddDocumentForm
