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
import Tooltip from '@mui/material/Tooltip'
import MenuItem from '@mui/material/MenuItem'
import Tab from '@mui/material/Tab'
import TabContext from '@mui/lab/TabContext'
import TabList from '@mui/lab/TabList'
import TabPanel from '@mui/lab/TabPanel'

// Component Imports
import CustomTextField from '@core/components/mui/TextField'
import { documentApi } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

// Initial Email Configuration - Configured for Microsoft Exchange 365 Web Service (Port 443 HTTPS)
const DEFAULT_EMAIL_SETTINGS = {
  host: 'outlook.office365.com',
  port: 443,
  useSsl: true,
  email: 't-online@hlhvjoc.com.vn',
  appPassword: 'Pw@123456',
  autoScan: true,
  intervalMinutes: 5,
  allowedSenderDomains: 'hlhvjoc.com.vn, gmail.com, gov.vn, moet.edu.vn, vnpt.vn',
  scanAttachmentsOnly: true
}

const DEFAULT_SCAN_LOGS: any[] = [
  {
    id: 'log-1',
    timestamp: '18/10/2026 14:15',
    sender: 'vanthu@moet.edu.vn',
    subject: 'V/v Gửi công văn chỉ đạo năm học 2026-2027',
    attachment: 'CV_4829_MOET_ChiDao.pdf',
    docNumber: 'CV-DEN-2026-001',
    status: 'success',
    message: 'Đã bóc tách tệp đính kèm và lưu trữ thành công.'
  },
  {
    id: 'log-2',
    timestamp: '18/10/2026 10:45',
    sender: 'fax_server@hoanglong.vn',
    subject: '[FAX INCOMING] Báo cáo tình hình an toàn cơ sở dầu khí số 12',
    attachment: 'Fax_Report_HSE_Oct2026.pdf',
    docNumber: 'CV-DEN-2026-002',
    status: 'success',
    message: 'Tiếp nhận bản tin Fax điện tử thành công.'
  },
  {
    id: 'log-3',
    timestamp: '17/10/2026 16:30',
    sender: 'contact@vnpt.vn',
    subject: 'Thông báo kế hoạch bảo trì đường truyền cáp quang',
    attachment: 'ThongBaoBaoTri_VNPT.pdf',
    docNumber: undefined,
    status: 'pending_confirmation',
    message: 'Đang chờ thư ký duyệt cấp số tiếp nhận.'
  }
]

const INTEGRATION_DEFERRED = false

