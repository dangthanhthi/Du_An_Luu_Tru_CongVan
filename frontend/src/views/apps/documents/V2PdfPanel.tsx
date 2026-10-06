'use client'
import { useEffect, useRef, useState } from 'react'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'
import { documentPdfApi } from '@/services/das/document-pdf'
import type { DocumentDetailV2 } from '@/types/das/document-v2'
import DocumentPDFPreview from '@/components/DocumentPDFPreview'

export default function V2PdfPanel({ document, onChange }: { document: DocumentDetailV2; onChange: () => void }) {
  const h = document.header, key = `${h.id}:${h.version}`
  const [result, setResult] = useState<{ key: string; url?: string; name?: string; complete?: boolean; error?: string }>({ key: '' })
  const [busy, setBusy] = useState(false), [notice, setNotice] = useState('')
  const pending = useRef<{ key: string; fileId: string; operationId: string; version: number; attempted: boolean } | null>(null)
  const current = result.key === key ? result : undefined

  useEffect(() => {
    const controller = new AbortController()
    let url: string | undefined

    async function load() {
      if (document.pdfState !== 'Ready') return
      try {
        const info = await documentPdfApi.info(h.id, controller.signal)
        const blob = await documentPdfApi.bytes(info.fileId, controller.signal)

        if (!controller.signal.aborted) { url = URL.createObjectURL(blob); setResult({ key, url, name: info.originalName, complete: info.completion.isComplete }) }
      } catch (error) {
        if (!controller.signal.aborted) setResult({ key, error: error instanceof Error ? error.message : 'Không thể tải PDF.' })
      }
    }
    load()
    return () => { controller.abort(); if (url) URL.revokeObjectURL(url) }
  }, [key, h.id, document.pdfState])

  async function replace(file?: File) {
    if (busy) return
    setBusy(true); setNotice('')
    try {
      if (file) {
        const uploaded = await documentPdfApi.upload(file)

        pending.current = { key, fileId: uploaded.id, operationId: crypto.randomUUID(), version: h.version, attempted: false }
      }
      const operation = pending.current

      if (!operation || operation.key !== key) throw new Error('Hãy chọn PDF sau khi tải lại công văn.')
      if (!operation.attempted) {
        const info = await documentPdfApi.uploadInfo(operation.fileId)

        if (info.state !== 'Available' || !info.canAttach) { setNotice('PDF chưa sẵn sàng. Công văn chưa đổi tệp; kiểm tra lại tệp đã tải sau khi dịch vụ quét xác nhận.'); return }
        operation.attempted = true
      }
      await documentPdfApi.replace(h.id, operation.operationId, operation.fileId, operation.version)
      pending.current = null
      onChange()
    } catch (error) { setNotice(error instanceof Error ? error.message : 'Chưa xác nhận được thay PDF. Thử lại cùng yêu cầu hoặc tải lại để đối chiếu.') }
    finally { setBusy(false) }
  }

  return <Card><CardHeader title='PDF hiện hành' /><CardContent>
    {document.pdfState !== 'Ready' && <Alert severity='info' className='mbe-4'>{document.pdfState === 'None' ? 'Chưa có PDF.' : document.pdfState === 'Pending' ? 'PDF đang chờ xác nhận.' : 'PDF không khả dụng. Hãy kiểm tra hoặc thay tệp.'}</Alert>}
    {current?.error && <Alert severity='error' className='mbe-4'>{current.error}</Alert>}
    {notice && <Alert severity='warning' className='mbe-4'>{notice}</Alert>}
    {current?.url && <><Alert severity={current.complete ? 'success' : 'info'} className='mbe-4'>{current.complete ? 'Đủ hồ sơ.' : 'Chưa đủ hồ sơ.'}</Alert><DocumentPDFPreview pdfUrl={current.url} fileName={current.name} docNumber={h.registrationNumber} /></>}
    {h.allowedActions.includes('ReplacePdf') && <div className='flex flex-wrap gap-3 mbs-4'>
      <Button variant='outlined' component='label' disabled={busy || pending.current?.key === key}>Chọn PDF<input type='file' accept='application/pdf,.pdf' hidden onChange={e => { const file = e.target.files?.[0]; e.target.value = ''; if (file) replace(file) }} /></Button>
      {pending.current?.key === key && <Button disabled={busy} onClick={() => replace()}>Thử lại cùng yêu cầu</Button>}
      <Button disabled={busy} onClick={onChange}>Kiểm tra lại</Button>
    </div>}
  </CardContent></Card>
}
