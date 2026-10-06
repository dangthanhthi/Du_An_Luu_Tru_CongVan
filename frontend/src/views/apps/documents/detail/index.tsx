'use client'
import { useEffect, useState } from 'react'
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
import { documentsV2Api } from '@/services/das/documents'
import { ApiRequestError } from '@/services/api'
import type { DocumentDetailV2 } from '@/types/das/document-v2'
import V2PdfPanel from '../V2PdfPanel'
import DocumentTaskPanel from '../DocumentTaskPanel'

export default function DocumentDetail({ id }: { id: string }) {
  const params = useParams(), lang = typeof params.lang === 'string' ? params.lang : 'vi'
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false)
  const key = `${id}:${revision}`
  const [result, setResult] = useState<{ key: string; document?: DocumentDetailV2; error?: string }>({ key: '' })
  const [message, setMessage] = useState(''), [cancelOpen, setCancelOpen] = useState(false), [reason, setReason] = useState('')
  const current = result.key === key ? result : null, doc = current?.document

  useEffect(() => {
    const controller = new AbortController()

    setMessage(''); setCancelOpen(false); setReason('')
    documentsV2Api.detail(id, controller.signal).then(document => {
      if (!controller.signal.aborted) setResult({ key, document })
    }).catch(error => {
      if (!controller.signal.aborted) setResult({ key, error: error instanceof Error ? error.message : 'Không thể tải công văn.' })
    })
    return () => controller.abort()
  }, [id, key])
  const refresh = () => { window.dispatchEvent(new Event('das_documents_updated')); setRevision(x => x + 1) }
  async function status(action: string) {
    if (!doc || busy) return
    setBusy(true); setMessage('')
    try { await documentsV2Api.status(id, doc.header.version, action, action === 'Cancel' ? reason : undefined); refresh() }
    catch (error) { setMessage(error instanceof ApiRequestError && error.status === 409 ? 'Công văn đã thay đổi. Tải lại trước khi thao tác tiếp.' : 'Chưa xác nhận được thay đổi trạng thái. Tải lại để đối chiếu dữ liệu trước khi thử lại.') }
    finally { setBusy(false) }
  }
  if (!current) return <div className='flex justify-center p-8'><CircularProgress aria-label='Đang tải công văn' /></div>
  if (!doc) return <Card><CardContent><Alert severity='error'>{current.error}</Alert><Button onClick={() => setRevision(x => x + 1)}>Thử lại</Button></CardContent></Card>
  const h = doc.header
  const statusLabel = { InProgress: 'Đang thực hiện', Distributed: 'Đã phân phối', Cancelled: 'Đã hủy' }[h.status]
  const fields = [
    ['Số công văn', h.registrationNumber], ['Ngày đăng ký', h.registrationDate], ['Ngày phát hành', h.issuedDate],
    ['Công ty', h.companyCode], ['Phòng', h.departmentName], ['Độ mật', h.sensitivity],
    ['Số hiệu đối tác', h.referenceNumber], ['Đơn vị gửi', h.senderPartnerName], ['Ghi chú', doc.remark]
  ]

  return <Grid container spacing={4}>
    <Grid size={{ xs: 12 }}><Card><CardHeader title={h.subject} action={<Chip label={statusLabel} color={h.status === 'Distributed' ? 'info' : h.status === 'Cancelled' ? 'secondary' : 'warning'} />} />
      <CardContent>
        {message && <Alert severity='error' className='mbe-4'>{message}</Alert>}
        <Grid container spacing={4}>{fields.map(([label, value]) => <Grid key={label} size={{ xs: 12, md: 6 }}><Typography variant='subtitle2'>{label}</Typography><Typography sx={{ overflowWrap: 'anywhere', whiteSpace: 'pre-wrap' }}>{value || '—'}</Typography></Grid>)}</Grid>
        {doc.recipients.length > 0 && <div className='mbs-4'><Typography variant='subtitle2'>Nơi nhận</Typography>{doc.recipients.map(r => <Chip key={`${r.referenceType}:${r.referenceId}`} label={r.name} className='mie-2 mbs-2' />)}</div>}
        {doc.relatedDocumentIds.length > 0 && <div className='flex flex-wrap gap-3 mbs-4'>{doc.relatedDocumentIds.map((relatedId, index) => <Button key={relatedId} component={Link} href={`/${lang}/apps/documents/${relatedId}`}>Công văn liên quan {index + 1}</Button>)}</div>}
        <div className='flex flex-wrap gap-3 mbs-5'>
          {h.allowedActions.includes('Edit') && <Button component={Link} variant='outlined' href={`/${lang}/apps/documents/edit/${id}`}>Sửa công văn</Button>}
          {h.allowedActions.includes('Distribute') && <Button disabled={busy || !!message} variant='contained' onClick={() => status('Distribute')}>Đã phân phối</Button>}
          {h.allowedActions.includes('Cancel') && <Button disabled={busy || !!message} color='error' onClick={() => setCancelOpen(true)}>Hủy công văn</Button>}
          {h.allowedActions.includes('Restore') && <Button disabled={busy || !!message} onClick={() => status('Restore')}>Khôi phục</Button>}
          <Button disabled={busy} onClick={refresh}>Tải lại dữ liệu</Button>
          <Button component={Link} href={`/${lang}/apps/documents/list?kind=${h.kind}&view=all`}>Danh sách công văn</Button>
        </div>
      </CardContent></Card></Grid>
    <Grid size={{ xs: 12 }}><V2PdfPanel document={doc} onChange={refresh} /></Grid>
    <Grid size={{ xs: 12 }}><DocumentTaskPanel key={id} documentId={id} /></Grid>
    <Dialog open={cancelOpen} onClose={() => { if (!busy) setCancelOpen(false) }} fullWidth maxWidth='sm'>
      <DialogTitle>Hủy công văn</DialogTitle><DialogContent><TextField autoFocus fullWidth multiline minRows={3} required label='Lý do hủy' value={reason} onChange={e => setReason(e.target.value)} disabled={busy} className='mbs-3' /></DialogContent>
      <DialogActions><Button disabled={busy} onClick={() => setCancelOpen(false)}>Quay lại</Button><Button color='error' disabled={busy || !reason.trim() || reason.length > 4000 || !!message} onClick={() => status('Cancel')}>Xác nhận hủy</Button></DialogActions>
    </Dialog>
  </Grid>
}