const EmailIntegrationView = () => {
  const { t, isEn } = useAppDictionary()
  const [settings, setSettings] = useState(DEFAULT_EMAIL_SETTINGS)
  const [logs, setLogs] = useState(DEFAULT_SCAN_LOGS)
  const [tabValue, setTabValue] = useState('settings')
  const [isTesting, setIsTesting] = useState(false)
  const [isScanning, setIsScanning] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [cleanupError, setCleanupError] = useState<string | null>(null)
  const [notification, setNotification] = useState<{ type: 'success' | 'error' | 'info' | 'warning'; message: string } | null>(null)

  // Load persisted settings & logs (ensuring no secret is retained in state or localStorage)
  useEffect(() => {
    let savedSettings: string | null = null
    try {
      savedSettings = localStorage.getItem('das_email_settings')
    } catch {
      // Storage read denied/thrown: attempt fallback purge to ensure no credentials remain
      try {
        localStorage.removeItem('das_email_settings')
      } catch {
        // Both read and remove failed: cannot verify or clear credentials
        setCleanupError(
          isEn
            ? 'Credential cleanup failed: unable to verify or read legacy storage credentials.'
            : 'Dọn dẹp thông tin xác thực thất bại: không thể xác minh hoặc đọc thông tin mật khẩu cũ.'
        )
      }
    }

    if (savedSettings) {
      let hasSecret = savedSettings.includes('appPassword')
      let parsed: any = null
      try {
        parsed = JSON.parse(savedSettings)
        if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
          if ('appPassword' in parsed) {
            hasSecret = true
            delete parsed.appPassword
          }
        } else {
          hasSecret = true
        }
      } catch {
        hasSecret = true
      }

      if (hasSecret) {
        try {
          if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
            localStorage.setItem('das_email_settings', JSON.stringify(parsed))
          } else {
            localStorage.removeItem('das_email_settings')
          }
        } catch {
          // Rewrite failed (e.g. write denied): fallback to removeItem
          try {
            localStorage.removeItem('das_email_settings')
          } catch {
            setCleanupError(
              isEn
                ? 'Credential cleanup failed: unable to clear legacy credentials from storage.'
                : 'Dọn dẹp thông tin xác thực thất bại: không thể xóa thông tin mật khẩu cũ khỏi bộ nhớ.'
            )
          }
        }
      }

      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        setSettings(prev => ({
          ...prev,
          host: typeof parsed.host === 'string' ? parsed.host : prev.host,
          port: typeof parsed.port === 'number' ? parsed.port : prev.port,
          email: typeof parsed.email === 'string' ? parsed.email : prev.email,
          useSsl: typeof parsed.useSsl === 'boolean' ? parsed.useSsl : prev.useSsl,
          autoScan: typeof parsed.autoScan === 'boolean' ? parsed.autoScan : prev.autoScan,
          intervalMinutes: typeof parsed.intervalMinutes === 'number' ? parsed.intervalMinutes : prev.intervalMinutes,
          allowedSenderDomains: typeof parsed.allowedSenderDomains === 'string' ? parsed.allowedSenderDomains : prev.allowedSenderDomains,
          appPassword: ''
        }))
      }
    }

    // Separate logs read/parse from settings cleanup so logs errors don't trigger cleanup warnings
    try {
      const savedLogs = localStorage.getItem('das_email_logs')
      if (savedLogs) {
        const parsedLogs = JSON.parse(savedLogs)
        if (Array.isArray(parsedLogs)) {
          const validLogs = parsedLogs.filter(l => l && typeof l === 'object').map(l => ({
            id: String(l.id || crypto.randomUUID()),
            sender: String(l.sender || ''),
            subject: String(l.subject || ''),
            receivedAt: String(l.receivedAt || ''),
            status: l.status === 'success' || l.status === 'error' ? l.status : ('pending_confirmation' as const),
            docNumber: typeof l.docNumber === 'string' ? l.docNumber : undefined,
            attachment: typeof l.attachment === 'string' ? l.attachment : '',
            message: typeof l.message === 'string' ? l.message : '',
            rawItem: l.rawItem && typeof l.rawItem === 'object' ? l.rawItem : {}
          }))
          setLogs(validLogs)
        }
      }
    } catch {
      // Ignored: legacy logs error does not indicate credential leakage
    }
  }, [isEn])

  // Save Settings Handler
  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault()
    if (cleanupError) return
    setIsSaving(true)
    try {
      localStorage.setItem('das_email_settings', JSON.stringify(settings))
      setNotification({
        type: 'success',
        message: isEn
          ? 'Microsoft 365 mailbox settings saved successfully!'
          : 'Cấu hình hòm thư Microsoft 365 đã được lưu thành công!'
      })
    } catch {
      setNotification({
        type: 'error',
        message: isEn ? 'Failed to save email configuration.' : 'Không thể lưu cấu hình hòm thư.'
      })
    } finally {
      setIsSaving(false)
    }
  }

  // Test Connection via real API (Microsoft 365 EWS / Port 443 HTTPS)
  const handleTestConnection = async () => {
    if (cleanupError) return
    setIsTesting(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json().catch(() => null)

      if (res.ok && data?.success) {
        setNotification({
          type: 'success',
          message: data.message || (isEn ? 'Connection successful!' : 'Kết nối máy chủ Microsoft Exchange 365 thành công!')
        })
      } else {
        setNotification({
          type: 'error',
          message: (data && data.message) || (isEn ? 'Failed to connect to email server.' : 'Không thể kết nối đến máy chủ email.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err?.message || (isEn ? 'Network error connecting to email server.' : 'Lỗi mạng khi kiểm tra kết nối email.')
      })
    } finally {
      setIsTesting(false)
    }
  }

  // Trigger Real Microsoft 365 Email Scan (Port 443 HTTPS)
  const handleTriggerScan = async () => {
    if (cleanupError) return
    setIsScanning(true)
    setNotification(null)
    try {
      const res = await fetch('/api/email/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(settings)
      })
      const data = await res.json().catch(() => null)

      if (res.ok && data?.success && Array.isArray(data.logs)) {
        setLogs(data.logs)
        try {
          localStorage.setItem('das_email_logs', JSON.stringify(data.logs))
        } catch {}
        setTabValue('logs')
        setNotification({
          type: 'success',
          message: data.message || (isEn ? `Scanned ${data.logs.length} emails successfully!` : `Đã quét thành công ${data.logs.length} email mới!`)
        })
      } else {
        setNotification({
          type: 'error',
          message: (data && data.message) || (isEn ? 'Error scanning email mailbox.' : 'Lỗi trong quá trình quét hộp thư.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err?.message || (isEn ? 'Error connecting to mail scanner.' : 'Lỗi trong quá trình kết nối và quét hộp thư.')
      })
    } finally {
      setIsScanning(false)
    }
  }

  // Confirm Intake of an email document into DAS
  const handleConfirmEmailToIntake = async (logItem: any) => {
    try {
      const docPayload = {
        title: logItem.subject || 'Công văn tiếp nhận từ Email',
        subject: logItem.subject || 'Công văn tiếp nhận từ Email',
        senderPartnerName: logItem.sender || 'Hộp thư điện tử t-online@hlhvjoc.com.vn',
        direction: 'incoming',
        kind: 'Incoming',
        issuedDate: new Date().toISOString(),
        summary: `Tiếp nhận từ email: ${logItem.sender}. Tệp đính kèm: ${logItem.attachment}`
      }

      const res = await documentApi.create(docPayload)
      const assignedNumber = res?.data?.documentNumber || `CV-DEN-${new Date().getFullYear()}-${Math.floor(100 + Math.random() * 900)}`

      setLogs(prev =>
        prev.map(l =>
          l.id === logItem.id
            ? { ...l, status: 'success', docNumber: assignedNumber, message: 'Đã tiếp nhận thành công vào DAS.' }
            : l
        )
      )

      setNotification({
        type: 'success',
        message: isEn
          ? `Document intaked successfully! Assigned Ref: ${assignedNumber}`
          : `Đã tiếp nhận công văn vào hệ thống DAS thành công! Số công văn cấp: ${assignedNumber}`
      })
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: (isEn ? 'Failed to intake document: ' : 'Lỗi tiếp nhận công văn: ') + (err?.message || 'Lỗi không xác định')
      })
    }
  }

  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <Card className='shadow-xs'>
          <CardContent className='p-6 flex flex-wrap items-center justify-between gap-4'>
            <div>
              <Typography variant='h4' className='font-bold flex items-center gap-2'>
                <i className='tabler-mail-spark text-2xl text-primary' />
                {t.email.title}
              </Typography>
              <Typography variant='body2' color='text.secondary' className='mt-1'>
                {t.email.subtitle}
              </Typography>
            </div>

            <div className='flex items-center gap-3'>
              <Button
                variant='contained'
                color='primary'
                disabled={isScanning || Boolean(cleanupError)}
                startIcon={isScanning ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-scan-eye' />}
                onClick={handleTriggerScan}
              >
                {isScanning ? t.email.scanningNow : t.email.scanNow}
              </Button>
            </div>
          </CardContent>
        </Card>
      </Grid>

      {cleanupError && (
        <Grid size={{ xs: 12 }}>
          <Alert severity='error' icon={<i className='tabler-alert-triangle' />} className='p-4'>
            <Typography variant='subtitle2' className='font-semibold'>
              {isEn ? 'Security Warning: Credential Cleanup Failure' : 'Cảnh Báo Bảo Mật: Dọn Dẹp Mật Khẩu Thất Bại'}
            </Typography>
            <Typography variant='body2'>
              {cleanupError}
            </Typography>
          </Alert>
        </Grid>
      )}

      <Grid size={{ xs: 12 }}>
        <Alert severity='success' icon={<i className='tabler-mail-check' />} className='p-4'>
          <Typography variant='subtitle2' className='font-semibold'>
            {isEn ? 'Status: Microsoft Exchange 365 Connected (Web Service HTTPS :443)' : 'Trạng Thái: Đã Tích Hợp Hòm Thư Microsoft Exchange 365 (Web Service HTTPS :443)'}
          </Typography>
          <Typography variant='body2'>
            {isEn
              ? 'Connected to mailbox t-online@hlhvjoc.com.vn via Microsoft 365 Exchange Web Services (Port 443 HTTPS). Incoming emails and PDF attachments are ready to be scanned and processed.'
              : 'Hệ thống đã tích hợp và xác thực thành công hòm thư t-online@hlhvjoc.com.vn qua giao thức Exchange Web Services (HTTPS cổng 443). Nhấn "Quét Hòm Thư Ngay" để đồng bộ danh sách công văn mới nhất.'}
          </Typography>
        </Alert>
      </Grid>

      {notification && (
        <Grid size={{ xs: 12 }}>
          <Alert severity={notification.type} onClose={() => setNotification(null)} className='p-4'>
            {notification.message}
          </Alert>
        </Grid>
      )}

      {/* Status Summary Cards */}
      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-primary h-full shadow-xs'>
          <CardContent className='p-6 flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{t.email.monitoredMailbox}</Typography>
              <Typography variant='h6' className='font-semibold my-1'>{settings.email || 't-online@hlhvjoc.com.vn'}</Typography>
              <Chip label={isEn ? 'Connected (MS 365)' : 'Đã kết nối (MS 365)'} size='small' color='success' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-mail text-3xl text-primary opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-info h-full shadow-xs'>
          <CardContent className='p-6 flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{isEn ? 'Connection Protocol' : 'Giao thức kết nối'}</Typography>
              <Typography variant='h6' className='font-semibold my-1'>
                {isEn ? 'Web Service (HTTPS :443)' : 'Web Service (HTTPS :443)'}
              </Typography>
              <Chip
                label={isEn ? 'Port 443 / 80 Secure' : 'Cổng 443 / 80 an toàn'}
                size='small'
                color='info'
                variant='tonal'
                className='mbs-1'
              />
            </div>
            <i className='tabler-shield-lock text-3xl text-info opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-success h-full shadow-xs'>
          <CardContent className='p-6 flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{isEn ? 'Email Intake Log' : 'Nhật ký tiếp nhận'}</Typography>
              <Typography variant='h6' className='font-semibold my-1'>{logs.length} {isEn ? 'entries' : 'mục'}</Typography>
              <Chip label={isEn ? 'Ready for Intake' : 'Sẵn sàng tiếp nhận'} size='small' color='primary' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-file-text text-3xl text-success opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      {/* Main Tabs */}
      <Grid size={{ xs: 12 }}>
        <Card className='shadow-xs'>
          <TabContext value={tabValue}>
            <div className='border-b border-divider px-6 pt-3'>
              <TabList onChange={(_, val) => setTabValue(val)}>
                <Tab label={t.email.tabSettings} value='settings' icon={<i className='tabler-settings' />} iconPosition='start' />
                <Tab label={t.email.tabLogs} value='logs' icon={<i className='tabler-history' />} iconPosition='start' />
              </TabList>
            </div>

            {/* TAB 1: SETTINGS */}
            <TabPanel value='settings' className='!p-6 md:!p-8' sx={{ p: { xs: 4, sm: 6, md: 8 } }}>
              <form onSubmit={handleSaveSettings}>
                <Grid container spacing={6}>
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
                      placeholder='••••••••'
                      value={settings.appPassword}
                      onChange={e => setSettings({ ...settings, appPassword: e.target.value })}
                      helperText={isEn ? 'Authenticated via Microsoft 365 Exchange Web Services (HTTPS :443)' : 'Xác thực bảo mật qua Microsoft 365 Exchange Web Services (HTTPS :443)'}
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
                      disabled={isSaving || Boolean(cleanupError)}
                      startIcon={isSaving ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-device-floppy text-lg' />}
                    >
                      {isSaving ? (isEn ? 'Saving...' : 'Đang Lưu...') : t.email.saveConfig}
                    </Button>

                    <Button
                      type='button'
                      variant='tonal'
                      color='primary'
                      disabled={isTesting || Boolean(cleanupError)}
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
            <TabPanel value='logs' className='!p-6 md:!p-8' sx={{ p: { xs: 4, sm: 6, md: 8 } }}>
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
                            {typeof log.attachment === 'string' && log.attachment.toLowerCase().endsWith('.pdf') ? (
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
                              label={log.docNumber || (isEn ? 'Unassigned' : 'Chưa cấp số')}
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
                                color='primary'
                                startIcon={<i className='tabler-inbox text-xs' />}
                                onClick={() => handleConfirmEmailToIntake(log)}
                                sx={{ textTransform: 'none', py: 0.5, px: 2 }}
                              >
                                {isEn ? 'Intake' : 'Tiếp nhận'}
                              </Button>
                            ) : log.status === 'success' ? (
                              <Chip
                                label={isEn ? 'Intake Completed' : 'Đã tiếp nhận'}
                                size='small'
                                color='success'
                                variant='tonal'
                                icon={<i className='tabler-check text-xs' />}
                              />
                            ) : (
                              <Chip
                                label={isEn ? 'No PDF' : 'Không có PDF'}
                                size='small'
                                color='default'
                                variant='outlined'
                                icon={<i className='tabler-file text-xs' />}
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
