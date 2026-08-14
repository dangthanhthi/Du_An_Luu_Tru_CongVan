'use client'

import { useState } from 'react'

// MUI Imports
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import Tooltip from '@mui/material/Tooltip'
import Chip from '@mui/material/Chip'

// Hook Imports
import { useAppDictionary } from '@/hooks/useDictionary'

interface DocumentPDFPreviewProps {
  pdfUrl?: string
  fileName?: string
  docNumber?: string
  summaryText?: string
}

export default function DocumentPDFPreview({
  pdfUrl = '/samples/01_Cong_Van_Den_Bo_GDDT.pdf',
  fileName = 'VanBan_DinhKem.pdf',
  docNumber = 'CV-2026',
  summaryText
}: DocumentPDFPreviewProps) {
  const { isEn } = useAppDictionary()
  const [isFullscreen, setIsFullscreen] = useState(false)
  const [activeView, setActiveView] = useState<'pdf' | 'text'>('pdf')

  const handleDownload = () => {
    const link = document.createElement('a')
    link.href = pdfUrl
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
  }

  return (
    <>
      <Card className='border border-divider shadow-sm'>
        <CardHeader
          title={
            <div className='flex flex-wrap items-center justify-between gap-2'>
              <div className='flex items-center gap-2'>
                <i className='tabler-file-type-pdf text-2xl text-error' />
                <div>
                  <Typography variant='subtitle1' className='font-semibold'>
                    {isEn ? 'PDF Document Preview' : 'Xem Trước Tệp PDF & Bản Scan'}
                  </Typography>
                  <Typography variant='caption' color='text.secondary'>
                    {fileName}
                  </Typography>
                </div>
              </div>

              <div className='flex items-center gap-1.5'>
                <div className='flex items-center rounded border border-divider p-0.5'>
                  <Button
                    size='small'
                    variant={activeView === 'pdf' ? 'contained' : 'text'}
                    color='primary'
                    className='py-0.5 px-2 text-xs'
                    onClick={() => setActiveView('pdf')}
                  >
                    PDF
                  </Button>
                  {summaryText && (
                    <Button
                      size='small'
                      variant={activeView === 'text' ? 'contained' : 'text'}
                      color='primary'
                      className='py-0.5 px-2 text-xs'
                      onClick={() => setActiveView('text')}
                    >
                      OCR Text
                    </Button>
                  )}
                </div>

                <Tooltip title={isEn ? 'Full Screen Preview' : 'Phóng To Toàn Màn Hình'} arrow>
                  <IconButton size='small' onClick={() => setIsFullscreen(true)}>
                    <i className='tabler-arrows-maximize' />
                  </IconButton>
                </Tooltip>

                <Tooltip title={isEn ? 'Download PDF' : 'Tải Về Tệp PDF'} arrow>
                  <IconButton size='small' color='primary' onClick={handleDownload}>
                    <i className='tabler-download' />
                  </IconButton>
                </Tooltip>
              </div>
            </div>
          }
        />

        <CardContent className='p-0'>
          {activeView === 'pdf' ? (
            <div className='w-full h-[520px] bg-zinc-900 relative rounded-b overflow-hidden'>
              <iframe
                src={`${pdfUrl}#toolbar=1&navpanes=0&scrollbar=1`}
                className='w-full h-full border-0'
                title='PDF Viewer'
              />
            </div>
          ) : (
            <div className='p-5 h-[520px] overflow-y-auto bg-actionHover text-textPrimary leading-relaxed font-sans text-sm whitespace-pre-line'>
              <div className='flex items-center justify-between pb-3 border-b border-divider mb-3'>
                <Typography variant='subtitle2' className='font-bold flex items-center gap-1.5'>
                  <i className='tabler-scan text-primary' />
                  {isEn ? 'AI OCR Full Text Extraction' : 'Nội Dung Toàn Văn Bóc Tách AI OCR'}
                </Typography>
                <Chip label='100% Verified' color='success' size='small' variant='tonal' />
              </div>
              {summaryText}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Fullscreen PDF Modal */}
      <Dialog
        open={isFullscreen}
        onClose={() => setIsFullscreen(false)}
        maxWidth='lg'
        fullWidth
        PaperProps={{ sx: { height: '90vh' } }}
      >
        <DialogTitle className='flex items-center justify-between pb-2 border-b border-divider'>
          <div className='flex items-center gap-2'>
            <i className='tabler-file-type-pdf text-2xl text-error' />
            <div>
              <Typography variant='h6' className='font-bold'>
                {docNumber} — {fileName}
              </Typography>
              <Typography variant='caption' color='text.secondary'>
                {isEn ? 'Official Document Inspection' : 'Hồ Sơ Công Văn Số Hóa Toàn Văn'}
              </Typography>
            </div>
          </div>

          <div className='flex items-center gap-2'>
            <Button
              size='small'
              variant='tonal'
              color='primary'
              startIcon={<i className='tabler-download' />}
              onClick={handleDownload}
            >
              {isEn ? 'Download' : 'Tải Về'}
            </Button>
            <IconButton size='small' onClick={() => setIsFullscreen(false)}>
              <i className='tabler-x' />
            </IconButton>
          </div>
        </DialogTitle>

        <DialogContent className='p-0 bg-zinc-950 flex flex-col h-full'>
          <iframe
            src={`${pdfUrl}#toolbar=1&navpanes=1&scrollbar=1`}
            className='w-full flex-1 border-0'
            title='Fullscreen PDF Preview'
          />
        </DialogContent>
      </Dialog>
    </>
  )
}
