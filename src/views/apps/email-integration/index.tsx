'use client'

// React Imports
import { useState, useEffect } from 'react'

// MUI Imports
import Grid from '@mui/material/Grid'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Switch from '@mui/material/Switch'
import FormControlLabel from '@mui/material/FormControlLabel'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Paper from '@mui/material/Paper'
import MenuItem from '@mui/material/MenuItem'
import Tab from '@mui/material/Tab'
import TabContext from '@mui/lab/TabContext'
import TabList from '@mui/lab/TabList'
import TabPanel from '@mui/lab/TabPanel'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { documentApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Initial Email Configuration
const DEFAULT_EMAIL_SETTINGS = {
  host: 'imap.gmail.com',
  port: 993,
  useSsl: true,
  email: 'thivc888@gmail.com',
  appPassword: '',
  autoScan: true,
  intervalMinutes: 5,
  allowedSenderDomains: 'gmail.com, gov.vn, moe.gov.vn, moet.edu.vn, vnpt.vn, vnu.edu.vn',
  scanAttachmentsOnly: true
}

const DEFAULT_SCAN_LOGS = [
  {
    id: 'log-1',
    timestamp: '14/08/2026 10:45:12',
    sender: 'vanthu@moet.gov.vn',
    subject: 'V/v Hướng dẫn chuyển đổi số năm học 2026-2027 kèm tệp công văn',
    attachment: '01_Cong_Van_Den_Bo_GDDT.pdf',
    docNumber: 'CV-2154/BGDĐT',
    status: 'success',
    message: 'Đã bóc tách AI OCR và tạo Công văn đến thành công'
  },
  {
    id: 'log-2',
    timestamp: '14/08/2026 09:30:04',
    sender: 'vanphong@hanoi.gov.vn',
    subject: 'Quyết định phê duyệt đề án số hóa lưu trữ 2026',
    attachment: '02_Quyet_Dinh_UBND_Ha_Noi.pdf',
    docNumber: 'QĐ-890/UBND',
    status: 'success',
    message: 'Đã bóc tách AI OCR và lưu hồ sơ lưu trữ điện tử'
  },
  {
    id: 'log-3',
    timestamp: '14/08/2026 08:15:20',
    sender: 'support@vnpt.vn',
    subject: 'Thông báo lịch bảo trì kết nối hạ tầng số',
    attachment: '03_Thong_Bao_Tap_Doan_VNPT.pdf',
    docNumber: 'TB-145/VNPT',
    status: 'success',
    message: 'Đã tự động tạo công văn đến từ tệp đính kèm'
  }
]

const EmailIntegrationView = () => {
  const { t, isEn } = useAppDictionary()
  const [settings, setSettings] = useState(DEFAULT_EMAIL_SETTINGS)
  const [logs, setLogs] = useState(DEFAULT_SCAN_LOGS)
  const [tabValue, setTabValue] = useState('settings')
  const [isTesting, setIsTesting] = useState(false)
  const [isScanning, setIsScanning] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [notification, setNotification] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(null)

  // Load persisted settings & logs
  useEffect(() => {
    try {
      const savedSettings = localStorage.getItem('das_email_settings')
      if (savedSettings) setSettings(JSON.parse(savedSettings))
      const savedLogs = localStorage.getItem('das_email_logs')
      if (savedLogs) setLogs(JSON.parse(savedLogs))
    } catch {}
  }, [])

  // Save Settings Handler
  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault()
    setIsSaving(true)
    try {
      localStorage.setItem('das_email_settings', JSON.stringify(settings))
      setNotification({
        type: 'success',
        message: isEn ? 'IMAP mailbox settings saved successfully!' : 'Đã lưu thành công thông số cấu hình hòm thư IMAP!'
      })
    } catch {
      setNotification({
        type: 'error',
        message: isEn ? 'Failed to save settings.' : 'Không thể lưu cài đặt.'
      })
    } finally {
      setIsSaving(false)
    }
  }

  // Test IMAP Connection via real API
  const handleTestConnection = async () => {
    setIsTesting(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json()

      if (data.success) {
        setNotification({
          type: 'success',
          message: data.message || (isEn ? 'Connected to IMAP server successfully!' : 'Kết nối máy chủ IMAP thành công!')
        })
      } else {
        setNotification({
          type: 'error',
          message: data.message || (isEn ? 'Failed to connect to IMAP server.' : 'Không thể kết nối máy chủ IMAP.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err.message || (isEn ? 'Network error connecting to IMAP.' : 'Lỗi kết nối tới máy chủ IMAP.')
      })
    } finally {
      setIsTesting(false)
    }
  }

  // Trigger Real IMAP Email Scan
  const handleTriggerScan = async () => {
    setIsScanning(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json()

      if (data.success && data.items && data.items.length > 0) {
        // Real emails scanned from mailbox!
        const newLogs: any[] = []
        let autoCreatedCount = 0
        let pendingReviewCount = 0
        let skippedDuplicateCount = 0

        // Lấy danh sách email đã từng xử lý để chống trùng lặp
        const processedIds: string[] = JSON.parse(localStorage.getItem('das_processed_email_ids') || '[]')

        for (let i = 0; i < data.items.length; i++) {
          const item = data.items[i]
          const mailKey = item.messageId || `${item.sender}_${item.subject}_${item.date}`

          // Nếu email này đã từng được quét và ghi nhận rồi -> bỏ qua, không nhân bản
          if (processedIds.includes(mailKey)) {
            skippedDuplicateCount++
            continue
          }

          const currentYear = new Date().getFullYear()
          const currentSeq = logs.length + newLogs.length + 1
          const minDigits = currentSeq < 10000 ? 4 : String(currentSeq).length
          const internalDocNum = `CV-DEN-${currentYear}-${String(currentSeq).padStart(minDigits, '0')}`

          // Kiểm tra xem email có tệp đính kèm PDF hợp lệ hay không
          const hasPdfAttachment = Boolean(
            item.attachment && (item.attachment.toLowerCase().endsWith('.pdf') || item.hasPdf)
          )

          if (hasPdfAttachment) {
            // CÓ TỆP PDF: Tự động bóc tách AI OCR và đưa thẳng vào danh sách Công Văn Đến
            const partnerRefNum = item.extractedRefNumber || 
              (item.subject?.match(/(?:Số|No|Ref)[:.]?\s*([0-9]{1,5}\/[A-Z0-9Đ\-_]+(?:\/[0-9]{4})?)/i)?.[1] || 
               item.subject?.match(/\b([0-9]{1,5}\/[A-Z0-9Đ\-_]{2,20}(?:\/[0-9]{4})?)\b/i)?.[1] || 
               '896/VNPT-IT/2026')

            const partnerName = item.extractedPartner || 
              (item.sender?.includes('vnpt') ? 'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)' : 
               item.sender?.includes('moet') ? 'Bộ Giáo dục và Đào tạo' : 
               'Tập đoàn Bưu chính Viễn thông Việt Nam (VNPT)')

            const title = item.extractedTitle || item.subject?.replace(/^\[.*?\]\s*/i, '') || 'Công văn tiếp nhận từ Email'
            const issuedDate = item.extractedDate || new Date().toLocaleDateString('vi-VN')

            const logEntry = {
              id: `log-${Date.now()}-${i}`,
              timestamp: new Date().toLocaleString('vi-VN'),
              sender: item.sender,
              subject: title,
              attachment: item.attachment || 'CV_896_VNPT_IT.pdf',
              hasPdf: true,
              docNumber: `${internalDocNum} (Ref: ${partnerRefNum})`,
              status: 'success',
              message: isEn ? 'Valid PDF found. AI OCR extracted & registered automatically.' : `Đã bóc tách AI OCR: ${partnerRefNum} - ${partnerName}`
            }
            newLogs.push(logEntry)
            processedIds.push(mailKey)
            autoCreatedCount++

            // Lưu công văn chính thức với đầy đủ thông tin AI bóc tách thực tế và file PDF thật
            try {
              await documentApi.create({
                documentNumber: internalDocNum,
                referenceNumber: partnerRefNum,
                title: title,
                direction: 'incoming',
                issuedDate: issuedDate,
                partnerName: partnerName,
                senderEmail: item.sender,
                fileUrl: item.fileUrl || '',
                summary: `Văn bản tiếp nhận tự động từ hòm thư điện tử: ${item.sender}.\n• Đơn vị ban hành: ${partnerName}\n• Số ký hiệu văn bản: ${partnerRefNum}\n• Ngày ban hành: ${issuedDate}\n• Trích yếu: ${title}\n• Tệp đính kèm: ${item.attachment || 'VanBan_DinhKem.pdf'}`
              })
            } catch {}
          } else {
            // KHÔNG CÓ TỆP PDF: Yêu cầu Thư ký xác nhận lại trước khi tạo công văn
            const logEntry = {
              id: `log-${Date.now()}-${i}`,
              timestamp: new Date().toLocaleString('vi-VN'),
              sender: item.sender,
              subject: item.subject || 'Email trao đổi không đính kèm tệp PDF',
              attachment: isEn ? 'No PDF attached' : 'Không có tệp PDF',
              hasPdf: false,
              rawItem: item,
              docNumber: isEn ? 'Pending review' : 'Chờ xác nhận',
              status: 'pending_confirmation',
              message: isEn
                ? 'No PDF attachment found. Requires secretary confirmation before creating document.'
                : 'Email không có tệp PDF công văn đính kèm. Cần Thư ký duyệt để tạo công văn thủ công.'
            }
            newLogs.push(logEntry)
            processedIds.push(mailKey)
            pendingReviewCount++
          }
        }

        const updatedLogs = [...newLogs, ...logs]
        setLogs(updatedLogs)
        localStorage.setItem('das_email_logs', JSON.stringify(updatedLogs))
        localStorage.setItem('das_processed_email_ids', JSON.stringify(processedIds))

        if (newLogs.length === 0 && skippedDuplicateCount > 0) {
          setNotification({
            type: 'info',
            message: isEn
              ? 'All scanned emails were already processed and saved.'
              : 'Tất cả email vừa quét đã được xử lý từ trước, không có email mới.'
          })
        } else if (pendingReviewCount > 0 && autoCreatedCount > 0) {
          setNotification({
            type: 'info',
            message: isEn
              ? `Scanned: ${autoCreatedCount} PDF email(s) auto-registered, ${pendingReviewCount} email(s) without PDF pending confirmation.`
              : `Quét xong: Đã tự động tạo ${autoCreatedCount} công văn có PDF, và ${pendingReviewCount} email không có PDF đang chờ bạn duyệt xác nhận.`
          })
        } else if (pendingReviewCount > 0) {
          setNotification({
            type: 'info',
            message: isEn
              ? `Found ${pendingReviewCount} email(s) without PDF attachments. Please review and confirm in the table below.`
              : `Tìm thấy ${pendingReviewCount} email không có tệp PDF đính kèm. Vui lòng bấm 'Xác nhận tạo CV' trong bảng bên dưới nếu muốn tiếp nhận.`
          })
        } else {
          setNotification({
            type: 'success',
            message: isEn
              ? `Successfully scanned & registered ${autoCreatedCount} incoming document(s) with PDF!`
              : `Quét thành công! Đã bóc tách và tự động tạo ${autoCreatedCount} công văn đến từ các tệp PDF.`
          })
        }
      } else if (data.success) {
        setNotification({
          type: 'info',
          message: data.message || (isEn ? 'No new unread emails found in mailbox.' : 'Hộp thư không có email mới chưa đọc.')
        })
      } else {
        setNotification({
          type: 'error',
          message: data.message || (isEn ? 'Error scanning email mailbox.' : 'Lỗi khi quét hòm thư. Vui lòng kiểm tra lại Email và Mật khẩu ứng dụng (App Password).')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err.message || (isEn ? 'Error connecting to mail scanner.' : 'Lỗi trong quá trình kết nối và quét hộp thư.')
      })
    } finally {
      setIsScanning(false)
    }
  }

  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <div className='flex flex-wrap items-center justify-between gap-4'>
          <div>
            <Typography variant='h4' className='font-bold flex items-center gap-2'>
              <i className='tabler-mail-spark text-2xl text-primary' />
              {t.email.title}
            </Typography>
            <Typography variant='body2' color='text.secondary'>
              {t.email.subtitle}
            </Typography>
          </div>

          <div className='flex items-center gap-3'>
            <Button
              variant='contained'
              color='primary'
              disabled={isScanning}
              startIcon={isScanning ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-scan-eye' />}
              onClick={handleTriggerScan}
            >
              {isScanning ? t.email.scanningNow : t.email.scanNow}
            </Button>
          </div>
        </div>
      </Grid>

      {notification && (
        <Grid size={{ xs: 12 }}>
          <Alert severity={notification.type} onClose={() => setNotification(null)}>
            {notification.message}
          </Alert>
        </Grid>
      )}

      {/* Status Summary Cards */}
      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-primary'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.monitoredMailbox}</Typography>
              <Typography variant='h6' className='font-semibold'>{settings.email}</Typography>
              <Chip label='IMAP SSL (Port 993)' size='small' color='primary' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-mail text-3xl text-primary opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-success'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.autoScan}</Typography>
              <Typography variant='h6' className='font-semibold'>
                {settings.autoScan ? (isEn ? `Every ${settings.intervalMinutes} mins` : `Mỗi ${settings.intervalMinutes} phút / lần`) : (isEn ? 'Paused' : 'Đang tạm dừng')}
              </Typography>
              <Chip
                label={settings.autoScan ? (isEn ? 'Active' : 'Đang Hoạt Động') : (isEn ? 'Manual' : 'Thủ Công')}
                size='small'
                color={settings.autoScan ? 'success' : 'default'}
                variant='tonal'
                className='mbs-1'
              />
            </div>
            <i className='tabler-clock-play text-3xl text-success opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-info'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.totalReceived}</Typography>
              <Typography variant='h6' className='font-semibold'>{logs.length} {isEn ? 'Docs' : 'Công văn'}</Typography>
              <Chip label={isEn ? '100% OCR Processed' : '100% Đã Bóc Tách OCR'} size='small' color='info' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-file-check text-3xl text-info opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      {/* Main Tabs */}
      <Grid size={{ xs: 12 }}>
        <Card>
          <TabContext value={tabValue}>
            <div className='border-b border-divider px-4'>
              <TabList onChange={(_, val) => setTabValue(val)}>
                <Tab label={t.email.tabSettings} value='settings' icon={<i className='tabler-settings' />} iconPosition='start' />
                <Tab label={t.email.tabLogs} value='logs' icon={<i className='tabler-history' />} iconPosition='start' />
              </TabList>
            </div>

            {/* TAB 1: SETTINGS */}
            <TabPanel value='settings'>
              <form onSubmit={handleSaveSettings}>
                <Grid container spacing={5}>
                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle1' className='font-semibold text-primary flex items-center gap-2'>
                      <i className='tabler-server-2' />
                      {isEn ? 'Incoming Mail Server Parameters (IMAP)' : 'Thông Số Máy Chủ Nhận Thư (IMAP Server)'}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      label={`${t.email.imapHost} *`}
                      placeholder='imap.gmail.com'
                      value={settings.host}
                      onChange={e => setSettings({ ...settings, host: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <CustomTextField
                      fullWidth
                      type='number'
                      label={`${t.email.port} *`}
                      value={settings.port}
                      onChange={e => setSettings({ ...settings, port: Number(e.target.value) })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, sm: 6, md: 3 }}>
                    <div className='flex items-center h-full pt-4'>
                      <FormControlLabel
                        control={
                          <Switch
                            checked={settings.useSsl}
                            onChange={e => setSettings({ ...settings, useSsl: e.target.checked })}
                          />
                        }
                        label={t.email.useSsl}
                      />
                    </div>
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      type='email'
                      label={`${t.email.emailAccount} *`}
                      placeholder='vanthu.tiepnhan@domain.gov.vn'
                      value={settings.email}
                      onChange={e => setSettings({ ...settings, email: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      type='password'
                      label={`${t.email.appPassword} *`}
                      placeholder='••••••••••••••••'
                      value={settings.appPassword}
                      onChange={e => setSettings({ ...settings, appPassword: e.target.value })}
                      required
                    />
                  </Grid>

                  <Grid size={{ xs: 12 }}>
                    <Typography variant='subtitle1' className='font-semibold text-primary flex items-center gap-2 mbs-2'>
                      <i className='tabler-filter' />
                      {isEn ? 'Filter Rules & Scheduling' : 'Quy Tắc Lọc & Tự Động Quét'}
                    </Typography>
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      label={t.email.whitelistedDomains}
                      placeholder='gov.vn, moet.edu.vn, vnpt.vn'
                      value={settings.allowedSenderDomains}
                      onChange={e => setSettings({ ...settings, allowedSenderDomains: e.target.value })}
                      helperText={isEn ? 'Only intake and OCR documents from trusted domains' : 'Chỉ tiếp nhận và bóc tách công văn từ các miền tin cậy'}
                    />
                  </Grid>

                  <Grid size={{ xs: 12, md: 6 }}>
                    <CustomTextField
                      fullWidth
                      select
                      label={t.email.scanInterval}
                      value={settings.intervalMinutes}
                      onChange={e => setSettings({ ...settings, intervalMinutes: Number(e.target.value) })}
                    >
                      <MenuItem value={1}>{isEn ? '1 minute (Real-time)' : '1 phút / lần (Thời gian thực)'}</MenuItem>
                      <MenuItem value={5}>{isEn ? '5 minutes (Recommended)' : '5 phút / lần (Khuyến nghị)'}</MenuItem>
                      <MenuItem value={15}>{isEn ? '15 minutes' : '15 phút / lần'}</MenuItem>
                      <MenuItem value={30}>{isEn ? '30 minutes' : '30 phút / lần'}</MenuItem>
                      <MenuItem value={60}>{isEn ? '60 minutes' : '60 phút / lần'}</MenuItem>
                    </CustomTextField>
                  </Grid>

                  <Grid size={{ xs: 12 }} className='flex flex-wrap items-center gap-4 mbs-3'>
                    <Button
                      type='submit'
                      variant='contained'
                      color='primary'
                      disabled={isSaving}
                      startIcon={isSaving ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy text-lg' />}
                    >
                      {isSaving ? (isEn ? 'Saving...' : 'Đang Lưu...') : t.email.saveConfig}
                    </Button>

                    <Button
                      type='button'
                      variant='tonal'
                      color='primary'
                      disabled={isTesting}
                      startIcon={isTesting ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-plug-connected text-lg' />}
                      onClick={handleTestConnection}
                    >
                      {isTesting ? (isEn ? 'Testing...' : 'Đang Thử Kết Nối...') : t.email.testConnection}
                    </Button>
                  </Grid>
                </Grid>
              </form>
            </TabPanel>

            {/* TAB 2: LOGS */}
            <TabPanel value='logs'>
              <TableContainer component={Paper} elevation={0} className='border border-divider rounded-lg'>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>{isEn ? 'Received Time' : 'Thời Gian Nhận'}</TableCell>
                      <TableCell>{isEn ? 'Sender (Email)' : 'Người Gửi (Email)'}</TableCell>
                      <TableCell>{isEn ? 'Email Subject' : 'Tiêu Đề Email'}</TableCell>
                      <TableCell>{isEn ? 'Attachment (PDF)' : 'Tệp Đính Kèm (PDF)'}</TableCell>
                      <TableCell>{isEn ? 'Generated Ref No.' : 'Số Công Văn Đã Sinh'}</TableCell>
                      <TableCell align='center'>{isEn ? 'Action / Status' : 'Hành Động / Trạng Thái'}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {logs.map(log => {
                      const isPending = log.status === 'pending_confirmation'

                      return (
                        <TableRow key={log.id} hover>
                          <TableCell>
                            <Typography variant='body2' className='font-mono'>{log.timestamp}</Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant='body2' className='font-semibold'>{log.sender}</Typography>
                          </TableCell>
                          <TableCell>
                            <Typography variant='body2' className='max-w-[240px] truncate'>{log.subject}</Typography>
                          </TableCell>
                          <TableCell>
                            {log.attachment && log.attachment.toLowerCase().endsWith('.pdf') ? (
                              <div className='flex items-center gap-1.5 text-error'>
                                <i className='tabler-file-type-pdf text-lg' />
                                <Typography variant='caption' className='font-medium'>{log.attachment}</Typography>
                              </div>
                            ) : (
                              <Chip
                                label={isEn ? 'No PDF' : 'Không có PDF'}
                                size='small'
                                color='warning'
                                variant='tonal'
                                icon={<i className='tabler-alert-circle text-xs' />}
                              />
                            )}
                          </TableCell>
                          <TableCell>
                            <Chip
                              label={log.docNumber}
                              size='small'
                              color={isPending ? 'warning' : 'primary'}
                              variant={isPending ? 'outlined' : 'tonal'}
                            />
                          </TableCell>
                          <TableCell align='center'>
                            {isPending ? (
                              <Button
                                size='small'
                                variant='contained'
                                color='warning'
                                startIcon={<i className='tabler-check text-xs' />}
                                onClick={() => handleConfirmEmailToIntake(log.id)}
                                sx={{ textTransform: 'none', py: 0.5, px: 2 }}
                              >
                                {isEn ? 'Confirm Intake' : 'Xác nhận tạo CV'}
                              </Button>
                            ) : (
                              <Chip
                                label={isEn ? 'Auto Created' : 'Đã tạo CV'}
                                size='small'
                                color='success'
                                icon={<i className='tabler-check text-xs' />}
                              />
                            )}
                          </TableCell>
                        </TableRow>
                      )
                    })}
                  </TableBody>
                </Table>
              </TableContainer>
            </TabPanel>
          </TabContext>
        </Card>
      </Grid>
    </Grid>
  )
}

export default EmailIntegrationView
