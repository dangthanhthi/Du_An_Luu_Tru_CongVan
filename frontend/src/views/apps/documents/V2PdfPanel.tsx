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
import { preloadPdfEngine } from '@/components/pdf/loadPdfDocument'
import PrintedPdfPanel from './PrintedPdfPanel'

const pollDelay = 2000
const maxPolls = 60
function waitForScan(signal: AbortSignal) {
  return new Promise<void>((resolve, reject) => {
    signal.throwIfAborted()
    const cancel = () => { clearTimeout(timer); signal.removeEventListener('abort', cancel); reject(signal.reason) }
    const timer = setTimeout(() => { signal.removeEventListener('abort', cancel); resolve() }, pollDelay)
    signal.addEventListener('abort', cancel, { once: true })
  })
}
const awaitingActivation = (error: unknown) => !!error && typeof error === 'object' &&
  'status' in error && error.status === 423 && 'code' in error && error.code === 'PDF_UNAVAILABLE'

function pdfFailureMessage(error: unknown, isEn: boolean, fallback: string) {
  if (error && typeof error === 'object' && 'code' in error && error.code === 'INVALID_PDF') {
    return isEn
      ? 'PDF is invalid, encrypted or exceeds the supported page limit. Please select another PDF.'
      : 'Tệp PDF không hợp lệ, được mã hóa hoặc vượt giới hạn số trang hỗ trợ. Vui lòng chọn tệp PDF khác.'
  }
  return error instanceof Error ? error.message : fallback
}

