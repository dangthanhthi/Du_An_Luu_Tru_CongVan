'use client'
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { Alert, Button, Card, CardContent, Checkbox, CircularProgress, FormControlLabel, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { incompleteReportsApi, type IncompleteReport as Report, type ReportFilter } from '@/services/das/reports-staff'
import { ApiRequestError } from '@/services/api'

export default function IncompleteReport() {
  const params = useParams(), lang = typeof params.lang === 'string' ? params.lang : 'vi'
  const [filter, setFilter] = useState<ReportFilter>({ pageNumber: 1, pageSize: 20, includeRecent: false })
  const [data, setData] = useState<Report>(), [error, setError] = useState(''), [loading, setLoading] = useState(false), [exporting, setExporting] = useState(false), [reload, setReload] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setData(undefined); setError(''); setLoading(true)
    incompleteReportsApi.list(filter, controller.signal).then(result => { if (!controller.signal.aborted) setData(result) }).catch(e => { if (!controller.signal.aborted) setError(e instanceof ApiRequestError && e.status === 503 ? 'Báo cáo đang chờ kết nối quyền hoặc dịch vụ xác minh PDF. Vui lòng thử lại sau.' : e.message || 'Không thể tải báo cáo.') }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [filter, reload])
  const download = async () => {
    setExporting(true); setError('')
    try { const blob = await incompleteReportsApi.export(filter), url = URL.createObjectURL(blob), link = document.createElement('a'); link.href = url; link.download = 'DAS-incomplete.xlsx'; link.click(); setTimeout(() => URL.revokeObjectURL(url), 1000) }
    catch (e) { setError(e instanceof Error ? e.message : 'Không thể tải báo cáo.') } finally { setExporting(false) }
  }
  return <Card><CardContent><Stack spacing={3}>
    <Typography variant='h4'>Báo cáo hồ sơ chưa hoàn tất</Typography>
    <Typography>Công văn đi/nội bộ chưa đủ trạng thái Distributed, PDF hiện hành và ngày phát hành. Mặc định chỉ lấy công văn đăng ký quá 14 ngày.</Typography>
    <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap>
      <TextField select label='Loại công văn' value={filter.kind ?? ''} onChange={e => setFilter(f => ({ ...f, kind: e.target.value ? e.target.value as 'Outgoing' | 'Internal' : undefined, pageNumber: 1 }))} sx={{ minWidth: 180 }}><MenuItem value=''>Tất cả</MenuItem><MenuItem value='Outgoing'>Công văn đi</MenuItem><MenuItem value='Internal'>Nội bộ</MenuItem></TextField>
      <FormControlLabel control={<Checkbox checked={filter.includeRecent ?? false} onChange={(_, checked) => setFilter(f => ({ ...f, includeRecent: checked, pageNumber: 1 }))} />} label='Bao gồm công văn trong 14 ngày gần đây' />
      <Button onClick={() => setReload(n => n + 1)} disabled={loading}>Tải lại</Button>
      {data?.canExport && <Button variant='contained' onClick={download} disabled={loading || exporting}>{exporting ? 'Đang tải…' : 'Tải Excel'}</Button>}
    </Stack>
    {error && <Alert severity='warning'>{error}</Alert>}{loading && <CircularProgress aria-label='Đang tải báo cáo' />}
    {data && <>
      <Typography>{data.total} hồ sơ • Kiểm tra lúc {new Date(data.evaluatedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</Typography>
      <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap>{data.groups.map(g => <Button key={g.departmentId} variant={filter.departmentId === g.departmentId ? 'contained' : 'outlined'} onClick={() => setFilter(f => ({ ...f, departmentId: f.departmentId === g.departmentId ? undefined : g.departmentId, pageNumber: 1 }))}>{g.departmentName}: {g.count}</Button>)}{filter.departmentId && <Button onClick={() => setFilter(f => ({ ...f, departmentId: undefined, pageNumber: 1 }))}>Tất cả phòng</Button>}</Stack>
      <div className='overflow-x-auto'><Table size='small'><TableHead><TableRow>{['Phòng', 'PDF', 'Số công văn', 'Ngày đăng ký', 'Ngày phát hành', 'Originator (ID)', 'Trạng thái', 'Nơi nhận'].map(v => <TableCell key={v}>{v}</TableCell>)}</TableRow></TableHead><TableBody>{data.items.map(r => <TableRow key={r.documentId}><TableCell>{r.departmentName}</TableCell><TableCell>{r.hasAttachment ? 'Có' : 'Thiếu'}</TableCell><TableCell><Link href={`/${lang}/apps/documents/${r.documentId}`}>{r.registrationNumber}</Link></TableCell><TableCell>{r.registeredDate}</TableCell><TableCell>{r.issueDate ?? '—'}</TableCell><TableCell>{r.originator}</TableCell><TableCell>{r.status}</TableCell><TableCell>{r.recipientList.join('; ') || '—'}</TableCell></TableRow>)}</TableBody></Table></div>
      {data.items.length === 0 && <Typography>Không có hồ sơ phù hợp trong phạm vi được cấp.</Typography>}
      <Typography variant='body2'>Quyền xem báo cáo không tự cấp quyền mở công văn hoặc tải PDF.</Typography>
      <Stack direction='row' spacing={2}><Button disabled={loading || (filter.pageNumber ?? 1) <= 1} onClick={() => setFilter(f => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}>Trước</Button><Typography>Trang {data.pageNumber}</Typography><Button disabled={loading || data.pageNumber * data.pageSize >= data.total} onClick={() => setFilter(f => ({ ...f, pageNumber: (f.pageNumber ?? 1) + 1 }))}>Sau</Button></Stack>
    </>}
  </Stack></CardContent></Card>
}
