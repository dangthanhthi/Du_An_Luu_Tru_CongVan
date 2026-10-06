'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useEffect, useState } from 'react'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'
import TextField from '@mui/material/TextField'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import TablePagination from '@mui/material/TablePagination'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import Dialog from '@mui/material/Dialog'
import DialogTitle from '@mui/material/DialogTitle'
import DialogContent from '@mui/material/DialogContent'
import DialogActions from '@mui/material/DialogActions'
import { externalEntityApi } from '@/services/das/external-entities'
import { ApiRequestError } from '@/services/api'
import type { ExternalEntity, ExternalEntityPage, ExternalEntityType } from '@/types/das/external-entities'
import AddPartnerDrawer from './AddPartnerDrawer'
import tableStyles from '@core/styles/table.module.css'

const names = { Sender: 'Nơi gửi', Recipient: 'Nơi nhận', Both: 'Nơi gửi và nhận' }
export default function PartnerListTable() {
  const sessionIntent = useSessionIntent()
  const [search, setSearch] = useState('')
  const [type, setType] = useState<ExternalEntityType | ''>('')
  const [status, setStatus] = useState('')
  const [deleted, setDeleted] = useState(false)
  const [page, setPage] = useState(0)
  const [size, setSize] = useState(10)
  const [epoch, setEpoch] = useState(0)
  const [canManage, setCanManage] = useState(false)
  const [result, setResult] = useState<{ key: string; data?: ExternalEntityPage; error?: string }>({ key: '' })
  const [modal, setModal] = useState<{ entry: ExternalEntity | null } | null>(null)
  const [operation, setOperation] = useState<ExternalEntity | null>(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const key = JSON.stringify([search, type, status, deleted, page, size, epoch])
  const current = result.key === key ? result : undefined

  useEffect(() => {
    const controller = new AbortController()
    const timer = setTimeout(async () => {
      try {
        const options = await externalEntityApi.getOptions(controller.signal)

        if (controller.signal.aborted) return
        setCanManage(options.canManage)
        if (!options.canManage && deleted) { setDeleted(false); setPage(0); return }
        const data = await externalEntityApi.getList({ searchTerm: search, entityType: type || undefined,
          isActive: status === '' ? undefined : status === 'active', includeDeleted: deleted, pageNumber: page + 1, pageSize: size }, controller.signal)

        if (!controller.signal.aborted) setResult({ key, data })
      } catch (failure) {
        if (!controller.signal.aborted) { setCanManage(false); setResult({ key, error: failure instanceof Error ? failure.message : 'Không thể tải danh mục đơn vị.' }) }
      }
    }, 250)

    return () => { clearTimeout(timer); controller.abort() }
  }, [key, search, type, status, deleted, page, size])
  const reload = () => setEpoch(value => value + 1)
  const revoke = () => setCanManage(false)
  const changeDeletion = async () => {
    if (!operation || !canManage || busy) return
    setBusy(true); setNotice(null)
    try {
      await externalEntityApi.changeDeletion(operation, !operation.isDeleted, sessionIntent)
      setNotice({ type: 'success', text: operation.isDeleted ? 'Đã khôi phục đơn vị.' : 'Đã xóa mềm đơn vị. Hồ sơ cũ vẫn giữ nguyên.' });setOperation(null);reload()
    } catch (failure) {
      if (failure instanceof ApiRequestError && [401, 403].includes(failure.status)) revoke()
      setNotice({ type: 'error', text: failure instanceof Error ? failure.message : 'Không thể thay đổi đơn vị.' });setOperation(null);reload()
    } finally { setBusy(false) }
  }
  const writeAllowed = canManage && !!current?.data

  return <>
    {notice && <Alert severity={notice.type} className='mbe-4' onClose={() => setNotice(null)}>{notice.text}</Alert>}
    <Card>
      <CardContent>
        <Typography variant='h5' className='mbe-4'>Danh mục đơn vị ngoài</Typography>
        <div className='flex flex-wrap gap-4 items-center'>
          <TextField label='Tìm đơn vị' value={search} onChange={event => { setSearch(event.target.value); setPage(0) }} inputProps={{ maxLength: 200 }} />
          <TextField select label='Vai trò' value={type} sx={{ minWidth: 170 }} onChange={event => { setType(event.target.value as ExternalEntityType | ''); setPage(0) }}>
            <MenuItem value=''>Tất cả</MenuItem>{Object.entries(names).map(([code, name]) => <MenuItem key={code} value={code}>{name}</MenuItem>)}
          </TextField>
          <TextField select label='Trạng thái' value={status} sx={{ minWidth: 170 }} onChange={event => { setStatus(event.target.value); setPage(0) }}>
            <MenuItem value=''>Tất cả</MenuItem><MenuItem value='active'>Đang hoạt động</MenuItem><MenuItem value='inactive'>Ngừng hoạt động</MenuItem>
          </TextField>
          {canManage && <FormControlLabel label='Gồm đơn vị đã xóa' control={<Checkbox checked={deleted} onChange={event => { setDeleted(event.target.checked); setPage(0) }} />} />}
          {writeAllowed && <Button variant='contained' onClick={() => setModal({ entry: null })}>Thêm đơn vị</Button>}
          <Button onClick={reload}>Tải lại</Button>
        </div>
      </CardContent>
      {!current && <div className='p-6'><CircularProgress aria-label='Đang tải danh mục' /></div>}
      {current?.error && <Alert severity='error' className='m-4'>{current.error}</Alert>}
      {current?.data && <>
        <div className='overflow-x-auto'>
          <table className={tableStyles.table}>
            <thead><tr>{['Tên đơn vị', 'Người liên hệ', 'Email / điện thoại', 'Vai trò', 'Trạng thái', 'Thao tác'].map(label => <th key={label}>{label}</th>)}</tr></thead>
            <tbody>{current.data.items.map(entry => <tr key={entry.id}>
              <td><Typography>{entry.fullName}</Typography>{entry.shortName && <Typography variant='caption'>{entry.shortName}</Typography>}</td>
              <td>{entry.contactPerson || '—'}</td><td>{entry.email || '—'}<br />{entry.phone || '—'}</td><td>{names[entry.entityType]}</td>
              <td><Chip size='small' label={entry.isDeleted ? 'Đã xóa' : entry.isActive ? 'Đang hoạt động' : 'Ngừng hoạt động'} color={entry.isActive ? 'success' : 'default'} /></td>
              <td><div className='flex gap-2'>
                <Button size='small' onClick={() => setModal({ entry })}>{writeAllowed && !entry.isDeleted ? 'Sửa' : 'Xem'}</Button>
                {writeAllowed && <Button size='small' color={entry.isDeleted ? 'primary' : 'error'} disabled={busy} onClick={() => setOperation(entry)}>{entry.isDeleted ? 'Khôi phục' : 'Xóa mềm'}</Button>}
              </div></td>
            </tr>)}</tbody>
          </table>
        </div>
        {current.data.totalCount === 0 && <Typography className='p-6'>Không có đơn vị phù hợp.</Typography>}
        <TablePagination component='div' count={current.data.totalCount} page={page} rowsPerPage={size} rowsPerPageOptions={[10, 20, 50]}
          labelRowsPerPage='Số dòng' onPageChange={(_event, next) => setPage(next)} onRowsPerPageChange={event => { setSize(Number(event.target.value)); setPage(0) }} />
      </>}
    </Card>
    {modal && <AddPartnerDrawer original={modal.entry} canManage={canManage} onClose={() => { setModal(null); reload() }} onRevoked={revoke}
      onSaved={() => { setModal(null); setNotice({ type: 'success', text: 'Đã lưu đơn vị.' }); setPage(0); reload() }} />}
    <Dialog open={!!operation} onClose={() => { if (!busy) setOperation(null) }}>
      <DialogTitle>{operation?.isDeleted ? 'Khôi phục đơn vị' : 'Xóa mềm đơn vị'}</DialogTitle>
      <DialogContent>{operation?.fullName}<Typography>Hồ sơ cũ vẫn giữ thông tin đã đăng ký.</Typography></DialogContent>
      <DialogActions><Button disabled={busy} onClick={() => setOperation(null)}>Đóng</Button><Button disabled={busy || !canManage} onClick={changeDeletion}>Xác nhận</Button></DialogActions>
    </Dialog>
  </>
}
