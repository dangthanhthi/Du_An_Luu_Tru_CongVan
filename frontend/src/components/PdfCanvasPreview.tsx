'use client'

import { useEffect, useRef, useState } from 'react'
import { Alert, Button, CircularProgress, Stack, Typography } from '@mui/material'
import type { PDFDocumentProxy, PDFDocumentLoadingTask, RenderTask } from 'pdfjs-dist'
import { useAppDictionary } from '@/hooks/useDictionary'
import { loadPdfDocument } from './pdf/loadPdfDocument'

export default function PdfCanvasPreview({ url }: { url: string }) {
  const { isEn } = useAppDictionary()
  const canvas = useRef<HTMLCanvasElement>(null)
  const [loaded, setLoaded] = useState<{ url: string; pdf: PDFDocumentProxy }>()
  const [page, setPage] = useState(1)
  const [rendered, setRendered] = useState('')
  const [failure, setFailure] = useState<{ url: string; page?: number; password: boolean }>()
  const pdf = loaded?.url === url ? loaded.pdf : undefined
  const key = `${url}:${page}`
  const error = failure?.url === url && (failure.page === undefined || failure.page === page) ? failure : undefined

  useEffect(() => {
    let alive = true
    let task: PDFDocumentLoadingTask | undefined

    setFailure(undefined); setPage(1); setRendered('')
    loadPdfDocument(url).then(value => {
      task = value
      const ready = value.promise.then(document => {
        if (alive) setLoaded({ url, pdf: document })
      })

      if (!alive) void value.destroy().catch(() => {})
      return ready
    }).catch(error => {
      if (alive) setFailure({ url, password: error?.name === 'PasswordException' })
    })
    return () => { alive = false; void task?.destroy().catch(() => {}) }
  }, [url])

  useEffect(() => {
    if (!pdf || !canvas.current) return
    let alive = true
    let task: RenderTask | undefined
    const target = canvas.current

    setRendered('')
    pdf.getPage(page).then(value => {
      if (!alive) return
      const base = value.getViewport({ scale: 1 })
      // Bound canvas allocation even for unusually large page dimensions.
      const viewport = value.getViewport({ scale: Math.min(1.25, 1600 / base.width, 2200 / base.height) })
      const context = target.getContext('2d')

      if (!context) throw new Error('Canvas unavailable')
      target.width = Math.ceil(viewport.width)
      target.height = Math.ceil(viewport.height)
      task = value.render({ canvas: target, canvasContext: context, viewport })
      return task.promise.then(() => { if (alive) { setFailure(undefined); setRendered(key) } })
    }).catch(error => {
      if (alive && error?.name !== 'RenderingCancelledException') setFailure({ url, page, password: false })
    })
    return () => { alive = false; task?.cancel() }
  }, [pdf, page, key, url])

  return <Stack spacing={2} sx={{ padding: 2, alignItems: 'center', minHeight: 240 }}>
    {error && <Alert severity='warning'>{error.password
      ? (isEn ? 'This PDF requires a password. Download it and open it with your PDF reader.' : 'PDF yêu cầu mật khẩu. Hãy tải xuống và mở bằng trình đọc PDF của bạn.')
      : (isEn ? 'Unable to preview this PDF. You can still download the original file.' : 'Không thể xem trước PDF này. Bạn vẫn có thể tải tệp gốc.')}</Alert>}
    {!error && rendered !== key && <CircularProgress aria-label={isEn ? 'Loading PDF preview' : 'Đang tải bản xem trước PDF'} />}
    <canvas ref={canvas} role='img' aria-label={isEn ? `PDF page ${page}` : `Trang PDF ${page}`}
      style={{ display: rendered === key && !error ? 'block' : 'none', maxWidth: '100%', height: 'auto', background: '#fff' }} />
    {pdf && <Stack direction='row' alignItems='center' spacing={2}>
      <Button disabled={page <= 1} onClick={() => setPage(value => Math.max(1, value - 1))}>{isEn ? 'Previous page' : 'Trang trước'}</Button>
      <Typography aria-live='polite'>{isEn ? 'Page' : 'Trang'} {page}/{pdf.numPages}</Typography>
      <Button disabled={page >= pdf.numPages} onClick={() => setPage(value => Math.min(pdf.numPages, value + 1))}>{isEn ? 'Next page' : 'Trang sau'}</Button>
    </Stack>}
  </Stack>
}
