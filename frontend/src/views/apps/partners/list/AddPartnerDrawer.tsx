'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { useState, useRef } from 'react'
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

export default function AddPartnerDrawer({ original, canManage, onClose, onSaved, onRevoked }: {
  original: ExternalEntity | null; canManage: boolean; onClose: () => void; onSaved: (entry: ExternalEntity) => void; onRevoked: () => void
}) {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const [draft, setDraft] = useState<ExternalEntityDraft>(original ?? { fullName: '', entityType: 'Both' })
  const [active, setActive] = useState(original?.isActive ?? true)
  const [busy, setBusy] = useState(false)
  const inFlightRef = useRef(false)
  const [error, setError] = useState('')
  const [uncertain, setUncertain] = useState(false)
  const [conflict, setConflict] = useState(false)
  const [latest, setLatest] = useState(original)
  const writable = canManage && !latest?.isDeleted && !uncertain

  const fields = [
    ['fullName', isEn ? 'Full Name' : 'Tên đầy đủ', 500],
    ['shortName', isEn ? 'Short Name' : 'Tên viết tắt', 450],
    ['taxCode', isEn ? 'Tax Code' : 'Mã số thuế', 450],
    ['contactPerson', isEn ? 'Contact Person' : 'Người liên hệ', 500],
    ['contactInformation', isEn ? 'Contact Information' : 'Thông tin liên hệ', 2000],
    ['email', 'Email', 254],
    ['phone', isEn ? 'Phone' : 'Điện thoại', 50],
    ['address', isEn ? 'Address' : 'Địa chỉ', 1000]
  ] as const

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    if (inFlightRef.current || busy || !writable || conflict) return
    inFlightRef.current = true
    setBusy(true); setError('')
    try {
      const saved = latest ? await externalEntityApi.update(latest, draft, active, sessionIntent) : await externalEntityApi.create(draft, sessionIntent)

      onSaved(saved)
    } catch (failure) {
      const isApi = failure instanceof ApiRequestError
      const status = isApi ? failure.status : 0
      const code = isApi ? failure.code : undefined

      if (status === 403 || status === 401) onRevoked()
      if (status === 409) {
        if (code === 'PARTNER_DUPLICATE') {
          // Duplicate tax code or short name: preserve draft and allow correction without reload
          setConflict(false)
        } else if (latest) {
          // Version/state conflict: requires explicit reload
          setConflict(true)
        }
      }
      if (status === 0 || status >= 500) setUncertain(true)
      setError(failure instanceof Error ? failure.message : (isEn ? 'Unable to save partner.' : 'Không thể lưu đơn vị.'))
    } finally {
      inFlightRef.current = false
      setBusy(false)
    }
  }
  const reload = async () => {
    if (!latest || inFlightRef.current || busy) return
    inFlightRef.current = true
    setBusy(true)
    try {
      const entry = await externalEntityApi.getById(latest.id)

      setLatest(entry); setDraft(entry); setActive(entry.isActive); setConflict(false); setUncertain(false); setError('')
    } catch (failure) {
      if (failure instanceof ApiRequestError && (failure.status === 401 || failure.status === 403)) onRevoked()
      setError(failure instanceof Error ? failure.message : (isEn ? 'Unable to reload.' : 'Không thể tải lại.'))
    }
    finally {
      inFlightRef.current = false
      setBusy(false)
    }
  }

  return <Drawer open anchor='right' onClose={() => { if (!busy) onClose() }} slotProps={{ paper: { 'aria-labelledby': 'das-partner-editor-title' } }} sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 460 }, p: 4 } }}>
    <Typography id='das-partner-editor-title' variant='h5' className='mbe-4'>{original ? (isEn ? 'Partner Information' : 'Thông tin đơn vị') : (isEn ? 'Add External Partner' : 'Thêm đơn vị ngoài')}</Typography>
    {error && <Alert severity='error' className='mbe-4'>{error}</Alert>}
    {uncertain && <Alert severity='warning' className='mbe-4'>{isEn ? 'Creation result is uncertain due to connection timeout. Close and reload list to verify before creating again; entered content is retained here.' : 'Chưa xác định được kết quả tạo. Đóng và tải lại danh sách để kiểm tra trước khi tạo lại; nội dung đã nhập vẫn giữ ở đây.'}</Alert>}
    {!canManage && <Alert severity='info' className='mbe-4'>{isEn ? 'You have read-only access to this directory.' : 'Bạn có quyền xem danh mục.'}</Alert>}
    <form onSubmit={submit} className='flex flex-col gap-4'>
      {fields.map(([name, label, max]) => <TextField key={name} label={label} autoFocus={name === 'fullName'} required={name === 'fullName'}
        type={name === 'email' ? 'email' : 'text'} multiline={name === 'contactInformation' || name === 'address'}
        minRows={name === 'contactInformation' || name === 'address' ? 2 : undefined} inputProps={{ maxLength: max }}
        value={draft[name] ?? ''} disabled={busy || !writable} onChange={event => setDraft({ ...draft, [name]: event.target.value })} />)}
      <TextField select label={isEn ? 'Partner Role' : 'Vai trò đơn vị'} value={draft.entityType ?? 'Both'} disabled={busy || !writable}
        onChange={event => setDraft({ ...draft, entityType: event.target.value as ExternalEntityType })}>
        <MenuItem value='Sender'>{isEn ? 'Sender' : 'Nơi gửi'}</MenuItem>
        <MenuItem value='Recipient'>{isEn ? 'Recipient' : 'Nơi nhận'}</MenuItem>
        <MenuItem value='Both'>{isEn ? 'Sender & Recipient' : 'Nơi gửi và nhận'}</MenuItem>
      </TextField>
      {latest && <FormControlLabel label={isEn ? 'Active' : 'Đang hoạt động'} control={<Checkbox checked={active} disabled={busy || !writable} onChange={event => setActive(event.target.checked)} />} />}
      {(conflict || uncertain) && latest && <Button disabled={busy} onClick={reload}>{isEn ? 'Discard changes & reload version' : 'Bỏ thay đổi và tải lại phiên bản'}</Button>}
      <div className='flex flex-wrap gap-3'>
        {canManage && !latest?.isDeleted && <Button variant='contained' type='submit' disabled={busy || !writable || conflict || uncertain}>{busy ? (isEn ? 'Saving…' : 'Đang lưu…') : (isEn ? 'Save Partner' : 'Lưu đơn vị')}</Button>}
        <Button disabled={busy} onClick={onClose}>{isEn ? 'Close' : 'Đóng'}</Button>
      </div>
    </form>
  </Drawer>
}
