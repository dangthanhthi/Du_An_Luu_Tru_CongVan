'use client'

import { useState, useEffect } from 'react'

// MUI Imports
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import IconButton from '@mui/material/IconButton'
import Chip from '@mui/material/Chip'
import Tooltip from '@mui/material/Tooltip'

// Hook Imports
import { useAppDictionary } from '@/hooks/useDictionary'

interface DocumentQRCodeProps {
  docNo: string
  title?: string
  pdfUrl?: string
}

export default function DocumentQRCode({ docNo, title, pdfUrl }: DocumentQRCodeProps) {
  const { isEn } = useAppDictionary()
  const [shareUrl, setShareUrl] = useState('')
  const [copied, setCopied] = useState(false)
  const [showModal, setShowModal] = useState(false)

  useEffect(() => {
    if (typeof window !== 'undefined') {
      const fullUrl = pdfUrl
        ? (pdfUrl.startsWith('http') ? pdfUrl : `${window.location.origin}${pdfUrl}`)
        : window.location.href
      setShareUrl(fullUrl)
    }
  }, [pdfUrl])

  const handleCopy = () => {
    if (shareUrl) {
      navigator.clipboard.writeText(shareUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    }
  }

  const defaultTitle = isEn ? 'QR Code Quick Access' : 'Mã QR Chia Sẻ & Truy Cập Nhanh'

  // Standard high-reliability QR code image url with local fallback
  const qrImageUrl = shareUrl
    ? `https://api.qrserver.com/v1/create-qr-code/?data=${encodeURIComponent(shareUrl)}&size=200x200&format=svg&color=0f172a`
    : ''

  const qrLargeImageUrl = shareUrl
    ? `https://api.qrserver.com/v1/create-qr-code/?data=${encodeURIComponent(shareUrl)}&size=300x300&format=svg&color=0f172a`
    : ''

  return (
    <>
      <Card className='border border-divider shadow-sm'>
        <CardHeader
          title={
            <div className='flex items-center justify-between'>
              <Typography variant='subtitle2' className='font-semibold uppercase tracking-wider'>
                {title || defaultTitle}
              </Typography>
              <Chip
                label='Live Link'
                size='small'
                color='success'
                variant='tonal'
                avatar={<span className='w-2 h-2 rounded-full bg-success animate-pulse mis-1' />}
              />
            </div>
          }
        />
        <CardContent className='flex flex-col items-center gap-4 text-center'>
          {/* Interactive QR Code Box */}
          <Tooltip title={isEn ? 'Click to enlarge QR Code' : 'Bấm để phóng to mã QR'} arrow>
            <div
              onClick={() => setShowModal(true)}
              className='p-3 bg-white border border-divider rounded-xl cursor-pointer hover:scale-105 transition-transform shadow-md flex items-center justify-center relative group w-[160px] h-[160px]'
            >
              {qrImageUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={qrImageUrl}
                  alt={`QR Code for ${docNo}`}
                  className='w-[136px] h-[136px] object-contain'
                  loading='eager'
                />
              ) : (
                <div className='text-textDisabled text-xs'>{isEn ? 'Generating QR...' : 'Đang tạo mã QR...'}</div>
              )}
            </div>
          </Tooltip>

          <Typography variant='caption' color='text.secondary' className='leading-relaxed'>
            {isEn
              ? `Scan QR code with your mobile camera to open & preview document ${docNo} instantly.`
              : `Quét mã QR bằng điện thoại để mở và xem trực tiếp công văn số ${docNo}.`}
          </Typography>

          <Button
            variant={copied ? 'contained' : 'tonal'}
            color={copied ? 'success' : 'primary'}
            fullWidth
            size='small'
            startIcon={<i className={copied ? 'tabler-check' : 'tabler-copy'} />}
            onClick={handleCopy}
          >
            {copied
              ? (isEn ? 'Link Copied to Clipboard!' : 'Đã Sao Chép Liên Kết!')
              : (isEn ? 'Copy Document Link' : 'Sao Chép Link Chia Sẻ')}
          </Button>
        </CardContent>
      </Card>

      {/* Enlarged QR Modal Dialog */}
      <Dialog open={showModal} onClose={() => setShowModal(false)} maxWidth='xs' fullWidth>
        <DialogTitle className='flex items-center justify-between pb-2'>
          <div>
            <Typography variant='h6' className='font-bold'>
              {isEn ? 'Document QR Code' : 'Mã QR Công Văn Điện Tử'}
            </Typography>
            <Typography variant='caption' color='text.secondary' className='font-mono'>
              {docNo}
            </Typography>
          </div>
          <IconButton size='small' onClick={() => setShowModal(false)}>
            <i className='tabler-x' />
          </IconButton>
        </DialogTitle>
        <DialogContent className='flex flex-col items-center gap-4 pt-4 text-center'>
          <div className='p-4 bg-white border border-divider rounded-2xl shadow-lg w-[240px] h-[240px] flex items-center justify-center'>
            {qrLargeImageUrl && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={qrLargeImageUrl}
                alt={`Large QR Code for ${docNo}`}
                className='w-[200px] h-[200px] object-contain'
                loading='eager'
              />
            )}
          </div>
          <div className='w-full p-2.5 rounded bg-actionHover text-left break-all font-mono text-xs text-textSecondary'>
            <span className='font-semibold block text-[10px] text-textDisabled uppercase'>
              {isEn ? 'Access URL:' : 'Đường Dẫn Truy Cập:'}
            </span>
            {shareUrl}
          </div>
        </DialogContent>
        <DialogActions className='p-4 pt-0'>
          <Button
            fullWidth
            variant='contained'
            color={copied ? 'success' : 'primary'}
            startIcon={<i className={copied ? 'tabler-check' : 'tabler-copy'} />}
            onClick={handleCopy}
          >
            {copied ? (isEn ? 'Copied!' : 'Đã sao chép!') : (isEn ? 'Copy Shareable Link' : 'Sao chép liên kết')}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
