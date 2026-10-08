'use client'

import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useEffect, useState, useRef } from 'react'
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

export default function PartnerListTable() {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
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
  const inFlightRef = useRef(false)
  const reloadButtonRef = useRef<HTMLButtonElement>(null)
  const editorWasOpen = useRef(false)
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)

  const names = {
    Sender: isEn ? 'Sender' : 'Nơi gửi',
    Recipient: isEn ? 'Recipient' : 'Nơi nhận',
    Both: isEn ? 'Sender & Recipient' : 'Nơi gửi và nhận'
  }

  const key = JSON.stringify([search, type, status, deleted, page, size, epoch])
  const current = result.key === key ? result : undefined

  useEffect(() => {
    // Closing the editor refreshes the list and unmounts its row trigger.
    // Restore keyboard focus to a stable control after modal cleanup.
    if (editorWasOpen.current && !modal) reloadButtonRef.current?.focus()
    editorWasOpen.current = Boolean(modal)
  }, [modal])

  useEffect(() => {
    if (result.key === key && result.data) return
    const controller = new AbortController()
    const timer = setTimeout(async () => {
      try {
        const options = await externalEntityApi.getOptions(controller.signal)

        if (controller.signal.aborted) return
        setCanManage(options.canManage)
        if (!options.canManage && deleted) { setDeleted(false); setPage(0); return }
        const data = await externalEntityApi.getList({
          searchTerm: search,
          entityType: type || undefined,
          isActive: status === '' ? undefined : status === 'active',
          includeDeleted: deleted,
          pageNumber: page + 1,
          pageSize: size
        }, controller.signal)

        if (!controller.signal.aborted) {
          const maxPage = Math.max(0, Math.ceil(data.totalCount / size) - 1)
          if (page > maxPage) {
            setPage(maxPage)
            setResult({ key: '' })
            return
          }
          setResult({ key, data })
        }
      } catch (failure) {
        if (!controller.signal.aborted) {
          if (failure instanceof ApiRequestError && [401, 403].includes(failure.status)) {
            setCanManage(false)
          }
          setResult({ key, error: failure instanceof Error ? failure.message : (isEn ? 'Failed to load external entities.' : 'Không thể tải danh mục đơn vị.') })
        }
      }
    }, 250)

    return () => { clearTimeout(timer); controller.abort() }
  }, [key, search, type, status, deleted, page, size, isEn])

  const reload = () => setEpoch(value => value + 1)
  const revoke = () => setCanManage(false)

  const changeDeletion = async () => {
    if (inFlightRef.current || !operation || !canManage || busy) return
    inFlightRef.current = true
    setBusy(true); setNotice(null)
    try {
      await externalEntityApi.changeDeletion(operation, !operation.isDeleted, sessionIntent)
      setNotice({
        type: 'success',
        text: operation.isDeleted
          ? (isEn ? 'Entity restored successfully.' : 'Đã khôi phục đơn vị.')
          : (isEn ? 'Entity soft-deleted successfully. Historical records preserved.' : 'Đã xóa mềm đơn vị. Hồ sơ cũ vẫn giữ nguyên.')
      })
      setOperation(null)
      reload()
    } catch (failure) {
      if (failure instanceof ApiRequestError && [401, 403].includes(failure.status)) revoke()
      setNotice({
        type: 'error',
        text: failure instanceof Error ? failure.message : (isEn ? 'Failed to update entity.' : 'Không thể thay đổi đơn vị.')
      })
      setOperation(null)
      reload()
    } finally {
      inFlightRef.current = false
      setBusy(false)
    }
  }

  const writeAllowed = canManage && !!current?.data

  return (
    <>
      {notice && <Alert severity={notice.type} className='mbe-4' onClose={() => setNotice(null)}>{notice.text}</Alert>}
      <Card>
        <CardContent>
          <Typography variant='h5' className='mbe-4'>
            {isEn ? 'External Entities Directory' : 'Danh mục đơn vị ngoài'}
          </Typography>
          <div className='flex flex-wrap gap-4 items-center'>
            <TextField
              label={isEn ? 'Search entity' : 'Tìm đơn vị'}
              value={search}
              onChange={event => { setSearch(event.target.value); setPage(0) }}
              inputProps={{ maxLength: 200 }}
            />
            <TextField
              select
              label={isEn ? 'Role' : 'Vai trò'}
              value={type}
              sx={{ minWidth: 170 }}
              onChange={event => { setType(event.target.value as ExternalEntityType | ''); setPage(0) }}
            >
              <MenuItem value=''>{isEn ? 'All roles' : 'Tất cả'}</MenuItem>
              {Object.entries(names).map(([code, name]) => <MenuItem key={code} value={code}>{name}</MenuItem>)}
            </TextField>
            <TextField
              select
              label={isEn ? 'Status' : 'Trạng thái'}
              value={status}
              sx={{ minWidth: 170 }}
              onChange={event => { setStatus(event.target.value); setPage(0) }}
            >
              <MenuItem value=''>{isEn ? 'All statuses' : 'Tất cả'}</MenuItem>
              <MenuItem value='active'>{isEn ? 'Active' : 'Đang hoạt động'}</MenuItem>
              <MenuItem value='inactive'>{isEn ? 'Inactive' : 'Ngừng hoạt động'}</MenuItem>
            </TextField>
            {canManage && (
              <FormControlLabel
                label={isEn ? 'Include deleted' : 'Gồm đơn vị đã xóa'}
                control={<Checkbox checked={deleted} onChange={event => { setDeleted(event.target.checked); setPage(0) }} />}
              />
            )}
            {writeAllowed && (
              <Button variant='contained' onClick={() => setModal({ entry: null })}>
                {isEn ? 'Add Entity' : 'Thêm đơn vị'}
              </Button>
            )}
            <Button ref={reloadButtonRef} onClick={reload}>{isEn ? 'Reload' : 'Tải lại'}</Button>
          </div>
        </CardContent>

        {!current && (
          <div className='p-6'>
            <CircularProgress aria-label={isEn ? 'Loading directory' : 'Đang tải danh mục'} />
          </div>
        )}

        {current?.error && <Alert severity='error' className='m-4'>{current.error}</Alert>}

        {current?.data && (
          <>
            <div className='overflow-x-auto'>
              <table className={tableStyles.table}>
                <thead>
                  <tr>
                    {[
                      isEn ? 'Entity Name' : 'Tên đơn vị',
                      isEn ? 'Contact Person' : 'Người liên hệ',
                      isEn ? 'Email / Phone' : 'Email / điện thoại',
                      isEn ? 'Role' : 'Vai trò',
                      isEn ? 'Status' : 'Trạng thái',
                      isEn ? 'Actions' : 'Thao tác'
                    ].map(label => <th key={label}>{label}</th>)}
                  </tr>
                </thead>
                <tbody>
                  {current.data.items.map(entry => (
                    <tr key={entry.id}>
                      <td>
                        <Typography fontWeight={500}>{entry.fullName}</Typography>
                        {entry.shortName && <Typography variant='caption'>{entry.shortName}</Typography>}
                      </td>
                      <td>{entry.contactPerson || '—'}</td>
                      <td>
                        {entry.email || '—'}
                        <br />
                        {entry.phone || '—'}
                      </td>
                      <td>{names[entry.entityType]}</td>
                      <td>
                        <Chip
                          size='small'
                          label={
                            entry.isDeleted
                              ? (isEn ? 'Deleted' : 'Đã xóa')
                              : entry.isActive
                                ? (isEn ? 'Active' : 'Đang hoạt động')
                                : (isEn ? 'Inactive' : 'Ngừng hoạt động')
                          }
                          color={entry.isDeleted ? 'default' : entry.isActive ? 'success' : 'default'}
                        />
                      </td>
                      <td>
                        <div className='flex gap-2'>
                          <Button size='small' onClick={() => setModal({ entry })}>
                            {writeAllowed && !entry.isDeleted ? (isEn ? 'Edit' : 'Sửa') : (isEn ? 'View' : 'Xem')}
                          </Button>
                          {writeAllowed && (
                            <Button
                              size='small'
                              color={entry.isDeleted ? 'primary' : 'error'}
                              disabled={busy}
                              onClick={() => setOperation(entry)}
                            >
                              {entry.isDeleted ? (isEn ? 'Restore' : 'Khôi phục') : (isEn ? 'Soft delete' : 'Xóa mềm')}
                            </Button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {current.data.totalCount === 0 && (
              <Typography className='p-6 text-center text-textSecondary'>
                {isEn ? 'No matching entities found.' : 'Không có đơn vị phù hợp.'}
              </Typography>
            )}

            <TablePagination
              component='div'
              count={current.data.totalCount}
              page={Math.min(page, Math.max(0, Math.ceil((current.data.totalCount || 1) / size) - 1))}
              rowsPerPage={size}
              rowsPerPageOptions={[10, 20, 50]}
              labelRowsPerPage={isEn ? 'Rows per page:' : 'Số dòng mỗi trang:'}
              labelDisplayedRows={({ from, to, count }) =>
                isEn
                  ? `${from}–${to} of ${count !== -1 ? count : `more than ${to}`}`
                  : `${from}–${to} trên ${count !== -1 ? count : `nhiều hơn ${to}`}`
              }
              getItemAriaLabel={type =>
                type === 'first'
                  ? (isEn ? 'Go to first page' : 'Đến trang đầu')
                  : type === 'last'
                    ? (isEn ? 'Go to last page' : 'Đến trang cuối')
                    : type === 'next'
                      ? (isEn ? 'Go to next page' : 'Sang trang sau')
                      : (isEn ? 'Go to previous page' : 'Về trang trước')
              }
              onPageChange={(_event, next) => setPage(next)}
              onRowsPerPageChange={event => { setSize(Number(event.target.value)); setPage(0) }}
            />
          </>
        )}
      </Card>

      {modal && (
        <AddPartnerDrawer
          original={modal.entry}
          canManage={canManage}
          onClose={() => { setModal(null); reload() }}
          onRevoked={revoke}
          onSaved={() => {
            setModal(null)
            setNotice({ type: 'success', text: isEn ? 'Entity saved successfully.' : 'Đã lưu đơn vị.' })
            setPage(0)
            reload()
          }}
        />
      )}

      <Dialog open={!!operation} onClose={() => { if (!busy) setOperation(null) }}>
        <DialogTitle>
          {operation?.isDeleted
            ? (isEn ? 'Restore Entity' : 'Khôi phục đơn vị')
            : (isEn ? 'Soft Delete Entity' : 'Xóa mềm đơn vị')}
        </DialogTitle>
        <DialogContent>
          <Typography fontWeight={600} className='mbe-2'>{operation?.fullName}</Typography>
          <Typography variant='body2' color='text.secondary'>
            {isEn
              ? 'Historical documents will retain recorded partner entity information.'
              : 'Hồ sơ cũ vẫn giữ thông tin đã đăng ký.'}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={() => setOperation(null)}>
            {isEn ? 'Cancel' : 'Đóng'}
          </Button>
          <Button color={operation?.isDeleted ? 'primary' : 'error'} disabled={busy || !canManage} onClick={changeDeletion}>
            {isEn ? 'Confirm' : 'Xác nhận'}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  )
}
