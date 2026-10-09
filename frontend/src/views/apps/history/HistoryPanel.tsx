'use client'

import { useEffect, useState } from 'react'
import { Alert, Box, Button, Card, CardContent, CircularProgress, TablePagination, Typography } from '@mui/material'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { ApiRequestError } from '@/services/api'
import { historyApi, historyGuid } from '@/services/das/history'
import type { PartnerAuditPage, DocumentLifecyclePage, DocumentLifecycleEvent } from '@/types/das/history'

export default function HistoryPanel({ kind, id, refreshKey = 0 }: { kind: 'partner' | 'document'; id: string; refreshKey?: string | number }) {
  const intent = useSessionIntent(), { isEn } = useAppDictionary()
  const l = (vi: string, en: string) => isEn ? en : vi
  const owned = () => { try { intent.assertCurrent(); return true } catch { return false } }
  const scope = JSON.stringify([kind, id, intent.epoch, refreshKey])
  const [revision, setRevision] = useState(0)
  const [cursor, setCursor] = useState<{ scope: string; pageNumber: number; pageSize: number; throughVersion?: string }>({ scope, pageNumber: 1, pageSize: 20 })
  const query = cursor.scope === scope ? cursor : { scope, pageNumber: 1, pageSize: cursor.pageSize }
  const key = JSON.stringify([scope, revision, query.pageNumber, query.pageSize, query.throughVersion])
  const [result, setResult] = useState<{ key: string; data?: PartnerAuditPage | DocumentLifecyclePage; status?: number }>({ key: '' })
  const current = result.key === key ? result : undefined

  useEffect(() => {
    if (!historyGuid(id) || !owned()) return
    const controller = new AbortController()
    historyApi[kind](id, { pageNumber: query.pageNumber, pageSize: query.pageSize, throughVersion: query.throughVersion }, controller.signal, intent)
      .then(data => { if (!controller.signal.aborted && owned()) setResult({ key, data }) })
      .catch(error => { if (!controller.signal.aborted && owned()) setResult({ key, status: error instanceof ApiRequestError ? error.status : 0 }) })
    return () => controller.abort()
  }, [key, id, kind, query.pageNumber, query.pageSize, query.throughVersion])

  const reload = () => {
    if (!owned()) return
    setCursor({ scope, pageNumber: 1, pageSize: query.pageSize }); setRevision(x => x + 1)
  }
  const error = (status?: number) => status === 401 ? l('Phiên đã hết hạn. Vui lòng đăng nhập lại.', 'Session expired. Please sign in again.') :
    status === 403 ? l('Bạn không có quyền xem lịch sử này.', 'You do not have permission to view this history.') :
      status === 404 ? l('Không tìm thấy bản ghi hoặc bản ghi ngoài phạm vi được cấp.', 'Record not found or outside your authorized scope.') :
        status === 503 ? l('Lịch sử tạm thời chưa sẵn sàng hoặc chưa thể xác minh. Thử lại sau.', 'History is temporarily unavailable or cannot be verified. Please retry later.') :
          l('Không thể tải lịch sử. Vui lòng thử lại.', 'Cannot load history. Please retry.')
  const action = (value: string) => ({ Create: l('Đã tạo', 'Created'), Update: l('Đã sửa', 'Updated'), Delete: l('Đã xóa mềm', 'Soft deleted'),
    Restore: l('Đã khôi phục', 'Restored'), Distribute: l('Đã phân phối', 'Distributed'), Cancel: l('Đã hủy', 'Cancelled') })[value] ?? value
  const status = (value: string) => ({ InProgress: l('Đang thực hiện', 'In progress'), Distributed: l('Đã phân phối', 'Distributed'), Cancelled: l('Đã hủy', 'Cancelled') })[value] ?? value

  if (!owned()) return <Alert severity='warning'>{l('Phiên đã thay đổi. Vui lòng đăng nhập lại.', 'Session changed. Please sign in again.')}</Alert>
  if (!historyGuid(id)) return <Alert severity='error'>{l('Định danh không hợp lệ.', 'Invalid record ID.')}</Alert>

  return <Card><CardContent>
    <Typography variant='h5' component='h2'>{kind === 'document' ? l('Lịch sử trạng thái công văn', 'Lifecycle history') : l('Lịch sử đối tác', 'Partner history')}</Typography>
    <Typography color='text.secondary' className='mbs-2 mbe-3'>
      {kind === 'document' ? l('Chỉ gồm phân phối, hủy và khôi phục V2; chưa gồm thay đổi trường, PDF và lịch sử cũ.', 'V2 distribution, cancellation and restoration only; field, PDF and legacy history are excluded.') :
        l('Dữ liệu hiện có không lưu chi tiết thay đổi từng trường. Chỉ hiển thị ID người thao tác đã được ghi nhận.', 'Existing data does not record field changes. Only the recorded actor ID is shown.')}
    </Typography>
    <Button onClick={reload} className='mbe-3'>{l('Tải lại lịch sử', 'Reload history')}</Button>
    {!current ? <Box role='status' aria-live='polite' className='flex items-center gap-3'><CircularProgress size={24} />{l('Đang tải lịch sử…', 'Loading history…')}</Box> :
      !current.data ? <Alert severity='error'>{error(current.status)}</Alert> : <>
        <Typography variant='caption'>{l('Thời gian Việt Nam (UTC+7)', 'Vietnam time (UTC+7)')} · {l('Đọc đến phiên bản', 'Through version')} {current.data.throughVersion}</Typography>
        {current.data.items.length === 0 ? <Typography role='status' className='mbs-3'>{l('Không có sự kiện trong phạm vi lịch sử này.', 'No events in this history coverage.')}</Typography> :
          <Box component='ol' sx={{ pl: 3, mb: 0 }}>{current.data.items.map(item => <Box component='li' key={item.id} sx={{ py: 2, overflowWrap: 'anywhere' }}>
            <Typography component='h3' variant='subtitle1'>{action(item.action)} · v{item.version}</Typography>
            <Typography component='time' dateTime={item.occurredAt} variant='body2'>{new Intl.DateTimeFormat(isEn ? 'en-US' : 'vi-VN', { dateStyle: 'medium', timeStyle: 'medium', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(item.occurredAt))}</Typography>
            <Typography variant='body2' sx={{ overflowWrap: 'anywhere' }}>{l('ID người thao tác', 'Actor ID')}: {item.actorUserId}</Typography>
            {kind === 'document' && <>
              <Typography variant='body2'>{status((item as DocumentLifecycleEvent).fromStatus)} → {status((item as DocumentLifecycleEvent).toStatus)}</Typography>
              {(item as DocumentLifecycleEvent).cancellationReason !== null && <Typography variant='body2' sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>
                {l('Lý do hủy của lần này', 'Cancellation reason for this cycle')}: {(item as DocumentLifecycleEvent).cancellationReason}
              </Typography>}
            </>}
          </Box>)}</Box>}
        <TablePagination component='div' count={current.data.totalCount} page={query.pageNumber - 1} rowsPerPage={query.pageSize} rowsPerPageOptions={[10, 20, 50]}
          labelRowsPerPage={l('Số sự kiện', 'Events per page')} labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`}
          getItemAriaLabel={type => type === 'next' ? l('Trang sau', 'Next page') : l('Trang trước', 'Previous page')}
          onPageChange={(_, page) => { if (owned()) setCursor({ scope, pageNumber: page + 1, pageSize: query.pageSize, throughVersion: current.data!.throughVersion }) }}
          onRowsPerPageChange={e => { if (owned()) setCursor({ scope, pageNumber: 1, pageSize: Number(e.target.value) }) }} />
      </>}
  </CardContent></Card>
}
