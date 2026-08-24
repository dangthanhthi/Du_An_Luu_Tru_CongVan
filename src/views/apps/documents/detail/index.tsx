'use client'

// React Imports
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'
import Button from '@mui/material/Button'
import Divider from '@mui/material/Divider'
import Timeline from '@mui/lab/Timeline'
import TimelineItem from '@mui/lab/TimelineItem'
import TimelineSeparator from '@mui/lab/TimelineSeparator'
import TimelineConnector from '@mui/lab/TimelineConnector'
import TimelineContent from '@mui/lab/TimelineContent'
import TimelineDot from '@mui/lab/TimelineDot'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'

// API Imports
import { documentApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Component Imports
import DocumentQRCode from '@/components/DocumentQRCode'
import DocumentPDFPreview from '@/components/DocumentPDFPreview'
import { parseOcrDocumentMetadata } from '@/utils/ocrExtractor'

const DocumentDetail = ({ id }: { id: string }) => {
  const router = useRouter()
  const { t, isEn } = useAppDictionary()
  const [doc, setDoc] = useState<any>(null)
  const [loading, setLoading] = useState(true)
  const [actionAlert, setActionAlert] = useState<{ type: 'success' | 'info'; message: string } | null>(null)

  useEffect(() => {
    const fetchDoc = async () => {
      setLoading(true)
      try {
        const res = await documentApi.getById(id)
        if (res?.success && res?.data) {
          setDoc(res.data)
        }
      } catch {}
      setLoading(false)
    }
    if (id) {
      fetchDoc()
    }
  }, [id])

  const handleStatusChange = async (newStatus: string, actionName: string) => {
    if (!doc) return
    const updated = { ...doc, status: newStatus }
    setDoc(updated)
    await documentApi.update(id, { status: newStatus })
    setActionAlert({
      type: 'success',
      message: `${actionName}: #${doc.documentNumber || id}`
    })
  }

  const getStatusLabel = (status: string) => {
    switch (status) {
      case 'completed': return t.documents.completed
      case 'processing': return t.documents.processing
      case 'rejected': return t.documents.rejected
      default: return t.documents.pending
    }
  }

  if (loading) {
    return (
      <div className='flex justify-center items-center min-bs-[300px]'>
        <CircularProgress />
      </div>
    )
  }

  if (!doc) {
    return (
      <Card>
        <CardContent>
          <Alert severity='warning'>{t.documents.emptyData}</Alert>
          <Button variant='contained' className='mbs-4' onClick={() => router.back()}>{t.documents.cancel}</Button>
        </CardContent>
      </Card>
    )
  }

  // Tệp PDF THẬT 100% từ email (Data URI Base64) - Tuyệt đối không dùng file mẫu có sẵn
  const pdfUrl = doc?.fileUrl || ''
  const fileName = doc.documentNumber ? `${doc.documentNumber.replace(/[\/\\:]/g, '_')}.pdf` : 'VanBan_DinhKem.pdf'

  // Bóc tách động thông tin chuẩn xác qua AI OCR Parser
  const meta = parseOcrDocumentMetadata(doc)
  const partnerRefNumber = meta.referenceNumber
  const displayPartnerName = meta.partnerName
  const displayTitle = meta.title
  const displayDate = meta.issuedDate

  return (
    <Grid container spacing={6}>
      {actionAlert && (
        <Grid size={{ xs: 12 }}>
          <Alert severity={actionAlert.type} onClose={() => setActionAlert(null)}>
            {actionAlert.message}
          </Alert>
        </Grid>
      )}

      {/* Left Column (Metadata & PDF Preview & Timeline) */}
      <Grid size={{ xs: 12, md: 8 }}>
        <Grid container spacing={6}>
          {/* Metadata Card */}
          <Grid size={{ xs: 12 }}>
            <Card>
              <CardHeader
                title={
                  <div className='flex flex-col gap-1'>
                    <Typography variant='h5' className='font-bold'>
                      {displayTitle || doc.title || `${t.documents.docNumber}: ${doc.documentNumber}`}
                    </Typography>
                    <div className='flex flex-wrap items-center gap-3 mbs-1'>
                      <Chip
                        label={`${doc.direction === 'internal' ? 'Số nội bộ' : doc.direction === 'outgoing' ? 'Số đi nội bộ' : 'Số đến nội bộ'}: ${doc.documentNumber || 'CV-DEN-2026-0001'}`}
                        color={doc.direction === 'internal' ? 'info' : doc.direction === 'outgoing' ? 'success' : 'primary'}
                        size='small'
                        variant='filled'
                      />
                      {partnerRefNumber && (
                        <Chip
                          label={`Số hiệu đối tác: ${partnerRefNumber}`}
                          color='secondary'
                          size='small'
                          variant='tonal'
                        />
                      )}
                    </div>
                  </div>
                }
                action={
                  <div className='flex items-center gap-2'>
                    <Chip
                      label={getStatusLabel(doc.status)}
                      color={
                        doc.status === 'completed' ? 'success' :
                        doc.status === 'processing' ? 'info' :
                        doc.status === 'rejected' ? 'error' : 'warning'
                      }
                    />
                  </div>
                }
              />
              <Divider />
              <CardContent>
                <Grid container spacing={4}>
                  {/* 1. SỐ ĐẾM NỘI BỘ (1 - VÔ HẠN) */}
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant='subtitle2' color='text.secondary'>
                      {t.documents.internalDocNumber || 'Số Đếm Nội Bộ (1 - Vô Hạn)'}
                    </Typography>
                    <Typography variant='body1' sx={{ fontWeight: 700, color: 'primary.main', fontSize: '1.05rem' }}>
                      {doc.documentNumber || 'CV-DEN-2026-0001'}
                    </Typography>
                  </Grid>

                  {/* 2. SỐ KÝ HIỆU CỦA ĐỐI TÁC BAN HÀNH (REFERENCE NO.) */}
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant='subtitle2' color='text.secondary'>
                      {t.documents.referenceNumber || 'Số Ký Hiệu Đối Tác (Reference No.)'}
                    </Typography>
                    <Typography variant='body1' sx={{ fontWeight: 700, color: 'text.primary', fontSize: '1.05rem' }}>
                      {partnerRefNumber}
                    </Typography>
                  </Grid>

                  {/* 3. PHÂN LOẠI CÔNG VĂN */}
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant='subtitle2' color='text.secondary'>{t.documents.type}</Typography>
                    <div className='mbs-1'>
                      {doc.direction === 'incoming' ? (
                        <Chip
                          label={t.documents.incoming || 'Công Văn Đến'}
                          color='primary'
                          size='small'
                          variant='tonal'
                          icon={<i className='tabler-arrow-down-left' />}
                        />
                      ) : doc.direction === 'outgoing' ? (
                        <Chip
                          label={t.documents.outgoing || 'Công Văn Đi'}
                          color='success'
                          size='small'
                          variant='tonal'
                          icon={<i className='tabler-arrow-up-right' />}
                        />
                      ) : (
                        <Chip
                          label={t.documents.internal || 'Công Văn Nội Bộ'}
                          color='info'
                          size='small'
                          variant='tonal'
                          icon={<i className='tabler-file-description' />}
                        />
                      )}
                    </div>
                  </Grid>

                  {/* 4. NGÀY BAN HÀNH */}
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant='subtitle2' color='text.secondary'>{t.documents.issuedDate}</Typography>
                    <Typography variant='body1' sx={{ fontWeight: 500 }}>{displayDate || doc.issuedDate || '12/08/2026'}</Typography>
                  </Grid>

                  {/* 5. CƠ QUAN / ĐỐI TÁC GỬI */}
                  <Grid size={{ xs: 12, sm: 6 }}>
                    <Typography variant='subtitle2' color='text.secondary'>{t.documents.partner}</Typography>
                    <Typography variant='body1' sx={{ fontWeight: 600 }}>
                      {displayPartnerName}
                    </Typography>
                    {doc.senderEmail && (
                      <Typography variant='caption' color='text.secondary'>
                        Email: {doc.senderEmail}
                      </Typography>
                    )}
                  </Grid>

                  {/* 6. TIÊU ĐỀ / TRÍCH YẾU CÔNG VĂN */}
                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle2' color='text.secondary'>{t.documents.title}</Typography>
                    <Typography variant='body1' sx={{ fontWeight: 600, color: 'text.primary' }}>
                      {displayTitle || doc.title}
                    </Typography>
                  </Grid>

                  {/* 7. BÓC TÁCH NỘI DUNG TỔNG QUAN */}
                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle2' color='text.secondary'>{t.documents.summary}</Typography>
                    <Typography variant='body2' className='p-4 rounded bg-actionHover text-textPrimary leading-relaxed whitespace-pre-line border border-divider'>
                      {meta.summary || doc.summary}
                    </Typography>
                  </Grid>
                </Grid>
              </CardContent>
            </Card>
          </Grid>

          {/* Interactive PDF Preview Component */}
          <Grid size={{ xs: 12 }}>
            <DocumentPDFPreview
              pdfUrl={pdfUrl}
              fileName={fileName}
              docNumber={doc.documentNumber}
              summaryText={doc.summary}
            />
          </Grid>

          {/* Workflow & Processing History */}
          <Grid size={{ xs: 12 }}>
            <Card>
              <CardHeader title={t.documents.workflowLog} />
              <Divider />
              <CardContent>
                <Timeline position='right'>
                  <TimelineItem>
                    <TimelineSeparator>
                      <TimelineDot color='primary' />
                      <TimelineConnector />
                    </TimelineSeparator>
                    <TimelineContent>
                      <Typography variant='subtitle2' className='font-semibold'>
                        {isEn ? 'Intake & AI OCR Scan' : 'Tiếp nhận & Quét AI OCR'}
                      </Typography>
                      <Typography variant='caption' color='text.secondary'>
                        {isEn ? 'Secretary Officer • 14/08/2026 08:30' : 'Cán bộ văn thư • 14/08/2026 08:30'}
                      </Typography>
                      <Typography variant='body2' className='mbs-1'>
                        {isEn ? 'Document file extracted and partner matched automatically.' : 'Tệp văn bản đã được bóc tách nội dung và so khớp cơ quan gửi.'}
                      </Typography>
                    </TimelineContent>
                  </TimelineItem>

                  <TimelineItem>
                    <TimelineSeparator>
                      <TimelineDot color='info' />
                      <TimelineConnector />
                    </TimelineSeparator>
                    <TimelineContent>
                      <Typography variant='subtitle2' className='font-semibold'>
                        {isEn ? 'Forwarded to Department' : 'Phân phối phòng ban chuyên môn'}
                      </Typography>
                      <Typography variant='caption' color='text.secondary'>
                        {isEn ? 'Admin Head • 14/08/2026 09:15' : 'Trưởng phòng Hành chính • 14/08/2026 09:15'}
                      </Typography>
                      <Typography variant='body2' className='mbs-1'>
                        {isEn ? 'Transferred to IT & HR Departments for execution.' : 'Chuyển Ban CNTT và Phòng Tổ chức cán bộ nghiên cứu triển khai.'}
                      </Typography>
                    </TimelineContent>
                  </TimelineItem>

                  <TimelineItem>
                    <TimelineSeparator>
                      <TimelineDot color={doc.status === 'completed' ? 'success' : 'grey'} />
                    </TimelineSeparator>
                    <TimelineContent>
                      <Typography variant='subtitle2' className='font-semibold'>
                        {isEn ? 'Approval & Archiving' : 'Phê duyệt & Lưu trữ hồ sơ'}
                      </Typography>
                      <Typography variant='caption' color='text.secondary'>
                        {isEn ? 'Directorate • 14/08/2026 14:00' : 'Ban Giám Đốc • 14/08/2026 14:00'}
                      </Typography>
                      <Typography variant='body2' className='mbs-1'>
                        {isEn ? 'Digitally signed and archived into permanent storage.' : 'Đã ký số phê duyệt và chuyển vào kho lưu trữ số hóa vĩnh viễn.'}
                      </Typography>
                    </TimelineContent>
                  </TimelineItem>
                </Timeline>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      </Grid>

      {/* Right Column (Operations & QR Code & Attachments) */}
      <Grid size={{ xs: 12, md: 4 }}>
        <Grid container spacing={6}>
          {/* Action Card */}
          <Grid size={{ xs: 12 }}>
            <Card>
              <CardHeader title={t.documents.operationsTitle} />
              <Divider />
              <CardContent className='flex flex-col gap-3'>
                <Button
                  variant='contained'
                  color='success'
                  fullWidth
                  startIcon={<i className='tabler-check' />}
                  onClick={() => handleStatusChange('completed', t.documents.approveAndArchive)}
                >
                  {t.documents.approveAndArchive}
                </Button>
                <Button
                  variant='tonal'
                  color='info'
                  fullWidth
                  startIcon={<i className='tabler-send' />}
                  onClick={() => handleStatusChange('processing', t.documents.forwardDepartment)}
                >
                  {t.documents.forwardDepartment}
                </Button>
                <Button
                  variant='tonal'
                  color='error'
                  fullWidth
                  startIcon={<i className='tabler-x' />}
                  onClick={() => handleStatusChange('rejected', t.documents.rejectDoc)}
                >
                  {t.documents.rejectDoc}
                </Button>
              </CardContent>
            </Card>
          </Grid>

          {/* QR Code Quick Access & Mobile Sharing Component */}
          <Grid size={{ xs: 12 }}>
            <DocumentQRCode
              docNo={doc.documentNumber || id}
              pdfUrl={pdfUrl}
            />
          </Grid>

          {/* Attached Files Card */}
          <Grid size={{ xs: 12 }}>
            <Card>
              <CardHeader title={t.documents.attachedFiles} />
              <Divider />
              <CardContent className='flex flex-col gap-3'>
                <div className='flex items-center justify-between p-3 rounded border border-divider'>
                  <div className='flex items-center gap-3'>
                    <i className='tabler-file-type-pdf text-2xl text-error' />
                    <div>
                      <Typography variant='body2' className='font-medium'>
                        {fileName}
                      </Typography>
                      <Typography variant='caption' color='text.secondary'>177 KB • PDF Scan</Typography>
                    </div>
                  </div>
                  <Button
                    size='small'
                    variant='outlined'
                    startIcon={<i className='tabler-download' />}
                    href={pdfUrl}
                    download={fileName}
                  >
                    {t.documents.download}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      </Grid>
    </Grid>
  )
}

export default DocumentDetail
