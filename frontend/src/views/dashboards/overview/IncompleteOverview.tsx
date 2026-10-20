'use client'

import { useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { Alert, Box, Button, Card, CardContent, CardHeader, Chip, CircularProgress, Divider, Grid, LinearProgress, MenuItem, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material'
import CustomAvatar from '@core/components/mui/Avatar'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { incompleteReportsApi } from '@/services/das/reports-staff'
import { notificationsApi } from '@/services/das/notifications'
import type { DocumentKind, DocumentListItem } from '@/types/das/documents'
import { loadOverview } from './overview-data'
import type { DashboardScope, OverviewSections, Section } from './overview-data'

const kinds: DocumentKind[] = ['Incoming', 'Outgoing', 'Internal']
const colors = { Incoming: 'info', Outgoing: 'primary', Internal: 'success' } as const
const icons = { Incoming: 'tabler-arrow-down-left', Outgoing: 'tabler-arrow-up-right', Internal: 'tabler-file-description' }
const value = <T,>(section?: Section<T>) => section?.state === 'ready' ? section.data : undefined

export default function IncompleteOverview() {
  const params = useParams(), lang = params.lang === 'en' ? 'en' : 'vi', isEn = lang === 'en'
  const sessionIntent = useSessionIntent()
  const [scope, setScope] = useState<DashboardScope>('all'), [kind, setKind] = useState<DocumentKind>('Incoming')
  const [revision, setRevision] = useState(0)
  const key = `${lang}:${scope}:${revision}:${sessionIntent.epoch}`
  const keyRef = useRef(key); keyRef.current = key
  const [result, setResult] = useState<{ key: string; sections: OverviewSections }>({ key: '', sections: {} })
  const [actionError, setActionError] = useState(''), [exporting, setExporting] = useState(false), [reading, setReading] = useState(false)
  const exportRef = useRef<AbortController | null>(null), actionRef = useRef(false), lifecycle = useRef(new AbortController())
  const exportUrl = useRef<string | null>(null)
  const sections = result.key === key ? result.sections : {}
  const tr = (vi: string, en: string) => isEn ? en : vi
  const label = (type: DocumentKind) => ({ Incoming: tr('Công văn đến', 'Incoming'), Outgoing: tr('Công văn đi', 'Outgoing'), Internal: tr('Công văn nội bộ', 'Internal') })[type]
  const count = (n?: number) => n === undefined ? '—' : n.toLocaleString(isEn ? 'en-US' : 'vi-VN')
  const date = (s?: string | null, time = false) => !s || Number.isNaN(Date.parse(s)) ? '—' : new Date(s).toLocaleString(isEn ? 'en-US' : 'vi-VN', time
    ? { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short', timeStyle: 'short' } : { timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'short' })
  const listUrl = (type: DocumentKind) => `/${lang}/apps/documents/list?kind=${type}&view=${scope}&pageNumber=1`
  const errorText = (status: number) => status === 401 ? tr('Phiên đã hết hạn. Vui lòng đăng nhập lại.', 'Your session has expired. Please sign in again.')
    : status === 403 ? tr('Bạn không có quyền xem dữ liệu này.', 'You do not have permission to view this data.')
    : status === 503 ? tr('Dịch vụ tạm thời chưa khả dụng.', 'Service temporarily unavailable.')
    : status === 409 ? tr('Phiên hoặc phạm vi dữ liệu đã đổi. Vui lòng tải lại.', 'Session or data scope changed. Please reload.')
    : tr('Không thể tải dữ liệu. Vui lòng thử lại.', 'Unable to load data. Please try again.')
  const state = (section?: Section<unknown>) => !section ? <CircularProgress size={20} aria-label={tr('Đang tải số liệu', 'Loading data')} />
    : section.state === 'error' ? <Alert severity={section.status === 403 ? 'info' : 'warning'}>{errorText(section.status)}</Alert> : null

  useEffect(() => {
    const controller = new AbortController(); lifecycle.current = controller
    exportRef.current?.abort(); actionRef.current = false
    setActionError(''); setExporting(false); setReading(false)
    try {
      sessionIntent.assertCurrent()
      void loadOverview(scope, controller.signal, (name, section) => {
        if (controller.signal.aborted || keyRef.current !== key) return
        try { sessionIntent.assertCurrent() } catch { return }
        setResult(previous => ({ key, sections: { ...(previous.key === key ? previous.sections : {}), [name]: section } }))
      })
    } catch { setResult({ key, sections: Object.fromEntries(['Incoming', 'Outgoing', 'Internal', 'report', 'notifications', 'staff'].map(name => [name, { state: 'error', status: 401 }])) }) }
    return () => { controller.abort(); exportRef.current?.abort(); if (exportUrl.current) { URL.revokeObjectURL(exportUrl.current); exportUrl.current = null } }
  }, [key, scope])
  useEffect(() => {
    if (typeof window === 'undefined') return
    const refresh = () => setRevision(n => n + 1)
    window.addEventListener('das_documents_updated', refresh); window.addEventListener('das_notifications_updated', refresh)
    return () => { window.removeEventListener('das_documents_updated', refresh); window.removeEventListener('das_notifications_updated', refresh) }
  }, [])

  const report = value(sections.report), inbox = value(sections.notifications), staff = value(sections.staff), selected = value(sections[kind])
  const pages = kinds.map(k => value(sections[k])?.page)
  const total = pages.every(Boolean) ? pages.reduce((n, p) => n + p!.totalCount, 0) : undefined
  const loading = Object.keys(sections).length < 6
  const totalKnown = total !== undefined && Number.isSafeInteger(total) ? total : undefined

  async function download() {
    if (!report?.canExport || exportRef.current && !exportRef.current.signal.aborted || exporting) return
    const controller = new AbortController(); exportRef.current = controller; setExporting(true); setActionError('')
    try {
      sessionIntent.assertCurrent()
      const blob = await incompleteReportsApi.export({ includeRecent: false }, controller.signal)
      if (controller.signal.aborted || lifecycle.current.signal.aborted || keyRef.current !== key) return
      sessionIntent.assertCurrent()
      if (exportUrl.current) URL.revokeObjectURL(exportUrl.current)
      const url = URL.createObjectURL(blob); exportUrl.current = url
      const link = document.createElement('a'); link.href = url; link.download = 'DAS-ho-so-chua-hoan-tat.xlsx'; link.click()
    } catch {
      if (!controller.signal.aborted && keyRef.current === key) setActionError(tr('Chưa tải được Excel. Vui lòng thử lại.', 'Unable to download Excel. Please try again.'))
    } finally {
      if (exportRef.current === controller) { exportRef.current = null; if (keyRef.current === key) setExporting(false) }
    }
  }
  async function markRead(id?: string) {
    if (actionRef.current || reading || !inbox || !inbox.unread) return
    actionRef.current = true; setReading(true); setActionError(''); const controller = lifecycle.current
    try {
      sessionIntent.assertCurrent()
      if (id) await notificationsApi.read(id, sessionIntent); else await notificationsApi.readAll(sessionIntent)
      sessionIntent.assertCurrent()
      if (controller.signal.aborted || keyRef.current !== key) return
      window.dispatchEvent(new Event('das_notifications_updated'))
    } catch {
      if (!controller.signal.aborted && keyRef.current === key) setActionError(tr('Chưa xác nhận được đã đọc thông báo. Vui lòng tải lại.', 'Unable to confirm notification update. Please reload.'))
    } finally {
      if (!controller.signal.aborted && keyRef.current === key) { actionRef.current = false; setReading(false) }
    }
  }
  const status = (item: DocumentListItem) => <Chip size='small' color={item.status === 'Cancelled' ? 'default' : item.status === 'Distributed' ? 'success' : 'warning'} label={item.status === 'Cancelled' ? tr('Đã hủy', 'Cancelled') : item.status === 'Distributed' ? tr('Đã phân phối', 'Distributed') : tr('Đang xử lý', 'In progress')} />
  const metric = (title: string, n: number | undefined, icon: string, color: 'primary' | 'info' | 'success' | 'warning', href?: string) => <Card sx={{ height: '100%' }}><CardContent>
    <Stack direction='row' spacing={3} alignItems='center'><CustomAvatar skin='light' color={color} variant='rounded' size={48}><i className={icon} aria-hidden /></CustomAvatar>
      <Box sx={{ minWidth: 0 }}><Typography variant='body2' color='text.secondary'>{title}</Typography><Typography variant='h3'>{count(n)}</Typography></Box>
    </Stack>{href && <Button component={Link} href={href} size='small' endIcon={<i className='tabler-arrow-right' aria-hidden />} sx={{ mt: 2 }}>{tr('Xem danh sách', 'View list')}</Button>}
  </CardContent></Card>

  return <Grid container spacing={6}>
    <Grid size={{ xs: 12 }}><Card><CardContent><Stack spacing={3} direction={{ xs: 'column', md: 'row' }} justifyContent='space-between' alignItems={{ md: 'center' }}>
      <Box><Typography variant='h4' component='h1'>{tr('Bảng điều khiển DAS', 'DAS dashboard')}</Typography><Typography color='text.secondary'>{tr('Theo dõi công văn và các việc cần xử lý trong phạm vi của bạn.', 'Track documents and work within your authorized scope.')}</Typography></Box>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}><TextField select size='small' label={tr('Phạm vi công văn', 'Document scope')} value={scope} onChange={e => setScope(e.target.value as DashboardScope)} sx={{ minWidth: 190 }}>
        <MenuItem value='all'>{tr('Tất cả được phép xem', 'All accessible')}</MenuItem><MenuItem value='mine'>{tr('Công văn của tôi', 'My documents')}</MenuItem><MenuItem value='department'>{tr('Công văn của phòng', 'Department documents')}</MenuItem>
      </TextField><Button variant='outlined' disabled={exporting || reading} onClick={() => setRevision(n => n + 1)} startIcon={loading ? <CircularProgress size={16} aria-label={tr('Đang tải tổng quan', 'Loading overview')} /> : <i className='tabler-refresh' aria-hidden />}>{tr('Tải lại tổng quan', 'Reload overview')}</Button></Stack>
    </Stack></CardContent></Card></Grid>
    {actionError && <Grid size={{ xs: 12 }}><Alert severity='warning' onClose={() => setActionError('')}>{actionError}</Alert></Grid>}
    <Grid size={{ xs: 12, sm: 6, lg: 4, xl: 2 }}>{metric(tr('Tổng công văn', 'Total documents'), totalKnown, 'tabler-files', 'primary')}</Grid>
    {kinds.map(type => <Grid key={type} size={{ xs: 12, sm: 6, lg: 4, xl: 2 }}>{metric(label(type), value(sections[type])?.page.totalCount, icons[type], colors[type], listUrl(type))}</Grid>)}
    <Grid size={{ xs: 12, sm: 6, lg: 4, xl: 2 }}>{metric(tr('Hồ sơ quá 7 ngày', 'Overdue dossiers'), report?.total, 'tabler-file-alert', 'warning', `/${lang}/apps/reports/incomplete`)}</Grid>
    <Grid size={{ xs: 12, sm: 6, lg: 4, xl: 2 }}>{metric(tr('Thông báo chưa đọc', 'Unread notifications'), inbox?.unread, 'tabler-bell', 'info')}</Grid>
    <Grid size={{ xs: 12, lg: 8 }}><Card sx={{ height: '100%' }}><CardHeader title={tr('Công văn đăng ký gần đây', 'Recently registered documents')} subheader={tr('Tối đa 6 công văn mỗi loại theo phạm vi đang chọn.', 'Up to 6 documents per kind within the selected scope.')} />
      <Tabs value={kind} onChange={(_, next: DocumentKind) => setKind(next)} variant='scrollable' scrollButtons='auto' aria-label={tr('Loại công văn', 'Document kind')} sx={{ px: 4 }}>{kinds.map(type => <Tab key={type} id={`dashboard-tab-${type}`} aria-controls={`dashboard-panel-${type}`} value={type} label={label(type)} />)}</Tabs><Divider />
      <CardContent role='tabpanel' id={`dashboard-panel-${kind}`} aria-labelledby={`dashboard-tab-${kind}`}>{state(sections[kind])}{selected && <>
        {selected.page.items.length === 0 ? <Alert severity='info'>{tr('Không có công văn trong phạm vi này.', 'No documents within this scope.')}</Alert> : <TableContainer sx={{ overflowX: 'auto' }}><Table size='small' sx={{ minWidth: 580 }} aria-label={tr('Công văn gần đây', 'Recent documents')}><TableHead><TableRow>
          {[tr('Số và trích yếu', 'Number and subject'), tr('Phòng', 'Department'), tr('Ngày đăng ký', 'Registered'), tr('Trạng thái', 'Status')].map(t => <TableCell key={t}>{t}</TableCell>)}
        </TableRow></TableHead><TableBody>{selected.page.items.map(item => <TableRow key={item.id} hover>
          <TableCell sx={{ maxWidth: 360, overflowWrap: 'anywhere' }}><Typography component={Link} href={`/${lang}/apps/documents/${item.id}`} color='primary.main' variant='body2'>{item.registrationNumber}</Typography><Typography variant='body2' sx={{ mt: 1, overflowWrap: 'anywhere' }}>{item.subject}</Typography>{item.sensitivity === 'Confidential' && <Chip size='small' label={tr('Mật', 'Confidential')} color='warning' sx={{ mt: 1 }} />}</TableCell>
          <TableCell sx={{ overflowWrap: 'anywhere' }}>{item.departmentName || '—'}</TableCell><TableCell>{date(item.registrationDate)}</TableCell><TableCell>{status(item)}</TableCell>
        </TableRow>)}</TableBody></Table></TableContainer>}
        <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap sx={{ mt: 4 }}><Button component={Link} href={listUrl(kind)} variant='outlined'>{tr('Xem tất cả', 'View all')} ({count(selected.page.totalCount)})</Button>{selected.canRegister && <Button component={Link} href={`/${lang}/apps/documents/add?kind=${kind}`} variant='contained' startIcon={<i className='tabler-plus' aria-hidden />}>{tr('Đăng ký mới', 'Register document')}</Button>}</Stack>
      </>}</CardContent>
    </Card></Grid>
    <Grid size={{ xs: 12, lg: 4 }}><Stack spacing={6}>
      <Card><CardHeader title={tr('Công văn theo loại', 'Documents by kind')} subheader={tr('Số liệu theo quyền xem công văn của bạn', 'Counts within your document read scope')} /><CardContent><Stack spacing={4}>
        {kinds.map(type => <Box key={type}><Stack direction='row' justifyContent='space-between' spacing={2}><Typography>{label(type)}</Typography><Typography fontWeight={600}>{count(value(sections[type])?.page.totalCount)}</Typography></Stack>
          {value(sections[type]) && totalKnown !== undefined ? <LinearProgress aria-label={label(type)} variant='determinate' value={totalKnown === 0 ? 0 : value(sections[type])!.page.totalCount / totalKnown * 100} color={colors[type]} sx={{ height: 8, borderRadius: 1, mt: 2 }} /> : state(sections[type])}
        </Box>)}{totalKnown === undefined && <Typography variant='body2' color='text.secondary'>{tr('Chỉ tính tổng khi cả ba loại có dữ liệu được xác minh.', 'Total is available after all three document kinds are verified.')}</Typography>}
      </Stack></CardContent></Card>
      <Card><CardHeader title={tr('Thao tác nhanh', 'Quick actions')} /><CardContent><Stack spacing={2}>
        {kinds.filter(type => value(sections[type])?.canRegister).map(type => <Button key={type} component={Link} href={`/${lang}/apps/documents/add?kind=${type}`} variant='tonal' startIcon={<i className={icons[type]} aria-hidden />}>{tr('Đăng ký', 'Register')} {label(type)}</Button>)}
        <Button component={Link} href={`/${lang}/apps/partners/list`} variant='outlined' startIcon={<i className='tabler-building' aria-hidden />}>{tr('Cơ quan và đối tác', 'External entities')}</Button><Button component={Link} href={`/${lang}/apps/settings/organization`} variant='outlined' startIcon={<i className='tabler-sitemap' aria-hidden />}>{tr('Tổ chức và nhân sự', 'Organization directory')}</Button>
      </Stack></CardContent></Card>
    </Stack></Grid>
    <Grid size={{ xs: 12, lg: 8 }}><Card><CardHeader title={tr('Tổng quan hồ sơ chưa hoàn tất', 'Incomplete document overview')} subheader={tr('Outgoing/Internal quá 7 ngày. Phạm vi báo cáo độc lập với bộ lọc công văn ở trên.', 'Outgoing/Internal older than 7 days. Report scope is independent of the document filter above.')} /><CardContent>
      {state(sections.report)}{report && <Stack spacing={3}>
        <Stack direction='row' alignItems='center' spacing={2} flexWrap='wrap' useFlexGap><Chip label={`${count(report.total)} ${tr('hồ sơ', 'dossiers')}`} color={report.total ? 'warning' : 'success'} /><Typography variant='body2' color='text.secondary'>{tr('Đánh giá lúc', 'Evaluated at')} {date(report.evaluatedAt, true)}</Typography></Stack>
        {report.groups.map(group => <Stack key={group.departmentId} direction='row' justifyContent='space-between' spacing={2}><Typography sx={{ overflowWrap: 'anywhere' }}>{group.departmentName}</Typography><Chip size='small' label={count(group.count)} /></Stack>)}
        {report.total === 0 && <Alert severity='success'>{tr('Không có hồ sơ quá 7 ngày chưa hoàn tất trong phạm vi báo cáo.', 'No overdue incomplete dossiers within your report scope.')}</Alert>}
        {report.items.map(item => <Box key={item.documentId} sx={{ borderBottom: 1, borderColor: 'divider', pb: 2 }}><Typography component={Link} href={`/${lang}/apps/documents/${item.documentId}`} color='primary.main'>{item.registrationNumber}</Typography><Typography variant='body2' color='text.secondary' sx={{ overflowWrap: 'anywhere' }}>{item.departmentName} · {date(item.registeredDate)} · {item.hasAttachment ? tr('Có PDF', 'PDF attached') : tr('Thiếu PDF', 'Missing PDF')} · {item.issueDate ? tr('Có ngày phát hành', 'Issue date set') : tr('Thiếu ngày phát hành', 'Missing issue date')}</Typography></Box>)}
        <Typography variant='body2' color='text.secondary'>{tr('Quyền mở chi tiết và PDF được kiểm tra riêng.', 'Opening document details and PDFs requires separate authorization.')}</Typography>
        <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap><Button component={Link} href={`/${lang}/apps/reports/incomplete`} variant='outlined'>{tr('Xem báo cáo chi tiết', 'View detailed report')}</Button>{report.canExport && <Button onClick={download} disabled={exporting} variant='contained' startIcon={<i className='tabler-file-spreadsheet' aria-hidden />}>{exporting ? tr('Đang xuất…', 'Exporting…') : tr('Xuất Excel', 'Export Excel')}</Button>}</Stack>
      </Stack>}
    </CardContent></Card></Grid>
    <Grid size={{ xs: 12, lg: 4 }}><Stack spacing={6}>
      <Card><CardHeader title={tr('Thông báo gần đây', 'Recent notifications')} /><CardContent>{state(sections.notifications)}{inbox && <Stack spacing={3}>
        <Typography color='text.secondary' variant='body2'>{tr('5 thông báo mới nhất', 'Latest 5 notifications')} · {count(inbox.totalCount)} {tr('tổng cộng', 'total')}</Typography>{inbox.items.length === 0 && <Typography>{tr('Chưa có thông báo.', 'No notifications yet.')}</Typography>}
        {inbox.items.map(item => <Box key={item.id} sx={{ borderBottom: 1, borderColor: 'divider', pb: 3, overflowWrap: 'anywhere' }}><Stack direction='row' spacing={2} alignItems='center'><Typography fontWeight={item.isRead ? 400 : 600}>{item.title}</Typography>{!item.isRead && <Chip size='small' color='primary' label={tr('Mới', 'New')} />}</Stack>
          <Typography variant='body2' color='text.secondary'>{item.message}</Typography><Typography variant='caption' color='text.secondary'>{date(item.createdAt, true)}</Typography><Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap sx={{ mt: 1 }}>{item.actionUrl && <Button size='small' component={Link} href={`/${lang}${item.actionUrl.replace(/^\/(vi|en)(?=\/|$)/, '')}`}>{tr('Mở', 'Open')}</Button>}{!item.isRead && <Button size='small' disabled={reading} onClick={() => markRead(item.id)}>{tr('Đánh dấu đã đọc', 'Mark read')}</Button>}</Stack>
        </Box>)}{inbox.unread > 0 && <Button variant='outlined' disabled={reading} onClick={() => markRead()}>{tr('Đọc tất cả', 'Mark all read')}</Button>}
      </Stack>}</CardContent></Card>
      <Card><CardHeader title='My Staff' subheader={tr('Nhân sự trong phạm vi quản lý', 'Staff within your managed scope')} /><CardContent>{state(sections.staff)}{staff && <Stack spacing={3}>
        <Typography variant='h3'>{count(staff.members.total)} <Typography component='span' color='text.secondary'>{tr('nhân sự', 'staff')}</Typography></Typography>
        {staff.tasks.state === 'ready' && staff.tasks.data.taskState === 'Connected' ? <Typography>{count(staff.tasks.data.tasks.total)} {tr('công việc từ TMS', 'tasks from TMS')}</Typography> : <Alert severity='info'>{tr('Chưa xác minh được công việc từ TMS. Số lượng công việc chưa được xác định.', 'TMS tasks are not verified. Task count is unknown.')}</Alert>}
        <Button component={Link} href={`/${lang}/apps/tasks/my-staff`} variant='outlined'>{tr('Xem My Staff', 'View My Staff')}</Button>
      </Stack>}</CardContent></Card>
    </Stack></Grid>
  </Grid>
}
