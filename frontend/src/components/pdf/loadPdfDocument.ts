import type { PDFDocumentLoadingTask } from 'pdfjs-dist'
import { createPdfDocumentPool } from './documentPool'

let engine: Promise<typeof import('pdfjs-dist/build/pdf.min.mjs')> | undefined
export function preloadPdfEngine() {
  if (!engine) {
    engine = import('pdfjs-dist/build/pdf.min.mjs').then(pdfjs => {
      pdfjs.GlobalWorkerOptions.workerSrc = new URL('pdfjs-dist/build/pdf.worker.min.mjs', import.meta.url).toString()
      // Public library only: overlap its worker download with authorized PDF IO.
      if (typeof document !== 'undefined' && !document.querySelector('link[data-das-pdf-worker]')) {
        const link = document.createElement('link')
        link.rel = 'modulepreload'; link.href = pdfjs.GlobalWorkerOptions.workerSrc
        link.setAttribute('data-das-pdf-worker', '')
        document.head.appendChild(link)
      }
      return pdfjs
    }).catch(error => { engine = undefined; throw error })
  }
  return engine
}

export async function loadPdfDocument(url: string): Promise<PDFDocumentLoadingTask> {
  // The minified distribution avoids PDF.js's internal webpack export names
  // colliding with Next's webpack development module wrapper.
  const pdfjs = await preloadPdfEngine()
  return pdfjs.getDocument({ url, isEvalSupported: false, enableXfa: false, useSystemFonts: true })
}

const documents = createPdfDocumentPool(loadPdfDocument)
export const acquirePdfDocument = (url: string) => documents.acquire(url)
