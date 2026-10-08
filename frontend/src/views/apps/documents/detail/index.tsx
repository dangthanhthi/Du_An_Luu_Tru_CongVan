'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useEffect, useState, useRef } from 'react'
import { useParams } from 'next/navigation'
import Link from 'next/link'
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import TextField from '@mui/material/TextField'
import Divider from '@mui/material/Divider'
import { documentsV2Api } from '@/services/das/documents'
import { ApiRequestError } from '@/services/api'
import type { DocumentDetailV2 } from '@/types/das/document-v2'
import V2PdfPanel from '../V2PdfPanel'
import DocumentTaskPanel from '../DocumentTaskPanel'

function catalogLabel(code?: string | null, savedName?: string | null) {
  return savedName ? (code ? `${savedName} (${code})` : savedName) : code
}

export default function DocumentDetail({ id }: { id: string }) {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const params = useParams(), lang = typeof params.lang === 'string' ? params.lang : 'vi'
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false)
  const inFlightRef = useRef(false)
  const key = `${id}:${revision}`
  const [result, setResult] = useState<{ key: string; document?: DocumentDetailV2; error?: string }>({ key: '' })
  const [message, setMessage] = useState(''), [cancelOpen, setCancelOpen] = useState(false), [reason, setReason] = useState('')
  const [mustReload, setMustReload] = useState(false)
  const current = result.key === key ? result : null, doc = current?.document

  useEffect(() => {
    const controller = new AbortController()

    setMessage(''); setCancelOpen(false); setReason('')
    documentsV2Api.detail(id, controller.signal).then(document => {
      if (!controller.signal.aborted) {
        setResult({ key, document })
        setMustReload(false)
      }
    }).catch(error => {
      if (!controller.signal.aborted) {
        const status = error instanceof ApiRequestError ? error.status : undefined
        const message = status === 401
          ? (isEn ? 'Your session has expired. Please sign in again.' : 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.')
          : status === 403
            ? (isEn ? 'You do not have permission to view this document.' : 'Bạn không có quyền xem công văn này.')
            : status === 404
              ? (isEn ? 'Document not found or outside your authorized scope.' : 'Không tìm thấy công văn hoặc công văn ngoài phạm vi được cấp.')
              : status === 503
                ? (isEn ? 'Document access is temporarily unavailable. Please try again later.' : 'Dịch vụ truy cập công văn tạm thời chưa sẵn sàng. Vui lòng thử lại sau.')
                : (isEn ? 'Failed to load document. Please try again.' : 'Không thể tải công văn. Vui lòng thử lại.')

        setResult({ key, error: message })
      }
    })
    return () => controller.abort()
  }, [id, key, isEn])

  const refresh = () => { window.dispatchEvent(new Event('das_documents_updated')); setRevision(x => x + 1) }

  async function status(action: string) {
    if (!doc || inFlightRef.current || busy || mustReload) return
    inFlightRef.current = true
    setBusy(true); setMessage('')
    try {
      await documentsV2Api.status(id, doc.header.version, action, action === 'Cancel' ? reason : undefined, sessionIntent)
      setCancelOpen(false)
      setReason('')
      refresh()
    } catch (error) {
      if (error instanceof ApiRequestError) {
        if (error.status === 409) {
          setMustReload(true)
          setMessage(isEn ? 'Document has been modified. Please reload before trying again.' : 'Công văn đã thay đổi. Tải lại trước khi thao tác tiếp.')
        } else if (error.status === 400) {
          const detailMsg = error.message || ''
          if (action === 'Distribute') {
            setMessage(isEn
              ? `Cannot distribute document (${detailMsg || 'Distribution requirements not met: Incoming documents require distribution targets; Outgoing documents require external recipient entities'}). Please edit the document to add recipients.`
              : `Không thể phân phối công văn (${detailMsg || 'Yêu cầu phân phối chưa đủ: Công văn đến cần có nơi nhận nội bộ; Công văn đi cần có đơn vị nhận ngoài'}). Vui lòng sửa công văn để bổ sung nơi nhận.`)
          } else if (action === 'Cancel') {
            setMessage(isEn
              ? `Cancellation request is invalid (${detailMsg || 'Cancellation reason or prerequisites not met'}).`
              : `Yêu cầu hủy không hợp lệ (${detailMsg || 'Lý do hủy hoặc điều kiện hủy chưa đáp ứng'}).`)
          } else if (action === 'Restore') {
            setMessage(isEn
              ? `Restoration request is invalid (${detailMsg || 'Document restoration prerequisites not met'}).`
              : `Yêu cầu khôi phục không hợp lệ (${detailMsg || 'Điều kiện khôi phục công văn chưa đáp ứng'}).`)
          } else {
            setMessage(isEn
              ? `Invalid request (${detailMsg || 'Validation requirements not met'}).`
              : `Yêu cầu không hợp lệ (${detailMsg || 'Dữ liệu không đáp ứng điều kiện nghiệp vụ'}).`)
          }
        } else if (error.status === 403) {
          setMessage(isEn ? 'You do not have permission to perform this action.' : 'Bạn không có quyền thực hiện thao tác này.')
        } else if (error.status === 404) {
          setMessage(isEn ? 'Document not found.' : 'Không tìm thấy công văn.')
        } else {
          setMustReload(true)
          setMessage(isEn ? 'Status change outcome uncertain. Please reload to inspect before retrying.' : 'Chưa xác nhận được thay đổi trạng thái. Tải lại để đối chiếu dữ liệu trước khi thử lại.')
        }
      } else {
        setMustReload(true)
        setMessage(isEn ? 'Status change outcome uncertain. Please reload to inspect before retrying.' : 'Chưa xác nhận được thay đổi trạng thái. Tải lại để đối chiếu dữ liệu trước khi thử lại.')
      }
    } finally {
      inFlightRef.current = false
      setBusy(false)
    }
  }

  if (!current) return <div className='flex justify-center p-8'><CircularProgress aria-label={isEn ? 'Loading document' : 'Đang tải công văn'} /></div>
  if (!doc) return <Card><CardContent><Alert severity='error'>{current.error}</Alert><Button className='mbs-3' onClick={() => setRevision(x => x + 1)}>{isEn ? 'Retry' : 'Thử lại'}</Button></CardContent></Card>

  const h = doc.header
  const dt = doc.details
  const statusLabel = {
    InProgress: isEn ? 'In Progress' : 'Đang thực hiện',
    Distributed: isEn ? 'Distributed' : 'Đã phân phối',
    Cancelled: isEn ? 'Cancelled' : 'Đã hủy'
  }[h.status] || h.status

  const kindLabel = {
    Incoming: isEn ? 'Incoming Document' : 'Công văn đến',
    Outgoing: isEn ? 'Outgoing Document' : 'Công văn đi',
    Internal: isEn ? 'Internal Document' : 'Công văn nội bộ'
  }[h.kind] || h.kind

  // General fields
  const generalFields = [
    [isEn ? 'Registration Number' : 'Số công văn', h.registrationNumber],
    [isEn ? 'Registration Date' : 'Ngày đăng ký', h.registrationDate],
    [isEn ? 'Issued Date' : 'Ngày phát hành', h.issuedDate],
    [isEn ? 'Document Kind' : 'Loại công văn', kindLabel],
    [isEn ? 'Company' : 'Công ty', h.companyCode],
    [isEn ? 'Department' : 'Phòng phụ trách', h.departmentName],
    [isEn ? 'Sensitivity' : 'Độ mật', h.sensitivity]
  ]

  // Specific detail fields based on document kind
  const specificFields: [string, string | null | undefined][] = []
  if (h.kind === 'Incoming') {
    specificFields.push(
      [isEn ? 'Receiving Date' : 'Ngày nhận', dt?.receivingDate],
      [isEn ? 'Sender Partner' : 'Đơn vị gửi', h.senderPartnerName || dt?.senderNameSnapshot],
      [isEn ? 'Partner Ref. No.' : 'Số hiệu đối tác', h.referenceNumber || dt?.referenceNumber],
      [isEn ? 'Intake Method' : 'Phương thức nhận', catalogLabel(dt?.methodCode, dt?.methodNameSnapshot)],
      [isEn ? 'Document Type' : 'Loại văn bản', catalogLabel(dt?.documentTypeCode, dt?.documentTypeNameSnapshot)],
      [isEn ? 'Category' : 'Phân loại', catalogLabel(dt?.categoryCode, dt?.categoryNameSnapshot)],
      [isEn ? 'Other Info' : 'Thông tin khác', dt?.others]
    )
  } else if (h.kind === 'Outgoing') {
    specificFields.push(
      [isEn ? 'Contract Number' : 'Số hợp đồng', dt?.contractNumber],
      [isEn ? 'Other Recipients' : 'Nơi nhận khác', dt?.otherRecipients],
      [isEn ? 'Dispatch Method' : 'Phương thức phát hành', catalogLabel(dt?.methodCode, dt?.methodNameSnapshot)],
      [isEn ? 'Document Type' : 'Loại văn bản', catalogLabel(dt?.documentTypeCode, dt?.documentTypeNameSnapshot)],
      [isEn ? 'Category' : 'Phân loại', catalogLabel(dt?.categoryCode, dt?.categoryNameSnapshot)],
      [isEn ? 'Other Info' : 'Thông tin khác', dt?.others]
    )
  } else if (h.kind === 'Internal') {
    specificFields.push(
      [isEn ? 'Document Type' : 'Loại văn bản', catalogLabel(dt?.documentTypeCode, dt?.documentTypeNameSnapshot)],
      [isEn ? 'Category' : 'Phân loại', catalogLabel(dt?.categoryCode, dt?.categoryNameSnapshot)],
      [isEn ? 'Other Info' : 'Thông tin khác', dt?.others]
    )
  }

  // Audit snapshot IDs (Strictly as per Handoff: do not infer user names from GUIDs; Originator represents the actual sender)
  const auditFields = [
    [isEn ? 'Originator (Actual Sender ID)' : 'ID Người thực sự gửi đi (Originator)', doc.originatorUserId],
    [isEn ? 'Inputter User ID' : 'ID Người nhập', doc.inputterUserId],
    [isEn ? 'Last Modifier User ID' : 'ID Người sửa cuối', doc.lastModifierUserId],
    [isEn ? 'Record Version' : 'Phiên bản DTO', `v${h.version}`]
  ]

  return (
    <Grid container spacing={4}>
      <Grid size={{ xs: 12 }}>
        <Card>
          <CardHeader
            title={h.subject}
            subheader={`${h.registrationNumber} · ${kindLabel}`}
            action={
              <Chip
                label={statusLabel}
                color={h.status === 'Distributed' ? 'info' : h.status === 'Cancelled' ? 'secondary' : 'warning'}
              />
            }
          />
          <CardContent>
            {message && !cancelOpen && <Alert severity='error' className='mbe-4' onClose={() => setMessage('')}>{message}</Alert>}

            {/* Section: General info */}
            <Typography variant='overline' className='font-semibold text-primary block mbe-2'>
              {isEn ? 'General Information' : 'Thông tin chung'}
            </Typography>
            <Grid container spacing={4} className='mbe-4'>
              {generalFields.map(([label, value]) => (
                <Grid key={label} size={{ xs: 12, sm: 6, md: 4 }}>
                  <Typography variant='caption' color='text.secondary' display='block'>{label}</Typography>
                  <Typography variant='body2' fontWeight={500} sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap' }}>
                    {value || '—'}
                  </Typography>
                </Grid>
              ))}
            </Grid>

            {/* Section: Business details */}
            {specificFields.length > 0 && (
              <>
                <Divider className='mbe-4' />
                <Typography variant='overline' className='font-semibold text-primary block mbe-2'>
                  {isEn ? 'Business Specific Details' : 'Chi tiết nghiệp vụ'}
                </Typography>
                <Grid container spacing={4} className='mbe-4'>
                  {specificFields.map(([label, value]) => (
                    <Grid key={label} size={{ xs: 12, sm: 6, md: 4 }}>
                      <Typography variant='caption' color='text.secondary' display='block'>{label}</Typography>
                      <Typography variant='body2' fontWeight={500} sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap' }}>
                        {value || '—'}
                      </Typography>
                    </Grid>
                  ))}
                </Grid>
              </>
            )}

            {/* Section: Recipients */}
            {doc.recipients.length > 0 && (
              <>
                <Divider className='mbe-4' />
                <div className='mbe-4'>
                  <Typography variant='overline' className='font-semibold text-primary block mbe-2'>
                    {isEn ? 'Recipients & Distribution Targets' : 'Nơi nhận và đơn vị phân phối'}
                  </Typography>
                  <div className='flex flex-wrap gap-2'>
                    {doc.recipients.map(r => (
                      <Chip
                        key={`${r.referenceType}:${r.referenceId}`}
                        label={`${r.name} (${r.referenceType === 'DistributionTarget' ? (isEn ? 'Internal' : 'Nội bộ') : (isEn ? 'External' : 'Đối tác')})`}
                        variant='tonal'
                        color={r.referenceType === 'DistributionTarget' ? 'primary' : 'secondary'}
                        size='small'
                      />
                    ))}
                  </div>
                </div>
              </>
            )}

            {/* Section: Related Documents */}
            {doc.relatedDocumentIds.length > 0 && (
              <>
                <Divider className='mbe-4' />
                <div className='mbe-4'>
                  <Typography variant='overline' className='font-semibold text-primary block mbe-2'>
                    {isEn ? 'Related Documents' : 'Công văn liên quan'}
                  </Typography>
                  <div className='flex flex-wrap gap-2'>
                    {doc.relatedDocumentIds.map((relatedId, index) => (
                      <Button
                        key={relatedId}
                        component={Link}
                        size='small'
                        variant='outlined'
                        href={`/${lang}/apps/documents/${relatedId}`}
                      >
                        {isEn ? `Related Document ${index + 1}` : `Công văn liên quan ${index + 1}`}
                      </Button>
                    ))}
                  </div>
                </div>
              </>
            )}

            {/* Section: Remark */}
            <Divider className='mbe-4' />
            <div className='mbe-4'>
              <Typography variant='overline' color='text.secondary' display='block'>
                {isEn ? 'Remark / Notes' : 'Ghi chú'}
              </Typography>
              <Typography variant='body2' sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap' }}>
                {doc.remark || '—'}
              </Typography>
            </div>

            {/* Section: Technical & Audit IDs */}
            <Divider className='mbe-4' />
            <Typography variant='overline' color='text.secondary' display='block' className='mbe-2'>
              {isEn ? 'Audit & DTO Record Tracking' : 'Thông tin truy vết DTO'}
            </Typography>
            <Grid container spacing={4} className='mbe-5'>
              {auditFields.map(([label, value]) => (
                <Grid key={label} size={{ xs: 12, sm: 6, md: 3 }}>
                  <Typography variant='caption' color='text.disabled' display='block'>{label}</Typography>
                  <Typography variant='caption' sx={{ fontFamily: 'monospace', overflowWrap: 'anywhere' }}>
                    {value || '—'}
                  </Typography>
                </Grid>
              ))}
            </Grid>

            {/* Action buttons */}
            <div className='flex flex-wrap gap-3 mbs-5'>
              {h.allowedActions.includes('Edit') && (
                <Button component={Link} variant='outlined' href={`/${lang}/apps/documents/edit/${id}`}>
                  {isEn ? 'Edit Document' : 'Sửa công văn'}
                </Button>
              )}
              {h.allowedActions.includes('Distribute') && (
                <Button disabled={busy || mustReload} variant='contained' onClick={() => status('Distribute')}>
                  {isEn ? 'Distribute' : 'Đã phân phối'}
                </Button>
              )}
              {h.allowedActions.includes('Cancel') && (
                <Button disabled={busy || mustReload} color='error' onClick={() => setCancelOpen(true)}>
                  {isEn ? 'Cancel Document' : 'Hủy công văn'}
                </Button>
              )}
              {h.allowedActions.includes('Restore') && (
                <Button disabled={busy || mustReload} onClick={() => status('Restore')}>
                  {isEn ? 'Restore' : 'Khôi phục'}
                </Button>
              )}
              <Button disabled={busy} onClick={refresh}>
                {isEn ? 'Reload Data' : 'Tải lại dữ liệu'}
              </Button>
              <Button component={Link} href={`/${lang}/apps/documents/list?kind=${h.kind}&view=all`}>
                {isEn ? 'Document List' : 'Danh sách công văn'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12 }}>
        <V2PdfPanel document={doc} onChange={refresh} />
      </Grid>
      <Grid size={{ xs: 12 }}>
        <DocumentTaskPanel key={id} documentId={id} />
      </Grid>

      <Dialog
        open={cancelOpen}
        onClose={() => { if (!busy) setCancelOpen(false) }}
        fullWidth
        maxWidth='sm'
      >
        <DialogTitle>{isEn ? 'Cancel Document' : 'Hủy công văn'}</DialogTitle>
        <DialogContent>
          {message && <Alert severity='error' className='mbe-3' onClose={() => setMessage('')}>{message}</Alert>}
          <TextField
            autoFocus
            fullWidth
            multiline
            minRows={3}
            required
            label={isEn ? 'Cancellation reason' : 'Lý do hủy'}
            value={reason}
            onChange={e => { setReason(e.target.value); if (message) setMessage('') }}
            disabled={busy}
            error={reason.length > 4000}
            helperText={`${reason.length}/4000 ${isEn ? 'characters' : 'ký tự'}${reason.length > 4000 ? (isEn ? ' (Exceeds maximum 4000 characters)' : ' (Vượt quá tối đa 4000 ký tự)') : ''}`}
            className='mbs-3'
          />
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={() => { setCancelOpen(false); setMessage('') }}>
            {isEn ? 'Back' : 'Quay lại'}
          </Button>
          <Button disabled={busy} onClick={refresh}>
            {isEn ? 'Reload Data' : 'Tải lại dữ liệu'}
          </Button>
          <Button
            color='error'
            disabled={busy || mustReload || !reason.trim() || reason.length > 4000}
            onClick={() => status('Cancel')}
          >
            {isEn ? 'Confirm Cancel' : 'Xác nhận hủy'}
          </Button>
        </DialogActions>
      </Dialog>
    </Grid>
  )
}
