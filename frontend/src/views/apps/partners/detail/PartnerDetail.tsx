'use client'

import { useEffect, useState } from 'react'
import Link from 'next/link'
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Typography } from '@mui/material'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useAppDictionary } from '@/hooks/useDictionary'
import { externalEntityApi } from '@/services/das/external-entities'
import { historyGuid } from '@/services/das/history'
import { ApiRequestError } from '@/services/api'
import type { ExternalEntity } from '@/types/das/external-entities'
import HistoryPanel from '@/views/apps/history/HistoryPanel'

export default function PartnerDetail({ id, lang, validQuery = true }: { id: string; lang: string; validQuery?: boolean }) {
  const intent = useSessionIntent(), { isEn } = useAppDictionary()
  const l = (vi: string, en: string) => isEn ? en : vi
  const owned = () => { try { intent.assertCurrent(); return true } catch { return false } }
  const valid = historyGuid(id) && ['vi', 'en', 'fr', 'ar'].includes(lang) && validQuery
  const [revision, setRevision] = useState(0)
  const key = JSON.stringify([id, lang, validQuery, revision, intent.epoch])
  const [result, setResult] = useState<{ key: string; partner?: ExternalEntity; canAudit?: boolean; status?: number }>({ key: '' })
  const current = result.key === key ? result : undefined

  useEffect(() => {
    if (!valid || !owned()) return
    const controller = new AbortController()
    let authorized = false
    const load = async () => {
      const options = await externalEntityApi.getOptions(controller.signal)

      if (controller.signal.aborted || !owned()) return
      if (!options.canManage) throw new ApiRequestError(403, 'History permission required')
      authorized = true
      const partner = await externalEntityApi.getById(id.toLowerCase(), controller.signal)

      if (!controller.signal.aborted && owned()) setResult({ key, partner, canAudit: true })
    }

    load().catch(error => {
      if (!controller.signal.aborted && owned()) {
        const status = error instanceof ApiRequestError ? error.status : 0

        // The legacy editable summary uses safe numeric versions. Audit is a separate
        // authorized read contract with Int64 strings; a summary decode/dependency failure
        // must not prevent its own server policy from evaluating the history request.
        setResult({ key, status, canAudit: authorized && ![401, 403, 404].includes(status) })
      }
    })
    return () => controller.abort()
  }, [key, valid, id])

  if (!owned()) return <Alert severity='warning'>{l('Phiên đã thay đổi. Vui lòng đăng nhập lại.', 'Session changed. Please sign in again.')}</Alert>
  if (!valid) return <Alert severity='error'>{l('Liên kết lịch sử không hợp lệ.', 'Invalid history link.')}</Alert>
  const error = current?.status === 401 ? l('Vui lòng đăng nhập lại.', 'Please sign in again.') : current?.status === 403 ?
    l('Bạn không có quyền xem lịch sử đối tác.', 'You do not have permission to view partner history.') : current?.status === 404 ?
      l('Không tìm thấy đối tác.', 'Partner not found.') : l('Không thể tải đối tác. Vui lòng thử lại.', 'Cannot load partner. Please retry.')

  return <Box className='flex flex-col gap-4'>
    <Card><CardContent>
      <Button component={Link} href={`/${lang}/apps/partners/list`}>{l('Danh sách đối tác', 'Partner list')}</Button>
      {!current ? <Box role='status' className='flex items-center gap-3'><CircularProgress size={24} />{l('Đang tải đối tác…', 'Loading partner…')}</Box> : !current.partner ?
        <><Alert severity='error'>{error}</Alert><Button className='mbs-3' onClick={() => { if (owned()) setRevision(x => x + 1) }}>{l('Thử lại', 'Retry')}</Button></> : <>
          <Typography component='h1' variant='h4' sx={{ overflowWrap: 'anywhere' }}>{current.partner.fullName}</Typography>
          <Typography sx={{ overflowWrap: 'anywhere' }}>{current.partner.shortName || '—'} · ID: {current.partner.id}</Typography>
          <Chip className='mbs-3' label={current.partner.isDeleted ? l('Đã xóa mềm', 'Soft deleted') : current.partner.isActive ? l('Đang hoạt động', 'Active') : l('Ngừng hoạt động', 'Inactive')} />
          <Typography variant='body2' className='mbs-3'>{l('Đây là thông tin đối tác hiện tại, không phải ảnh chụp của từng sự kiện.', 'This is the current partner information, not a snapshot of each event.')}</Typography>
        </>}
    </CardContent></Card>
    {current?.canAudit && <HistoryPanel key={key} kind='partner' id={id} refreshKey={revision} />}
  </Box>
}
