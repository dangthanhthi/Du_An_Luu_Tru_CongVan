'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'
import { useEffect, useRef, useState } from 'react'
import { useParams, useRouter, useSearchParams } from 'next/navigation'
import Card from '@mui/material/Card'
import CardHeader from '@mui/material/CardHeader'
import CardContent from '@mui/material/CardContent'
import Grid from '@mui/material/Grid'
import TextField from '@mui/material/TextField'
import MenuItem from '@mui/material/MenuItem'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import Typography from '@mui/material/Typography'
import { documentsV2Api } from '@/services/das/documents'
import { ApiRequestError } from '@/services/api'
import type { DocumentKind } from '@/types/das/documents'
import type { DocumentDetailV2, DocumentFormOptions, KindDetails, RegistrationDraft } from '@/types/das/document-v2'
import V2ReferencePicker from './V2ReferencePicker'
import type { ReferenceOption } from './V2ReferencePicker'

type Model = { draft: RegistrationDraft; options: DocumentFormOptions; original?: DocumentDetailV2; external: ReferenceOption[]; sender: ReferenceOption[]; related: ReferenceOption[] }
export default function V2DocumentForm({ id }: { id?: string }) {
  const sessionIntent = useSessionIntent()
  const router = useRouter(), params = useParams(), search = useSearchParams()
  const kindValue = search.get('kind') ?? 'Outgoing'
  const validKind = ['Incoming', 'Outgoing', 'Internal'].includes(kindValue) && search.getAll('kind').length <= 1
  const [revision, setRevision] = useState(0), [busy, setBusy] = useState(false)
  const key = `${id ?? kindValue}:${revision}`
  const [state, setState] = useState<{ key: string; model?: Model; error?: string }>({ key: '' })
  const [writeError, setWriteError] = useState(''), [ambiguous, setAmbiguous] = useState(false)
  const retry = useRef<{ key: string; body: string } | null>(null)
  const current = state.key === key ? state : null, model = current?.model
  const lang = typeof params.lang === 'string' ? params.lang : 'vi'

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        if (!id && !validKind) throw new Error('Loại công văn không hợp lệ.')
        const original = id ? await documentsV2Api.detail(id, controller.signal) : undefined
        const kind = original?.header.kind ?? kindValue as DocumentKind
        const options = await documentsV2Api.options(kind, controller.signal)
        const primary = options.targets.find(t => t.originatorUserId === options.userId && t.isPrimary) ?? options.targets.find(t => t.originatorUserId === options.userId)
        const external = original?.recipients.filter(r => r.referenceType === 'ExternalEntity').map(r => ({ id: r.referenceId, name: r.name })) ?? []
        const related = original ? await Promise.all(original.relatedDocumentIds.map(async relatedId => {
          const d = await documentsV2Api.detail(relatedId, controller.signal)

          return { id: relatedId, name: `${d.header.registrationNumber} — ${d.header.subject}` }
        })) : []
        const source = original?.details
        const details: KindDetails = {
          receivingDate: source?.receivingDate ?? null, senderPartnerId: source?.senderPartnerId ?? null,
          referenceNumber: source?.referenceNumber ?? null, methodCode: source?.methodCode ?? null,
          documentTypeCode: source?.documentTypeCode ?? null, categoryCode: source?.categoryCode ?? null,
          contractNumber: source?.contractNumber ?? null, otherRecipients: source?.otherRecipients ?? null, others: source?.others ?? null,
          recipientPartnerIds: external.map(x => x.id), distributionTargetIds: original?.recipients.filter(r => r.referenceType === 'DistributionTarget').map(r => r.referenceId) ?? []
        }
        const draft: RegistrationDraft = { kind, companyCode: original?.header.companyCode ?? options.catalogs.find(c => c.group === 'companies')?.code ?? '',
          subject: original?.header.subject ?? '', originatorUserId: original?.originatorUserId ?? primary?.originatorUserId ?? '',
          ownerDepartmentId: original?.ownerDepartmentId ?? primary?.departmentId ?? '', sensitivity: original?.header.sensitivity ?? 'Normal',
          issuedDate: original?.header.issuedDate ?? null, remark: original?.remark ?? null, details }

        const sender = source?.senderPartnerId ? [{ id: source.senderPartnerId, name: source.senderNameSnapshot ?? 'Đơn vị đã lưu' }] : []

        if (!controller.signal.aborted) { retry.current = null; setAmbiguous(false); setWriteError(''); setState({ key, model: { draft, options, original, external, sender, related } }) }
      } catch (error) {
        if (!controller.signal.aborted) setState({ key, error: error instanceof Error ? error.message : 'Không thể tải form.' })
      }
    }
    load()
    return () => controller.abort()
  }, [id, key, kindValue, validKind])

  function update(patch: Partial<RegistrationDraft>) {
    if (!model) return
    setState({ key, model: { ...model, draft: { ...model.draft, ...patch } } })
  }
  function details(patch: Partial<KindDetails>) { update({ details: { ...model?.draft.details, ...patch } }) }
  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!model || busy) return
    setBusy(true); setWriteError('')
    try {
      sessionIntent.assertCurrent()
      const { draft, original } = model
      const relatedIds = model.related.map(x => x.id)
      const result = id && original ? await documentsV2Api.edit(id, {
        companyCode: draft.companyCode, subject: draft.subject, originatorUserId: draft.originatorUserId, ownerDepartmentId: draft.ownerDepartmentId,
        sensitivity: draft.sensitivity, issuedDate: draft.issuedDate, remark: draft.remark, details: draft.details,
        expectedVersion: original.header.version, ...(draft.kind !== 'Internal' ? { relations: { addedIds: relatedIds.filter(x => !original.relatedDocumentIds.includes(x)), removedIds: original.relatedDocumentIds.filter(x => !relatedIds.includes(x)) } } : {})
      }, sessionIntent) : await (async () => {
        const body = { ...draft, ...(draft.kind !== 'Internal' ? { relatedDocumentIds: relatedIds } : {}) }
        const serialized = JSON.stringify(body)

        if (retry.current && retry.current.body !== serialized && ambiguous) throw new Error('Hãy thử lại yêu cầu đang chờ xác nhận trước khi thay đổi nội dung.')
        if (!retry.current || retry.current.body !== serialized) retry.current = { key: crypto.randomUUID(), body: serialized }
        return documentsV2Api.register(body, retry.current.key, sessionIntent)
      })()

      window.dispatchEvent(new Event('das_documents_updated'))
      router.push(`/${lang}/apps/documents/${result.id}`)
    } catch (error) {
      const uncertain = !(error instanceof ApiRequestError) || error.status === 0 || error.status >= 500

      setAmbiguous(uncertain)
      setWriteError(error instanceof ApiRequestError && error.status === 409 ? 'Công văn đã thay đổi. Tải lại dữ liệu trước khi sửa tiếp.'
        : uncertain ? 'Chưa xác nhận được kết quả lưu. Đăng ký mới: thử lại sẽ dùng cùng mã yêu cầu; chỉnh sửa: tải lại để đối chiếu dữ liệu.'
        : error instanceof Error ? error.message : 'Không thể lưu công văn.')
    } finally { setBusy(false) }
  }
  if (!current) return <div className='flex justify-center p-8'><CircularProgress aria-label='Đang tải form công văn' /></div>
  if (!model) return <Card><CardContent><Alert severity='error'>{current.error}</Alert><Button onClick={() => setRevision(x => x + 1)}>Thử lại</Button></CardContent></Card>
  const { draft, options, original } = model
  const permitted = id ? original?.header.allowedActions.includes('Edit') : options.canRegister
  const disabled = busy || !permitted || ambiguous
  const targetKey = `${draft.originatorUserId}:${draft.ownerDepartmentId}`
  const field = (label: string, value: string | null | undefined, onChange: (v: string) => void, required = false, type = 'text') =>
    <TextField fullWidth label={label} value={value ?? ''} onChange={e => onChange(e.target.value)} disabled={disabled} required={required} type={type} slotProps={{ inputLabel: { shrink: true } }} />
  const catalog = (label: string, group: string, value: string | null | undefined, onChange: (v: string) => void, required = false) => {
    const rows = options.catalogs.filter(c => c.group === group)

    return <TextField fullWidth select label={label} value={value ?? ''} onChange={e => onChange(e.target.value)} disabled={disabled} required={required}>
      <MenuItem value=''>—</MenuItem>{value && !rows.some(c => c.code === value) && <MenuItem value={value}>{value} (đã lưu)</MenuItem>}
      {rows.map(c => <MenuItem key={c.code} value={c.code}>{c.name}</MenuItem>)}
    </TextField>
  }

  return <Card><CardHeader title={id ? `Sửa công văn ${original?.header.registrationNumber}` : `Đăng ký công văn ${draft.kind === 'Incoming' ? 'đến' : draft.kind === 'Outgoing' ? 'đi' : 'nội bộ'}`} />
    <CardContent>
      {!permitted && <Alert severity='warning' className='mbe-4'>Bạn chưa có quyền thực hiện thao tác này.</Alert>}
      {writeError && <Alert severity='error' className='mbe-4'>{writeError}</Alert>}
      <form onSubmit={submit}><Grid container spacing={4}>
        {original && <Grid size={{ xs: 12 }}><Typography>Số công văn: {original.header.registrationNumber} · Ngày đăng ký: {original.header.registrationDate}</Typography></Grid>}
        <Grid size={{ xs: 12, md: 6 }}>{catalog('Công ty', 'companies', draft.companyCode, v => update({ companyCode: v }), true)}</Grid>
        <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth select label='Người gửi / Phòng' value={targetKey} disabled={disabled} required onChange={e => {
          const target = options.targets.find(t => `${t.originatorUserId}:${t.departmentId}` === e.target.value)

          if (target) update({ originatorUserId: target.originatorUserId, ownerDepartmentId: target.departmentId })
        }}>
          {!options.targets.some(t => `${t.originatorUserId}:${t.departmentId}` === targetKey) && <MenuItem value={targetKey}>{original ? `${original.header.departmentName} (đã lưu)` : 'Chưa có phòng được cấp'}</MenuItem>}
          {options.targets.map(t => <MenuItem key={`${t.originatorUserId}:${t.departmentId}`} value={`${t.originatorUserId}:${t.departmentId}`}>{t.originatorName} / {t.departmentName}{t.isPrimary ? ' (chính)' : ''}</MenuItem>)}
        </TextField></Grid>
        <Grid size={{ xs: 12 }}>{field('Trích yếu', draft.subject, v => update({ subject: v }), true)}</Grid>
        <Grid size={{ xs: 12, md: 6 }}>{field('Ngày phát hành', draft.issuedDate, v => update({ issuedDate: v || null }), false, 'date')}</Grid>
        <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth select label='Độ mật' value={draft.sensitivity} disabled={disabled} onChange={e => update({ sensitivity: e.target.value as RegistrationDraft['sensitivity'] })}><MenuItem value='Normal'>Normal</MenuItem><MenuItem value='Confidential'>Confidential</MenuItem></TextField></Grid>
        {draft.kind === 'Incoming' && <>
          <Grid size={{ xs: 12, md: 6 }}>{field('Ngày nhận', draft.details?.receivingDate, v => details({ receivingDate: v || null }), true, 'date')}</Grid>
          <Grid size={{ xs: 12, md: 6 }}><V2ReferencePicker label='Đơn vị gửi' multiple={false} disabled={disabled} value={model.sender} onChange={sender => setState({ key, model: { ...model, sender, draft: { ...draft, details: { ...draft.details, senderPartnerId: sender[0]?.id ?? null } } } })} /></Grid>
          <Grid size={{ xs: 12, md: 6 }}>{field('Số hiệu đối tác', draft.details?.referenceNumber, v => details({ referenceNumber: v || null }))}</Grid>
          <Grid size={{ xs: 12, md: 6 }}><TextField fullWidth select label='Nơi nhận' disabled={disabled} value={draft.details?.distributionTargetIds ?? []} SelectProps={{ multiple: true }} onChange={e => details({ distributionTargetIds: typeof e.target.value === 'string' ? e.target.value.split(',') : e.target.value })}>
            {original?.recipients.filter(r => r.referenceType === 'DistributionTarget' && !options.distributionTargets.some(t => t.id === r.referenceId)).map(r => <MenuItem key={r.referenceId} value={r.referenceId}>{r.name} (đã lưu)</MenuItem>)}
            {options.distributionTargets.map(t => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
          </TextField></Grid>
        </>}
        {draft.kind !== 'Internal' && <Grid size={{ xs: 12, md: 6 }}>{catalog('Phương thức', 'methods', draft.details?.methodCode, v => details({ methodCode: v || null }), draft.kind === 'Incoming')}</Grid>}
        <Grid size={{ xs: 12, md: 6 }}>{catalog('Loại văn bản', draft.kind === 'Internal' ? 'internalTypes' : 'documentTypes', draft.details?.documentTypeCode, v => details({ documentTypeCode: v || null }))}</Grid>
        <Grid size={{ xs: 12, md: 6 }}>{catalog('Category', 'categories', draft.details?.categoryCode, v => details({ categoryCode: v || null }))}</Grid>
        {draft.kind === 'Outgoing' && <>
          <Grid size={{ xs: 12 }}><V2ReferencePicker label='Đơn vị nhận' value={model.external} disabled={disabled} onChange={external => setState({ key, model: { ...model, external, draft: { ...draft, details: { ...draft.details, recipientPartnerIds: external.map(x => x.id) } } } })} /></Grid>
          <Grid size={{ xs: 12, md: 6 }}>{field('Số hợp đồng', draft.details?.contractNumber, v => details({ contractNumber: v || null }))}</Grid>
          <Grid size={{ xs: 12, md: 6 }}>{field('Nơi nhận khác', draft.details?.otherRecipients, v => details({ otherRecipients: v || null }))}</Grid>
        </>}
        {draft.kind !== 'Internal' && <Grid size={{ xs: 12 }}><V2ReferencePicker label='Công văn liên quan' documentKind={draft.kind === 'Incoming' ? 'Outgoing' : 'Incoming'} value={model.related} disabled={disabled} onChange={related => setState({ key, model: { ...model, related } })} /></Grid>}
        <Grid size={{ xs: 12 }}>{field('Thông tin khác', draft.details?.others, v => details({ others: v || null }))}</Grid>
        <Grid size={{ xs: 12 }}><TextField fullWidth multiline minRows={3} label='Ghi chú' value={draft.remark ?? ''} disabled={disabled} onChange={e => update({ remark: e.target.value || null })} /></Grid>
        <Grid size={{ xs: 12 }} className='flex flex-wrap gap-3'>
          <Button variant='contained' type='submit' disabled={busy || !permitted || ambiguous && !!id}>{busy ? 'Đang lưu…' : ambiguous ? 'Thử lại cùng yêu cầu' : 'Lưu công văn'}</Button>
          {!!id && <Button disabled={busy} onClick={() => setRevision(x => x + 1)}>Tải lại dữ liệu</Button>}
          <Button disabled={busy} onClick={() => router.back()}>Quay lại</Button>
        </Grid>
      </Grid></form>
    </CardContent></Card>
}
