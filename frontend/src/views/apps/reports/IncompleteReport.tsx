'use client'
import { useEffect, useRef, useState } from 'react'
import Link from 'next/link'
import { useParams } from 'next/navigation'
import { Alert, Button, Card, CardContent, Checkbox, CircularProgress, FormControlLabel, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { useAppDictionary } from '@/hooks/useDictionary'
import { incompleteReportsApi, type IncompleteReport as Report, type ReportFilter } from '@/services/das/reports-staff'
import { ApiRequestError } from '@/services/api'

export default function IncompleteReport() {
  const { isEn } = useAppDictionary()
  const params = useParams(), lang = typeof params.lang === 'string' ? params.lang : 'vi'
  const [filter, setFilter] = useState<ReportFilter>({ pageNumber: 1, pageSize: 20, includeRecent: false })
  const [data, setData] = useState<Report>()
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [exporting, setExporting] = useState(false)
  const [reload, setReload] = useState(0)
  const exportControllerRef = useRef<AbortController | null>(null)

  useEffect(() => {
    return () => {
      exportControllerRef.current?.abort()
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    setData(undefined)
    setError('')
    setLoading(true)

    incompleteReportsApi.list(filter, controller.signal)
      .then(result => {
        if (!controller.signal.aborted) setData(result)
      })
      .catch(e => {
        if (!controller.signal.aborted) {
          if (e instanceof ApiRequestError) {
            if (e.status === 401) setError(isEn ? 'Please sign in again to view the report.' : 'Vui lòng đăng nhập lại để xem báo cáo.')
            else if (e.status === 403) setError(isEn ? 'You do not have permission to access this report.' : 'Bạn không có quyền truy cập báo cáo này.')
            else if (e.status === 409) setError(isEn ? 'Report criteria or session changed. Please reload.' : 'Dữ liệu hoặc phiên làm việc đã thay đổi. Vui lòng tải lại báo cáo.')
            else if (e.status === 422) setError(isEn ? 'Filter scope is too broad. Please narrow down the filters.' : 'Bộ lọc quá rộng. Vui lòng chọn phạm vi cụ thể hơn.')
            else if (e.status === 503) setError(isEn ? 'Report service or PDF verification service is temporarily unavailable. Please retry later.' : 'Báo cáo đang chờ kết nối quyền hoặc dịch vụ xác minh PDF. Vui lòng thử lại sau.')
            else if (e.status === 502) setError(isEn ? 'Invalid report data received from server.' : 'Phản hồi từ máy chủ không hợp lệ.')
            else if (e.status === 0) setError(isEn ? 'Cannot connect to report service. Please check your network and retry.' : 'Không thể kết nối máy chủ. Kiểm tra kết nối mạng và thử lại.')
            else setError(e.message || (isEn ? 'Failed to load report.' : 'Không thể tải báo cáo.'))
          } else {
            setError(e instanceof Error ? e.message : (isEn ? 'Failed to load report.' : 'Không thể tải báo cáo.'))
          }
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })

    return () => controller.abort()
  }, [filter, reload, isEn])

  const download = async () => {
    const exportFilter = { ...filter }
    const controller = new AbortController()
    exportControllerRef.current = controller
    setExporting(true)
    setError('')

    try {
      const blob = await incompleteReportsApi.export(exportFilter, controller.signal)
      if (controller.signal.aborted) return

      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = 'DAS-incomplete.xlsx'
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (e) {
      if (!controller.signal.aborted) {
        if (e instanceof ApiRequestError && e.status === 403) {
          setError(isEn ? 'You do not have permission to export this report.' : 'Bạn không có quyền xuất báo cáo này.')
        } else if (e instanceof ApiRequestError && e.status === 409) {
          setError(isEn ? 'Report scope changed during export. Please reload and try again.' : 'Dữ liệu thay đổi trong khi xuất. Vui lòng thử lại.')
        } else {
          setError(e instanceof Error ? e.message : (isEn ? 'Failed to export report.' : 'Không thể tải báo cáo.'))
        }
      }
    } finally {
      if (!controller.signal.aborted) {
        setExporting(false)
      }
    }
  }

  const cancelExport = () => {
    exportControllerRef.current?.abort()
    setExporting(false)
  }

  return (
    <Card>
      <CardContent>
        <Stack spacing={3}>
          <Typography variant='h4'>
            {isEn ? 'Incomplete Document Dossiers Report' : 'Báo cáo hồ sơ chưa hoàn tất'}
          </Typography>
          <Typography color='text.secondary'>
            {isEn
              ? 'Outgoing and Internal documents missing Distributed status, current verified PDF attachment, or issued date. Defaults to documents registered more than 7 days ago.'
              : 'Công văn đi/nội bộ chưa đủ trạng thái Distributed, PDF hiện hành và ngày phát hành. Mặc định chỉ lấy công văn đăng ký quá 7 ngày.'}
          </Typography>
          <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap alignItems='center'>
            <TextField
              select
              label={isEn ? 'Document Kind' : 'Loại công văn'}
              value={filter.kind ?? ''}
              onChange={e => setFilter(f => ({ ...f, kind: e.target.value ? e.target.value as 'Outgoing' | 'Internal' : undefined, pageNumber: 1 }))}
              sx={{ minWidth: 180 }}
            >
              <MenuItem value=''>{isEn ? 'All Kinds' : 'Tất cả'}</MenuItem>
              <MenuItem value='Outgoing'>{isEn ? 'Outgoing Document' : 'Công văn đi'}</MenuItem>
              <MenuItem value='Internal'>{isEn ? 'Internal Document' : 'Nội bộ'}</MenuItem>
            </TextField>
            <FormControlLabel
              control={
                <Checkbox
                  checked={filter.includeRecent ?? false}
                  onChange={(_, checked) => setFilter(f => ({ ...f, includeRecent: checked, pageNumber: 1 }))}
                />
              }
              label={isEn ? 'Include documents within the last 7 days' : 'Bao gồm công văn trong 7 ngày gần đây'}
            />
            <Button onClick={() => setReload(n => n + 1)} disabled={loading}>
              {isEn ? 'Reload' : 'Tải lại'}
            </Button>
            {data?.canExport && (
              <Stack direction='row' spacing={1} alignItems='center'>
                <Button variant='contained' onClick={download} disabled={loading || exporting}>
                  {exporting ? (isEn ? 'Exporting…' : 'Đang tải…') : (isEn ? 'Export Excel' : 'Tải Excel')}
                </Button>
                {exporting && (
                  <Button size='small' color='secondary' onClick={cancelExport}>
                    {isEn ? 'Cancel' : 'Hủy'}
                  </Button>
                )}
              </Stack>
            )}
          </Stack>

          {error && <Alert severity='warning' onClose={() => setError('')}>{error}</Alert>}
          {loading && <CircularProgress aria-label={isEn ? 'Loading report' : 'Đang tải báo cáo'} />}

          {data && (
            <>
              <Typography>
                {data.total} {isEn ? 'dossiers • Evaluated at' : 'hồ sơ • Kiểm tra lúc'}{' '}
                {new Date(data.evaluatedAt).toLocaleString(isEn ? 'en-US' : 'vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}
              </Typography>
              <Stack direction='row' spacing={2} flexWrap='wrap' useFlexGap>
                {data.groups.map(g => (
                  <Button
                    key={g.departmentId}
                    variant={filter.departmentId === g.departmentId ? 'contained' : 'outlined'}
                    onClick={() => setFilter(f => ({ ...f, departmentId: f.departmentId === g.departmentId ? undefined : g.departmentId, pageNumber: 1 }))}
                  >
                    {g.departmentName}: {g.count}
                  </Button>
                ))}
                {filter.departmentId && (
                  <Button onClick={() => setFilter(f => ({ ...f, departmentId: undefined, pageNumber: 1 }))}>
                    {isEn ? 'All departments' : 'Tất cả phòng'}
                  </Button>
                )}
              </Stack>
              <div className='overflow-x-auto'>
                <Table size='small'>
                  <TableHead>
                    <TableRow>
                      {[
                        isEn ? 'Department' : 'Phòng',
                        'PDF',
                        isEn ? 'Document No.' : 'Số công văn',
                        isEn ? 'Registration Date' : 'Ngày đăng ký',
                        isEn ? 'Issued Date' : 'Ngày phát hành',
                        'Originator (ID)',
                        isEn ? 'Status' : 'Trạng thái',
                        isEn ? 'Recipients' : 'Nơi nhận'
                      ].map(v => <TableCell key={v}>{v}</TableCell>)}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.items.map(r => (
                      <TableRow key={r.documentId}>
                        <TableCell>{r.departmentName}</TableCell>
                        <TableCell>{r.hasAttachment ? (isEn ? 'Yes' : 'Có') : (isEn ? 'Missing' : 'Thiếu')}</TableCell>
                        <TableCell>
                          <Link href={`/${lang}/apps/documents/${r.documentId}`}>{r.registrationNumber}</Link>
                        </TableCell>
                        <TableCell>{r.registeredDate}</TableCell>
                        <TableCell>{r.issueDate ?? '—'}</TableCell>
                        <TableCell>{r.originator}</TableCell>
                        <TableCell>{r.status}</TableCell>
                        <TableCell>{r.recipientList.join('; ') || '—'}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              {data.items.length === 0 && (
                <Typography color='text.secondary'>
                  {isEn ? 'No matching dossiers in authorized scope.' : 'Không có hồ sơ phù hợp trong phạm vi được cấp.'}
                </Typography>
              )}
              <Typography variant='body2' color='text.secondary'>
                {isEn
                  ? 'Permission to view this report does not grant access to open confidential documents or download PDFs.'
                  : 'Quyền xem báo cáo không tự cấp quyền mở công văn hoặc tải PDF.'}
              </Typography>
              <Stack direction='row' spacing={2} alignItems='center'>
                <Button
                  disabled={loading || (filter.pageNumber ?? 1) <= 1}
                  onClick={() => setFilter(f => ({ ...f, pageNumber: (f.pageNumber ?? 1) - 1 }))}
                >
                  {isEn ? 'Previous' : 'Trước'}
                </Button>
                <Typography>{isEn ? `Page ${data.pageNumber}` : `Trang ${data.pageNumber}`}</Typography>
                <Button
                  disabled={loading || data.pageNumber * data.pageSize >= data.total}
                  onClick={() => setFilter(f => ({ ...f, pageNumber: (f.pageNumber ?? 1) + 1 }))}
                >
                  {isEn ? 'Next' : 'Sau'}
                </Button>
              </Stack>
            </>
          )}
        </Stack>
      </CardContent>
    </Card>
  )
}
