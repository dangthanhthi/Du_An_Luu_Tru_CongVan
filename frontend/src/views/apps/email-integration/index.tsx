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

// Initial Email Configuration
const DEFAULT_EMAIL_SETTINGS = {
  host: 'imap.gmail.com',
  port: 993,
  useSsl: true,
  email: '',
  appPassword: '',
  autoScan: true,
  intervalMinutes: 5,
  allowedSenderDomains: 'gmail.com, gov.vn, moe.gov.vn, moet.edu.vn, vnpt.vn, vnu.edu.vn',
  scanAttachmentsOnly: true
}

const DEFAULT_SCAN_LOGS: any[] = []

const INTEGRATION_DEFERRED = true

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

  // Save Settings Handler - deferred to backend authority, no secret stored
  const handleSaveSettings = (e: React.FormEvent) => {
    e.preventDefault()
    if (cleanupError) return
    setIsSaving(true)
    try {
      const { appPassword: _omitted, ...sanitized } = settings
      localStorage.setItem('das_email_settings', JSON.stringify(sanitized))
      setNotification({
        type: 'info',
        message: isEn
          ? 'Mailbox integration is deferred (INTEGRATION_DEFERRED). Local parameters updated (secrets omitted); backend credential configuration pending.'
          : 'Tính năng hòm thư đang tạm hoãn (INTEGRATION_DEFERRED). Thông số cục bộ đã cập nhật (không lưu mật khẩu); cấu hình chứng thực backend đang chờ phê duyệt.'
      })
    } catch {
      setNotification({
        type: 'error',
        message: isEn ? 'Failed to save local email configuration.' : 'Không thể lưu cấu hình hòm thư cục bộ.'
      })
    } finally {
      setIsSaving(false)
    }
  }

  // Test IMAP Connection via real API (neutralized under deferred integration)
  const handleTestConnection = async () => {
    if (cleanupError) return
    if (INTEGRATION_DEFERRED) {
      setNotification({
        type: 'warning',
        message: isEn
          ? 'IMAP connection test is deferred (503 INTEGRATION_DEFERRED). Backend mailbox service is not active.'
          : 'Kiểm tra kết nối IMAP đang tạm hoãn (503 INTEGRATION_DEFERRED). Dịch vụ hòm thư backend chưa được kích hoạt.'
      })
      return
    }
    setIsTesting(true)
    setNotification(null)
    try {
      const { appPassword: _omitted, ...sanitized } = settings
      const res = await fetch('/api/email/test', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(sanitized)
      })
      const data = await res.json().catch(() => null)

      if (res.status === 503 || (data && data.code === 'INTEGRATION_DEFERRED') || INTEGRATION_DEFERRED) {
        setNotification({
          type: 'warning',
          message: isEn
            ? 'IMAP connection test is deferred (503 INTEGRATION_DEFERRED). Backend mailbox service is not active.'
            : 'Kiểm tra kết nối IMAP đang tạm hoãn (503 INTEGRATION_DEFERRED). Dịch vụ hòm thư backend chưa được kích hoạt.'
        })
      } else {
        setNotification({
          type: 'error',
          message: (data && data.message) || (isEn ? 'Failed to connect to IMAP server.' : 'Không thể kết nối máy chủ IMAP.')
        })
      }
    } catch (err: any) {
      setNotification({
        type: 'error',
        message: err?.message || (isEn ? 'Network error connecting to IMAP.' : 'Lỗi kết nối tới máy chủ IMAP.')
      })
    } finally {
      setIsTesting(false)
    }
  }

  // Trigger Real IMAP Email Scan - neutralized under deferred integration
  const handleTriggerScan = async () => {
    if (cleanupError) return
    if (INTEGRATION_DEFERRED) {
      setNotification({
        type: 'warning',
        message: isEn
          ? 'Mailbox scanning is currently deferred (503 INTEGRATION_DEFERRED) by project specification.'
          : 'Tính năng quét hòm thư tự động đang tạm hoãn (503 INTEGRATION_DEFERRED) theo yêu cầu quản lý dự án.'
      })
      return
    }
    setIsScanning(true)
    setNotification(null)
    try {
      const { appPassword: _omitted, ...sanitized } = settings
      const res = await fetch('/api/email/scan', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(sanitized)
      })
      const data = await res.json().catch(() => null)

      if (res.status === 503 || (data && data.code === 'INTEGRATION_DEFERRED') || INTEGRATION_DEFERRED) {
        setNotification({
          type: 'warning',
          message: isEn
            ? 'Mailbox scanning is currently deferred (503 INTEGRATION_DEFERRED) by project specification.'
            : 'Tính năng quét hòm thư tự động đang tạm hoãn (503 INTEGRATION_DEFERRED) theo yêu cầu quản lý dự án.'
        })
        return
      }

      // Neutralize legacy auto-registration completely during deferred status.
      // Under NO circumstances do we auto-create documents or assign synthetic numbers like CV-DEN-2026-AUTO.
      if (res.ok && data?.success) {
        setNotification({
          type: 'warning',
          message: isEn
            ? 'Mailbox integration is deferred (INTEGRATION_DEFERRED). Automated intake and document creation are disabled.'
            : 'Tính năng hòm thư đang tạm hoãn (INTEGRATION_DEFERRED). Tự động tiếp nhận và tạo công văn đã bị khóa.'
        })
      } else {
        setNotification({
          type: 'error',
          message: (data && data.message) || (isEn ? 'Error scanning email mailbox.' : 'Lỗi khi quét hòm thư hoặc tính năng đang tạm hoãn.')
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

  // Xác nhận tiếp nhận email không có PDF: Đã khóa trong thời gian hoãn tích hợp (J01/E1)
  const handleConfirmEmailToIntake = async () => {
    setNotification({
      type: 'warning',
      message: isEn
        ? 'Email intake is deferred (INTEGRATION_DEFERRED). Unverified legacy logs cannot be registered as documents.'
        : 'Tính năng tiếp nhận email đang tạm hoãn (INTEGRATION_DEFERRED). Dữ liệu cũ chưa đối soát không thể đăng ký thành công văn.'
    })
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
              disabled={isScanning || Boolean(cleanupError) || INTEGRATION_DEFERRED}
              startIcon={isScanning ? <CircularProgress size={18} color='inherit' /> : <i className='tabler-scan-eye' />}
              onClick={handleTriggerScan}
            >
              {isScanning ? t.email.scanningNow : t.email.scanNow}
            </Button>
          </div>
        </div>
      </Grid>

      {cleanupError && (
        <Grid size={{ xs: 12 }}>
          <Alert severity='error' icon={<i className='tabler-alert-triangle' />}>
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
        <Alert severity='warning' icon={<i className='tabler-alert-triangle' />}>
          <Typography variant='subtitle2' className='font-semibold'>
            {isEn ? 'Status: Integration Deferred (503 INTEGRATION_DEFERRED)' : 'Trạng Thái: Hoãn Tích Hợp (503 INTEGRATION_DEFERRED)'}
          </Typography>
          <Typography variant='body2'>
            {isEn
              ? 'Automatic email ingestion and OCR automation are deferred pending backend credential authority and vault integration. Secrets and passwords must not be stored in browser storage.'
              : 'Tính năng tiếp nhận công văn qua Email / IMAP và bóc tách OCR đang tạm hoãn theo yêu cầu quản lý dự án. Việc cấu hình mật khẩu và kết nối hòm thư sẽ được chuyển sang dịch vụ backend sau khi có phê duyệt. Mật khẩu không được phép lưu trữ trên trình duyệt.'}
          </Typography>
        </Alert>
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
              <Typography variant='h6' className='font-semibold'>{settings.email || '—'}</Typography>
              <Chip label={isEn ? 'Deferred / Not Connected' : 'Tạm hoãn / Chưa kết nối'} size='small' color='default' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-mail text-3xl text-primary opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-warning'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{isEn ? 'Automated Scanning' : 'Tự động quét'}</Typography>
              <Typography variant='h6' className='font-semibold'>
                {isEn ? 'Deferred' : 'Tạm hoãn'}
              </Typography>
              <Chip
                label={isEn ? 'Integration Deferred' : 'Tạm hoãn tích hợp'}
                size='small'
                color='warning'
                variant='tonal'
                className='mbs-1'
              />
            </div>
            <i className='tabler-clock-pause text-3xl text-warning opacity-80' />
          </CardContent>
        </Card>
      </Grid>

      <Grid size={{ xs: 12, md: 4 }}>
        <Card className='border-l-4 border-l-info'>
          <CardContent className='flex items-center justify-between'>
            <div>
              <Typography variant='body2' color='text.secondary'>{isEn ? 'Local History (Unverified)' : 'Nhật ký cục bộ (Chưa đối soát)'}</Typography>
              <Typography variant='h6' className='font-semibold'>{logs.length} {isEn ? 'entries' : 'mục'}</Typography>
              <Chip label={isEn ? 'OCR Deferred' : 'OCR tạm hoãn'} size='small' color='default' variant='tonal' className='mbs-1' />
            </div>
            <i className='tabler-file-text text-3xl text-info opacity-80' />
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
                      label={t.email.appPassword}
                      placeholder={isEn ? '[Credential management deferred to backend]' : '[Quản lý mật khẩu đã chuyển sang backend]'}
                      value=''
                      disabled
                      helperText={isEn ? 'Password input disabled during integration deferral (security policy)' : 'Khóa nhập mật khẩu trong thời gian hoãn tích hợp để đảm bảo an toàn'}
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
                      disabled={isTesting || Boolean(cleanupError) || INTEGRATION_DEFERRED}
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
                              <Tooltip title={isEn ? 'Deferred (Unverified legacy logs cannot be registered as documents)' : 'Tạm hoãn (Dữ liệu cũ chưa đối soát, không thể tạo công văn)'}>
                                <span>
                                  <Button
                                    size='small'
                                    variant='tonal'
                                    color='warning'
                                    disabled
                                    startIcon={<i className='tabler-lock text-xs' />}
                                    sx={{ textTransform: 'none', py: 0.5, px: 2 }}
                                  >
                                    {isEn ? 'Deferred' : 'Tạm hoãn'}
                                  </Button>
                                </span>
                              </Tooltip>
                            ) : (
                              <Chip
                                label={isEn ? 'Legacy Log' : 'Dữ liệu cũ'}
                                size='small'
                                color='default'
                                variant='outlined'
                                icon={<i className='tabler-history text-xs' />}
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
