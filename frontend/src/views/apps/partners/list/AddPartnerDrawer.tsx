'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useState } from 'react'
import Drawer from '@mui/material/Drawer'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import TextField from '@mui/material/TextField'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import { ApiRequestError } from '@/services/api'
import { externalEntityApi } from '@/services/das/external-entities'
import type { ExternalEntity, ExternalEntityDraft, ExternalEntityType } from '@/types/das/external-entities'

const fields = [['fullName', 'Tên đầy đủ', 500], ['shortName', 'Tên viết tắt', 450], ['taxCode', 'Mã số thuế', 450],
  ['contactPerson', 'Người liên hệ', 500], ['contactInformation', 'Thông tin liên hệ', 2000], ['email', 'Email', 254], ['phone', 'Điện thoại', 50], ['address', 'Địa chỉ', 1000]] as const
export default function AddPartnerDrawer({ original, canManage, onClose, onSaved, onRevoked }: {
  original: ExternalEntity | null; canManage: boolean; onClose: () => void; onSaved: (entry: ExternalEntity) => void; onRevoked: () => void
}) {
  const sessionIntent = useSessionIntent()
  const [draft, setDraft] = useState<ExternalEntityDraft>(original ?? { fullName: '', entityType: 'Both' })
  const [active, setActive] = useState(original?.isActive ?? true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const [conflict, setConflict] = useState(false)
  const [latest, setLatest] = useState(original)
  const writable = canManage && !latest?.isDeleted && !uncertain
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (busy || !writable || conflict) return
    setBusy(true); setError('')
    try {
      const saved = latest ? await externalEntityApi.update(latest, draft, active, sessionIntent) : await externalEntityApi.create(draft, sessionIntent)

      onSaved(saved)
    } catch (failure) {
      const status = failure instanceof ApiRequestError ? failure.status : 0

      if (status === 403 || status === 401) onRevoked()
      if (status === 409 && latest) setConflict(true)
      if (!latest && (status === 0 || status >= 500)) setUncertain(true)
      setError(failure instanceof Error ? failure.message : 'Không thể lưu đơn vị.')
    } finally { setBusy(false) }
  }
  const reload = async () => {
    if (!latest || busy) return
    setBusy(true)
    try {
      const entry = await externalEntityApi.getById(latest.id)

      setLatest(entry); setDraft(entry); setActive(entry.isActive); setConflict(false); setError('')
    } catch (failure) { setError(failure instanceof Error ? failure.message : 'Không thể tải lại.') }
    finally { setBusy(false) }
  }

  return <Drawer open anchor='right' onClose={() => { if (!busy) onClose() }} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 460 }, p: 4 } }}>
    <Typography variant='h5' className='mbe-4'>{original ? 'Thông tin đơn vị' : 'Thêm đơn vị ngoài'}</Typography>
    {error && <Alert severity='error' className='mbe-4'>{error}</Alert>}
    {uncertain && <Alert severity='warning' className='mbe-4'>Chưa xác định được kết quả tạo. Đóng và tải lại danh sách để kiểm tra trước khi tạo lại; nội dung đã nhập vẫn giữ ở đây.</Alert>}
    {!canManage && <Alert severity='info' className='mbe-4'>Bạn có quyền xem danh mục.</Alert>}
    <form onSubmit={submit} className='flex flex-col gap-4'>
      {fields.map(([name, label, max]) => <TextField key={name} label={label} required={name === 'fullName'}
        type={name === 'email' ? 'email' : 'text'} multiline={name === 'contactInformation' || name === 'address'}
        minRows={name === 'contactInformation' || name === 'address' ? 2 : undefined} inputProps={{ maxLength: max }}
        value={draft[name] ?? ''} disabled={busy || !writable} onChange={event => setDraft({ ...draft, [name]: event.target.value })} />)}
      <TextField select label='Vai trò đơn vị' value={draft.entityType ?? 'Both'} disabled={busy || !writable}
        onChange={event => setDraft({ ...draft, entityType: event.target.value as ExternalEntityType })}>
        <MenuItem value='Sender'>Nơi gửi</MenuItem><MenuItem value='Recipient'>Nơi nhận</MenuItem><MenuItem value='Both'>Nơi gửi và nhận</MenuItem>
      </TextField>
      {latest && <FormControlLabel label='Đang hoạt động' control={<Checkbox checked={active} disabled={busy || !writable} onChange={event => setActive(event.target.checked)} />} />}
      {conflict && latest && <Button disabled={busy} onClick={reload}>Bỏ thay đổi và tải lại phiên bản</Button>}
      <div className='flex flex-wrap gap-3'>
        {canManage && !latest?.isDeleted && <Button variant='contained' type='submit' disabled={busy || !writable || conflict}>{busy ? 'Đang lưu…' : 'Lưu đơn vị'}</Button>}
        <Button disabled={busy} onClick={onClose}>Đóng</Button>
      </div>
    </form>
  </Drawer>
}
