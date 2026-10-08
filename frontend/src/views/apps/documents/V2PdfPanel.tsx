'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useEffect, useRef, useState } from 'react'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import { documentPdfApi } from '@/services/das/document-pdf'
import type { DocumentDetailV2 } from '@/types/das/document-v2'
import DocumentPDFPreview from '@/components/DocumentPDFPreview'

export default function V2PdfPanel({ document, onChange }: { document: DocumentDetailV2; onChange: () => void }) {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const h = document.header, key = `${h.id}:${h.version}`
  const [result, setResult] = useState<{ key: string; url?: string; name?: string; complete?: boolean; error?: string }>({ key: '' })
  const [busy, setBusy] = useState(false), [notice, setNotice] = useState('')
  const inFlightRef = useRef(false)
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

        if (!controller.signal.aborted) {
          url = URL.createObjectURL(blob)
          setResult({ key, url, name: info.originalName, complete: info.completion.isComplete })
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setResult({ key, error: error instanceof Error ? error.message : (isEn ? 'Unable to load PDF.' : 'Không thể tải PDF.') })
        }
      }
    }
    load()
    return () => { controller.abort(); if (url) URL.revokeObjectURL(url) }
  }, [key, h.id, document.pdfState, isEn])

  async function replace(file?: File) {
    if (inFlightRef.current || busy) return
    inFlightRef.current = true
    setBusy(true)
    setNotice('')
    try {
      sessionIntent.assertCurrent()
      if (file) {
        const uploaded = await documentPdfApi.upload(file, sessionIntent)

        sessionIntent.assertCurrent()
        pending.current = { key, fileId: uploaded.id, operationId: crypto.randomUUID(), version: h.version, attempted: false }
      }
      const operation = pending.current

      if (!operation || operation.key !== key) throw new Error(isEn ? 'Please select PDF after reloading document.' : 'Hãy chọn PDF sau khi tải lại công văn.')
      if (!operation.attempted) {
        const info = await documentPdfApi.uploadInfo(operation.fileId)

        if (info.state !== 'Available' || !info.canAttach) {
          if (info.state === 'PendingScan') {
            setNotice(isEn
              ? 'PDF file is scanning for security verification. Please wait a moment and verify again.'
              : 'Tệp PDF đang được quét kiểm tra an toàn. Vui lòng chờ giây lát rồi kiểm tra lại.')
          } else if (info.state === 'Rejected') {
            setNotice(isEn
              ? 'Uploaded PDF was rejected by security scan. You may select a different clean PDF file.'
              : 'Tệp PDF bị từ chối do không đạt kiểm tra an toàn. Bạn có thể chọn tệp PDF khác.')
            pending.current = null
          } else if (info.state === 'Missing' || info.state === 'Failed') {
            setNotice(isEn
              ? `PDF processing failed (${info.state}). You may select a different PDF file.`
              : `Xử lý PDF thất bại (${info.state}). Bạn có thể chọn tệp PDF khác.`)
            pending.current = null
          } else {
            setNotice(isEn
              ? 'PDF is not ready or cannot be attached. Verify uploaded file once scan service confirms.'
              : 'PDF chưa sẵn sàng hoặc không thể đính kèm. Kiểm tra lại tệp đã tải sau khi dịch vụ quét xác nhận.')
          }
          return
        }
        operation.attempted = true
      }
      await documentPdfApi.replace(h.id, operation.operationId, operation.fileId, operation.version, sessionIntent)
      pending.current = null
      onChange()
    } catch (error) {
      setNotice(error instanceof Error ? error.message : (isEn ? 'Could not confirm PDF replacement. Retry with same request or reload to reconcile.' : 'Chưa xác nhận được thay PDF. Thử lại cùng yêu cầu hoặc tải lại để đối chiếu.'))
    } finally {
      inFlightRef.current = false
      setBusy(false)
    }
  }

  const isReadyLoading = document.pdfState === 'Ready' && !current

  return (
    <Card aria-busy={isReadyLoading || busy}>
      <CardHeader title={isEn ? 'Current PDF Attachment' : 'PDF hiện hành'} />
      <CardContent>
        {document.pdfState !== 'Ready' && (
          <Alert severity='info' className='mbe-4'>
            {document.pdfState === 'None'
              ? (isEn ? 'No PDF attached.' : 'Chưa có PDF.')
              : document.pdfState === 'Pending'
                ? (isEn ? 'PDF pending confirmation.' : 'PDF đang chờ xác nhận.')
                : (isEn ? 'PDF unavailable. Please verify or replace file.' : 'PDF không khả dụng. Hãy kiểm tra hoặc thay tệp.')}
          </Alert>
        )}
        {isReadyLoading && (
          <div className='flex items-center gap-3 p-4' role='status'>
            <CircularProgress size={24} aria-label={isEn ? 'Loading PDF content' : 'Đang tải nội dung PDF'} />
            <span>{isEn ? 'Loading PDF document...' : 'Đang tải nội dung PDF...'}</span>
          </div>
        )}
        {current?.error && (
          <Alert
            severity='error'
            className='mbe-4'
            action={
              <Button color='inherit' size='small' onClick={onChange}>
                {isEn ? 'Retry' : 'Thử lại'}
              </Button>
            }
          >
            {current.error}
          </Alert>
        )}
        {notice && <Alert severity='warning' className='mbe-4' onClose={() => setNotice('')}>{notice}</Alert>}
        {current?.url && (
          <>
            <Alert severity={current.complete ? 'success' : 'info'} className='mbe-4'>
              {current.complete
                ? (isEn ? 'Complete document dossier.' : 'Đủ hồ sơ.')
                : (isEn ? 'Incomplete document dossier.' : 'Chưa đủ hồ sơ.')}
            </Alert>
            <DocumentPDFPreview pdfUrl={current.url} fileName={current.name} docNumber={h.registrationNumber} />
          </>
        )}
        {h.allowedActions.includes('ReplacePdf') && (
          <div className='flex flex-wrap items-center gap-3 mbs-4'>
            <Button variant='outlined' component='label' disabled={busy || pending.current?.key === key}>
              {isEn ? 'Select PDF' : 'Chọn PDF'}
              <input
                type='file'
                accept='application/pdf,.pdf'
                hidden
                onChange={e => {
                  const file = e.target.files?.[0]
                  e.target.value = ''
                  if (file) replace(file)
                }}
              />
            </Button>
            {pending.current?.key === key && (
              <Button disabled={busy} onClick={() => replace()}>
                {isEn ? 'Retry with same request' : 'Thử lại cùng yêu cầu'}
              </Button>
            )}
            <Button disabled={busy} onClick={onChange}>
              {isEn ? 'Verify again' : 'Kiểm tra lại'}
            </Button>
            {busy && <CircularProgress size={20} className='mis-2' />}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
