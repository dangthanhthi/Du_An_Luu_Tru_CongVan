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
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import Tooltip from '@mui/material/Tooltip'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { getLocalizedUrl } from '@/utils/i18n'
import type { Locale } from '@configs/i18n'
import { documentApi, fileApi, ocrApi, partnerApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'
import { getCompanyConfig, saveCompanyConfig, type CompanyProfileConfig, parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

const AddDocumentForm = () => {
  const router = useRouter()
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  const [formData, setFormData] = useState({
    documentNumber: '',
    referenceNumber: '',
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
  const [ocrResult, setOcrResult] = useState<{ text?: string; confidence?: number; direction?: string; rationale?: string; matchedPartnerId?: string } | null>(null)
  const [submitLoading, setSubmitLoading] = useState(false)
  const [alertInfo, setAlertInfo] = useState<{ type: 'success' | 'error' | 'warning'; message: string } | null>(null)

  // Company Settings Modal State
  const [openCompanyModal, setOpenCompanyModal] = useState(false)
  const [companyForm, setCompanyForm] = useState<CompanyProfileConfig>(() => getCompanyConfig())

  const handleSaveCompanyConfig = () => {
    saveCompanyConfig(companyForm)
    setOpenCompanyModal(false)
    setAlertInfo({
      type: 'success',
      message: `Đã lưu thông tin công ty [${companyForm.companyName}]! Hệ thống AI OCR sẽ dựa vào tên này để tự động phân định Công văn đi, đến và nội bộ.`
    })
  }

  // Handle OCR Scan — Kết hợp Backend AI-OCR Engine và Local Next.js PDF Engine
  const handleOcrScan = async () => {
    if (!selectedFile) {
      setAlertInfo({ type: 'error', message: 'Vui lòng chọn tệp PDF hoặc ảnh công văn trước khi quét OCR.' })
      return
    }

    setOcrLoading(true)
    setAlertInfo(null)

    try {
      // 1. Quét trực tiếp qua Next.js AI OCR Engine (sử dụng unpdf + Tesseract Neural + Semantic Parser)
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
          extractedSigner,
          extractedSignerPosition,
          extractedDocumentType,
          extractedDirection,
          directionRationale,
          confidence
        } = localData.data

        const finalDir = extractedDirection || 'incoming'

        setOcrResult({
          text: extractedText,
          confidence,
          direction: finalDir,
          rationale: directionRationale
        })

        setFormData(prev => ({
          ...prev,
          referenceNumber: extractedReferenceNumber || '',
          title: extractedSubject || '',
          direction: finalDir,
          partnerName: matchedPartnerName || '',
          issuedDate: extractedDateString ? convertDateToISO(extractedDateString) : prev.issuedDate,
          summary: extractedText ? extractedText.trim().substring(0, 2000) : '',
          fileIds: prev.fileIds.length > 0 ? prev.fileIds : ['local-' + Date.now()]
        }))

        // Tải tệp lên backend storage nếu có kết nối
        fileApi.upload(selectedFile).then(uploadRes => {
          const fileId = uploadRes?.data?.fileId || uploadRes?.data?.id || uploadRes?.fileId || uploadRes?.id
          if (fileId) {
            setFormData(prev => ({ ...prev, fileIds: [fileId] }))
          }
        }).catch(() => {})

        const dirLabel = finalDir === 'internal' ? 'Công văn nội bộ' : finalDir === 'outgoing' ? 'Công văn đi' : 'Công văn đến'
        const infoLines = []
        infoLines.push(`Thể loại: ${dirLabel}`)
        if (extractedReferenceNumber) infoLines.push(`Số đối tác: ${extractedReferenceNumber}`)
        if (matchedPartnerName) infoLines.push(`Đơn vị: ${matchedPartnerName}`)
        if (extractedSigner) infoLines.push(`Người ký: ${extractedSignerPosition ? `${extractedSignerPosition} ` : ''}${extractedSigner}`)
        if (extractedSubject) infoLines.push(`Trích yếu: ${extractedSubject.substring(0, 50)}...`)

        setAlertInfo({
          type: 'success',
          message: `Quét AI OCR thành công! ${infoLines.join(' • ')}`
        })
      } else {
        throw new Error(localData.message || 'Lỗi bóc tách PDF')
      }
    } catch (err: any) {
      setAlertInfo({
        type: 'error',
        message: `Lỗi xử lý file: ${err.message || 'Không thể bóc tách nội dung.'}. Vui lòng nhập thông tin thủ công.`
      })
    } finally {
      setOcrLoading(false)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitLoading(true)
    setAlertInfo(null)

    try {
      const payload = {
        documentNumber: formData.documentNumber,
        referenceNumber: formData.referenceNumber,
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

          <div className='flex flex-wrap items-center justify-between gap-4'>
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

              {ocrResult && ocrResult.direction && (
                <Chip
                  label={`Loại: ${ocrResult.direction === 'internal' ? 'Công văn nội bộ' : ocrResult.direction === 'outgoing' ? 'Công văn đi' : 'Công văn đến'}`}
                  color={ocrResult.direction === 'internal' ? 'info' : ocrResult.direction === 'outgoing' ? 'success' : 'primary'}
                  variant='tonal'
                  icon={<i className={ocrResult.direction === 'internal' ? 'tabler-file-text' : ocrResult.direction === 'outgoing' ? 'tabler-arrow-up-right' : 'tabler-arrow-down-left'} />}
                />
              )}
            </div>

            <Button
              variant='tonal'
              color='secondary'
              size='small'
              startIcon={<i className='tabler-building-cog' />}
              onClick={() => setOpenCompanyModal(true)}
            >
              Cấu hình Tên Công Ty ({companyForm.shortName || 'DAS'})
            </Button>
          </div>
        </div>

        {/* Dialog Cài Đặt Tên Công Ty & Quy Tắc Nhận Diện */}
        <Dialog open={openCompanyModal} onClose={() => setOpenCompanyModal(false)} maxWidth='sm' fullWidth>
          <DialogTitle className='flex items-center gap-2'>
            <i className='tabler-building-cog text-2xl text-primary' />
            Cấu hình Thông tin Công Ty & Nhận diện Văn bản
          </DialogTitle>
          <DialogContent className='pt-4'>
            <Typography variant='body2' color='text.secondary' className='mbe-4'>
              Hệ thống AI OCR sử dụng Tên công ty và các Tên viết tắt bên dưới để tự động phân định văn bản:
              <br />
              • <b>Công văn đến:</b> Cơ quan bên ngoài gửi đến công ty.
              <br />
              • <b>Công văn đi:</b> Công ty phát hành gửi cho đối tác/cơ quan bên ngoài.
              <br />
              • <b>Công văn nội bộ:</b> Công ty phát hành gửi cho các phòng ban, chi nhánh hoặc cán bộ nhân viên nội bộ.
            </Typography>

            <Grid container spacing={4}>
              <Grid size={{ xs: 12 }}>
                <CustomTextField
                  fullWidth
                  label='Tên đầy đủ của Công ty / Đơn vị'
                  value={companyForm.companyName}
                  onChange={e => setCompanyForm({ ...companyForm, companyName: e.target.value })}
                  placeholder='VD: Công ty Cổ phần Quản trị Dữ liệu & Văn thư Số DAS'
                  required
                />
              </Grid>

              <Grid size={{ xs: 12, md: 6 }}>
                <CustomTextField
                  fullWidth
                  label='Tên viết tắt chính'
                  value={companyForm.shortName}
                  onChange={e => setCompanyForm({ ...companyForm, shortName: e.target.value })}
                  placeholder='VD: DAS'
                  required
                />
              </Grid>

              <Grid size={{ xs: 12, md: 6 }}>
                <CustomTextField
                  fullWidth
                  label='Mã số thuế'
                  value={companyForm.taxCode || ''}
                  onChange={e => setCompanyForm({ ...companyForm, taxCode: e.target.value })}
                  placeholder='VD: 0109988776'
                />
              </Grid>

              <Grid size={{ xs: 12 }}>
                <CustomTextField
                  fullWidth
                  multiline
                  rows={2}
                  label='Các tên viết tắt / Ký hiệu nhận diện (phân cách bằng dấu phẩy)'
                  value={companyForm.aliases.join(', ')}
                  onChange={e => setCompanyForm({
                    ...companyForm,
                    aliases: e.target.value.split(',').map(s => s.trim()).filter(Boolean)
                  })}
                  placeholder='VD: DAS, DAS Corp, DAS Group, Ban Giám Đốc DAS, Văn phòng DAS'
                  helperText='Bất kỳ văn bản nào có đơn vị phát hành chứa các tên này sẽ được nhận diện là do công ty ban hành'
                />
              </Grid>
            </Grid>
          </DialogContent>
          <DialogActions className='p-4 pt-0'>
            <Button variant='tonal' color='secondary' onClick={() => setOpenCompanyModal(false)}>
              Đóng
            </Button>
            <Button variant='contained' color='primary' onClick={handleSaveCompanyConfig} startIcon={<i className='tabler-check' />}>
              Lưu Cấu Hình
            </Button>
          </DialogActions>
        </Dialog>

        <form onSubmit={handleSubmit}>
          <Grid container spacing={5}>
            <Grid size={{ xs: 12, md: 4 }}>
              <CustomTextField
                fullWidth
                label='Số nội bộ công ty'
                placeholder='Tự động sinh (VD: CV-DEN-2026-0011)'
                helperText='Để trống nếu muốn hệ thống tự động cấp số theo chuẩn'
                value={formData.documentNumber}
                onChange={e => setFormData({ ...formData, documentNumber: e.target.value })}
              />
            </Grid>

            <Grid size={{ xs: 12, md: 4 }}>
              <CustomTextField
                fullWidth
                label='Số ký hiệu cơ quan / đối tác (OCR)'
                placeholder='VD: 2258/SGDĐT-VP'
                helperText='Số ký hiệu ban hành trên văn bản gốc'
                value={formData.referenceNumber}
                onChange={e => setFormData({ ...formData, referenceNumber: e.target.value })}
              />
            </Grid>

            <Grid size={{ xs: 12, md: 4 }}>
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
                <MenuItem value='internal'>{t.documents.internal || 'Công văn nội bộ'}</MenuItem>
              </CustomTextField>
            </Grid>

            <Grid size={{ xs: 12 }}>
              <CustomTextField
                fullWidth
                label={`${t.documents.title} *`}
                placeholder='VD: Về thời gian học tập đối với trẻ mầm non và học sinh trên địa bàn Thành phố'
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
                placeholder='VD: SỞ GIÁO DỤC VÀ ĐÀO TẠO THÀNH PHỐ HỒ CHÍ MINH'
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
