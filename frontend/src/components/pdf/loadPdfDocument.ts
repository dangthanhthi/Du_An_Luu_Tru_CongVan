import type { PDFDocumentLoadingTask } from 'pdfjs-dist'

export async function loadPdfDocument(url: string): Promise<PDFDocumentLoadingTask> {
  // The minified distribution avoids PDF.js's internal webpack export names
  // colliding with Next's webpack development module wrapper.
  const pdfjs = await import('pdfjs-dist/build/pdf.min.mjs')

  pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdfjs-dist/build/pdf.worker.min.mjs', import.meta.url).toString()
  return pdfjs.getDocument({ url, isEvalSupported: false, enableXfa: false, useSystemFonts: true })
}