export default function V2PdfPanel({ document, onChange }: { document: DocumentDetailV2; onChange: () => void }) {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const h = document.header, key = `${h.id}:${h.version}`
  const [result, setResult] = useState<{ key: string; url?: string; fileId?: string; name?: string; complete?: boolean; error?: string }>({ key: '' })
  const [busyOwner, setBusyOwner] = useState<AbortController | null>(null)
  const [noticeState, setNoticeState] = useState<{ owner: AbortController | null; text: string }>({ owner: null, text: '' })
  const inFlightRef = useRef(false)
  const lifecycle = useRef({ key, controller: new AbortController() })
  const keyRef = useRef(key)
  keyRef.current = key
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange
  const fileInput = useRef<HTMLInputElement>(null)
  const pending = useRef<{ key: string; fileId: string; operationId: string; version: number; attempted: boolean } | null>(null)
  const current = result.key === key ? result : undefined
  if (lifecycle.current.key !== key) {
    lifecycle.current = { key, controller: new AbortController() }
    pending.current = null
    inFlightRef.current = false
  }
  const owner = lifecycle.current.controller
  const busy = busyOwner === owner, notice = noticeState.owner === owner ? noticeState.text : ''
  const setBusy = (value: boolean) => setBusyOwner(value ? owner : null)
  const setNotice = (text: string) => setNoticeState({ owner, text })

  useEffect(() => {
    // Keep an upload started before the first passive effect under its original
    // owner; mount must not unlock its synchronous duplicate-request guard.
    if (lifecycle.current.controller.signal.aborted) {
      lifecycle.current.controller = new AbortController()
      pending.current = null; inFlightRef.current = false; setBusy(false); setNotice('')
    }
    const controller = lifecycle.current.controller
    return () => controller.abort()
  }, [key])

  useEffect(() => {
    const controller = new AbortController()
    let url: string | undefined

    async function load() {
      if (!['Ready', 'Pending'].includes(document.pdfState)) return
      void preloadPdfEngine().catch(() => {})
      try {
        let info
        for (let attempt = 0; ; attempt++) {
          sessionIntent.assertCurrent()
          controller.signal.throwIfAborted()
          try { info = await documentPdfApi.info(h.id, controller.signal); break }
          catch (error) {
            if (!awaitingActivation(error) || attempt >= maxPolls) throw error
            await waitForScan(controller.signal)
          }
        }
        sessionIntent.assertCurrent()
        if (controller.signal.aborted || keyRef.current !== key) return
        // Refresh the aggregate once activation has finished. The fresh Ready
        // DTO carries the version needed for subsequent document mutations.
        if (document.pdfState === 'Pending') { onChangeRef.current(); return }
        const blob = await documentPdfApi.bytes(info.fileId, controller.signal)
        sessionIntent.assertCurrent()
        if (!controller.signal.aborted) {
          url = URL.createObjectURL(blob)
          setResult({ key, url, fileId: info.fileId, name: info.originalName, complete: info.completion.isComplete })
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setResult({ key, error: pdfFailureMessage(error, isEn, isEn ? 'Unable to load PDF.' : 'Không thể tải PDF.') })
        }
      }
    }
    load()
    return () => { controller.abort(); if (url) URL.revokeObjectURL(url) }
  }, [key, h.id, document.pdfState, isEn])

  async function replace(file?: File) {
    if (lifecycle.current.controller !== owner || owner.signal.aborted || keyRef.current !== key) return
    if (inFlightRef.current || busy) return
    inFlightRef.current = true
    setBusy(true)
    setNotice('')
    const controller = owner
    const owned = () => {
      controller.signal.throwIfAborted()
      if (keyRef.current !== key || lifecycle.current.controller !== controller) throw new Error('Document changed')
      sessionIntent.assertCurrent()
    }
    try {
      owned()
      if (file) {
        const uploaded = await documentPdfApi.upload(file, sessionIntent)

        owned()
        pending.current = { key, fileId: uploaded.id, operationId: crypto.randomUUID(), version: h.version, attempted: false }
      }
      const operation = pending.current

      if (!operation || operation.key !== key) throw new Error(isEn ? 'Please select PDF after reloading document.' : 'Hãy chọn PDF sau khi tải lại công văn.')
      if (!operation.attempted) {
        let info = await documentPdfApi.uploadInfo(operation.fileId, controller.signal)
        owned()

        for (let attempt = 0; info.state === 'PendingScan' && attempt < maxPolls; attempt++) {
          setNotice(isEn
            ? 'PDF file is scanning for security verification. It will appear automatically when ready.'
            : 'Đang kiểm tra an toàn PDF. Tệp sẽ tự hiển thị khi xử lý xong.')
          await waitForScan(controller.signal)
          owned()
          info = await documentPdfApi.uploadInfo(operation.fileId, controller.signal)
          owned()
        }

        if (info.state !== 'Available' || !info.canAttach) {
          if (info.state === 'PendingScan') {
            setNotice(isEn
              ? 'PDF processing is taking longer than expected. Retry with the same request to continue checking.'
              : 'Xử lý PDF lâu hơn dự kiến. Bấm thử lại cùng yêu cầu để tiếp tục kiểm tra.')
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
      owned()
      setNotice(isEn ? 'Attaching PDF…' : 'Đang gán PDF…')
      await documentPdfApi.replace(h.id, operation.operationId, operation.fileId, operation.version, sessionIntent)
      owned()
      pending.current = null
      setNotice('')
      onChangeRef.current()
    } catch (error) {
      if (!controller.signal.aborted && keyRef.current === key && lifecycle.current.controller === controller) {
        // Rejected/failed owned uploads disappear from the Files info endpoint.
        // Do not discard an attempted PUT: its outcome may still be ambiguous.
        const operation = pending.current
        if (error && typeof error === 'object' && 'status' in error && error.status === 404 && operation?.key === key && !operation.attempted) {
          pending.current = null
          setNotice(isEn ? 'Uploaded PDF is no longer available. Please select another PDF.' : 'Tệp PDF đã tải không còn khả dụng. Vui lòng chọn PDF khác.')
        } else setNotice(pdfFailureMessage(error, isEn, isEn ? 'Could not confirm PDF replacement. Retry with same request or reload to reconcile.' : 'Chưa xác nhận được thay PDF. Thử lại cùng yêu cầu hoặc tải lại để đối chiếu.'))
      }
    } finally {
      if (!controller.signal.aborted && keyRef.current === key && lifecycle.current.controller === controller) { inFlightRef.current = false; setBusy(false) }
    }
  }

  const isReadyLoading = document.pdfState === 'Ready' && !current
  const activating = document.pdfState === 'Pending' && !current?.error

  return (
    <Card aria-busy={isReadyLoading || busy || activating}>
      <CardHeader title={isEn ? 'Current PDF Attachment' : 'PDF hiện hành'} />
      <CardContent>
        {document.pdfState !== 'Ready' && !busy && (
          <Alert severity='info' className='mbe-4'>
            {document.pdfState === 'None'
              ? (isEn ? 'No PDF attached.' : 'Chưa có PDF.')
              : document.pdfState === 'Pending'
                ? (isEn ? 'Preparing PDF preview. It will appear automatically.' : 'Đang chuẩn bị bản xem PDF. Tệp sẽ tự hiển thị khi sẵn sàng.')
                : (isEn ? 'PDF unavailable. Please verify or replace file.' : 'PDF không khả dụng. Hãy kiểm tra hoặc thay tệp.')}
          </Alert>
        )}
        {(isReadyLoading || activating) && (
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
        {notice && <Alert role='status' severity={busy ? 'info' : 'warning'} className='mbe-4' onClose={busy ? undefined : () => setNotice('')}>{notice}</Alert>}
        {current?.url && (
          <>
            <Alert severity={current.complete ? 'success' : 'info'} className='mbe-4'>
              {current.complete
                ? (isEn ? 'Complete document dossier.' : 'Đủ hồ sơ.')
                : (isEn ? 'Incomplete document dossier.' : 'Chưa đủ hồ sơ.')}
            </Alert>
            <DocumentPDFPreview pdfUrl={current.url} fileName={current.name} docNumber={h.registrationNumber} />
            {current.fileId && <div className='mbs-4'><PrintedPdfPanel key={key} fileId={current.fileId} disabled={busy || activating} /></div>}
          </>
        )}
        {h.allowedActions.includes('ReplacePdf') && (
          <div className='flex flex-wrap items-center gap-3 mbs-4'>
            <Button type='button' variant='outlined' onClick={() => fileInput.current?.click()} disabled={busy || activating || pending.current?.key === key}>
              {isEn ? 'Select PDF' : 'Chọn PDF'}
            </Button>
              <input
                ref={fileInput}
                type='file'
                accept='application/pdf,.pdf'
                hidden
                onChange={e => {
                  const file = e.target.files?.[0]
                  e.target.value = ''
                  if (file) replace(file)
                }}
              />
            {pending.current?.key === key && (
              <Button disabled={busy} onClick={() => replace()}>
                {isEn ? 'Retry with same request' : 'Thử lại cùng yêu cầu'}
              </Button>
            )}
            <Button disabled={busy || activating} onClick={onChange}>
              {isEn ? 'Verify again' : 'Kiểm tra lại'}
            </Button>
            {busy && <CircularProgress size={20} className='mis-2' />}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
