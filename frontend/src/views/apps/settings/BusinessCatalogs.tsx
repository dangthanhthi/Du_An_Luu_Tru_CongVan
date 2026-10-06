'use client'
import { useSessionIntent } from '@/hooks/useSessionIntent'

import { useEffect, useRef, useState } from 'react'
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, FormControlLabel, Switch, Tab, Table, TableBody, TableCell, TableContainer, TableHead,
  TablePagination, TableRow, Tabs, Typography } from '@mui/material'

import CustomTextField from '@core/components/mui/TextField'
import { useAppDictionary } from '@/hooks/useDictionary'
import { tokenManager } from '@/services/api'
import { catalogApi } from '@/services/das/catalogs'
import { V2ApiError } from '@/services/das/http'
import { catalogGroups, editableCatalogGroups } from '@/types/das/catalogs'
import type { CatalogGroup, CatalogItem, DistributionPage } from '@/types/das/catalogs'

type Group = CatalogGroup | 'targets'
type Editor = { original?: CatalogItem; code: string; name: string; sortOrder: string; isActive: boolean }

const BusinessCatalogs = () => {
  const sessionIntent = useSessionIntent()
  const { isEn } = useAppDictionary()
  const l = (vi: string, en: string) => isEn ? en : vi
  const names: Record<Group, string> = { companies: l('Công ty', 'Companies'), methods: l('Phương thức', 'Methods'),
    documentTypes: l('Loại công văn', 'Document types'), internalTypes: l('Loại nội bộ', 'Internal types'),
    sensitivity: l('Độ mật', 'Sensitivity'), categories: l('Phân loại', 'Categories'), targets: l('Nơi nhận', 'Recipients') }
  const [group, setGroup] = useState<Group>('companies')
  const [canManage, setCanManage] = useState(false)
  const [revision, setRevision] = useState(0)
  const [pageNumber, setPageNumber] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [draftSearch, setDraftSearch] = useState('')
  const key = JSON.stringify([group, revision, pageNumber, pageSize, search])
  const [result, setResult] = useState<{ key: string; entries?: CatalogItem[]; targets?: DistributionPage; error?: unknown }>({ key: '' })
  const current = result.key === key ? result : undefined
  const loading = !current
  const [editor, setEditor] = useState<Editor | null>(null)
  const [busy, setBusy] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [mustReload, setMustReload] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const alive = useRef(false)
  const editable = group !== 'targets' && editableCatalogGroups.includes(group) && canManage

  useEffect(() => {
    alive.current = true
    try {
      const capabilities = tokenManager.getUser()?.capabilities

      setCanManage(Array.isArray(capabilities) && capabilities.includes('CatalogManage'))
    }
    catch { setCanManage(false) }
    return () => { alive.current = false }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    const request = group === 'targets'
      ? catalogApi.getDistributionTargets({ pageNumber, pageSize, search }, controller.signal).then(targets => ({ key, targets }))
      : catalogApi.getGroup(group, controller.signal).then(entries => ({ key, entries }))

    request.then(value => { if (!controller.signal.aborted) setResult(value) })
      .catch(error => { if (!controller.signal.aborted) setResult({ key, error }) })
    return () => controller.abort()
  }, [group, key, pageNumber, pageSize, search])

  const message = (error: unknown) => {
    const status = error instanceof V2ApiError ? error.status : 0

    if (status === 401) return l('Vui lòng đăng nhập lại.', 'Please sign in again.')
    if (status === 403) return l('Bạn không có quyền thực hiện thao tác này.', 'You do not have permission for this action.')
    return l('Không thể tải danh mục. Vui lòng thử lại.', 'Cannot load catalogs. Please retry.')
  }
  const open = (original?: CatalogItem) => {
    setEditor({ original, code: original?.code ?? '', name: original?.name ?? '', sortOrder: String(original?.sortOrder ?? 0), isActive: original?.isActive ?? true })
    setSaveError(null)
    setMustReload(false)
    setNotice(null)
  }
  const save = async () => {
    if (!editor || !editable || busy || mustReload) return
    const order = Number(editor.sortOrder)

    if (!editor.name.trim() || editor.name.trim().length > 200 || !Number.isSafeInteger(order) || order < 0 || order > 10000 ||
      (!editor.original && !/^[A-Z0-9_]{1,64}$/i.test(editor.code.trim()))) {
      setSaveError(l('Tên tối đa 200 ký tự; mã chỉ gồm chữ, số, dấu gạch dưới; thứ tự từ 0 đến 10000.', 'Name: up to 200 characters; code: letters, digits or underscore; order: 0–10000.'))
      return
    }
    setBusy(true)
    setSaveError(null)
    try {
      if (editor.original) await catalogApi.update(editor.original, { name: editor.name, sortOrder: order, isActive: editor.isActive }, sessionIntent)
      else await catalogApi.create({ group: group as CatalogGroup, code: editor.code, name: editor.name }, sessionIntent)
      if (alive.current) {
        setEditor(null)
        setRevision(value => value + 1)
        setNotice(l('Đã lưu danh mục.', 'Catalog saved.'))
      }
    } catch (error) {
      if (!alive.current) return
      const status = error instanceof V2ApiError ? error.status : 0

      if (status === 409 && editor.original) {
        setMustReload(true)
        setSaveError(l('Danh mục đã thay đổi. Tải phiên bản mới rồi kiểm tra trước khi lưu.', 'This entry has changed. Reload and review the latest version before saving.'))
      } else if (status === 409) setSaveError(l('Mã đã tồn tại, kể cả danh mục đã ngừng sử dụng. Chọn mã khác.', 'This code already exists, including inactive entries. Choose another code.'))
      else if (status === 0 || status >= 500) {
        setMustReload(true)
        setSaveError(l('Chưa xác định kết quả lưu. Máy chủ có thể đã lưu; kiểm tra dữ liệu trước khi thao tác lại.', 'Save outcome is unknown. The server may have saved it; check the data before trying again.'))
      } else setSaveError(status === 400 ? l('Dữ liệu không hợp lệ. Kiểm tra các trường và thử lại.', 'Invalid data. Review the fields and retry.') : message(error))
    } finally { if (alive.current) setBusy(false) }
  }
  const reloadEditor = async () => {
    if (!editor || busy) return
    setBusy(true)
    try {
      if (editor.original) {
        const latest = await catalogApi.getById(editor.original.id)

        if (!alive.current) return
        if (latest.group !== group || latest.code !== editor.original.code) throw new V2ApiError(502, 'Invalid identity')
        open(latest)
        setNotice(l('Đã tải phiên bản mới. Kiểm tra lại nội dung trước khi lưu.', 'Latest version loaded. Review the fields before saving.'))
      } else if (group !== 'targets') {
        await catalogApi.getGroup(group)
        if (alive.current) {
          setEditor(null)
          setNotice(l('Đã tải lại danh sách. Kiểm tra mã vừa tạo trước khi thêm tiếp.', 'List reloaded. Check the code you submitted before adding another entry.'))
        }
      }
      if (alive.current) setRevision(value => value + 1)
    } catch (error) { if (alive.current) setSaveError(message(error)) }
    finally { if (alive.current) setBusy(false) }
  }

  return <Card>
    <CardContent>
      <Typography variant='h4'>{l('Danh mục nghiệp vụ', 'Business catalogs')}</Typography>
      <Typography color='text.secondary' className='mbs-2'>
        {l('Ngừng sử dụng danh mục để giữ nguyên tham chiếu trên công văn cũ.', 'Deactivate entries to preserve references on historical documents.')}
      </Typography>
    </CardContent>
    <Tabs value={group} variant='scrollable' scrollButtons='auto' aria-label={l('Nhóm danh mục', 'Catalog groups')}
      onChange={(_, value: Group) => { if (!busy) { setGroup(value); setPageNumber(1); setSearch(''); setDraftSearch(''); setNotice(null) } }}>
      {[...catalogGroups, 'targets' as const].map(value => <Tab disabled={busy} key={value} value={value} label={names[value]} />)}
    </Tabs>
    <CardContent>
      {notice && <Alert severity='success' className='mbe-4'>{notice}</Alert>}
      {group === 'targets' ? <Alert severity='info' className='mbe-4'>
        {l('Nơi nhận đang chờ xác nhận mapping. Nhãn nơi nhận không cấp quyền truy cập công văn.', 'Recipient mapping awaits confirmation. Recipient labels do not grant document access.')}
      </Alert> : !editable ? <Alert severity='info' className='mbe-4'>
        {group === 'companies' || group === 'sensitivity' ? l('Danh mục này được cố định trong phiên bản hiện tại.', 'This catalog is fixed in this release.') :
          l('Bạn có thể xem danh mục. Cần quyền CatalogManage để chỉnh sửa.', 'You can view this catalog. CatalogManage is required to edit it.')}
      </Alert> : <Button className='mbe-4' variant='contained' disabled={loading || !!current?.error} onClick={() => open()}>{l('Thêm danh mục', 'Add entry')}</Button>}
      {group === 'targets' && <Box component='form' className='flex gap-3 mbe-4' onSubmit={e => { e.preventDefault(); setPageNumber(1); setSearch(draftSearch.trim()) }}>
        <CustomTextField label={l('Tìm nơi nhận', 'Search recipients')} value={draftSearch} onChange={e => setDraftSearch(e.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <Button type='submit'>{l('Tìm kiếm', 'Search')}</Button>
      </Box>}
      <Box aria-busy={loading}>
        {loading ? <Box role='status' className='flex items-center gap-3'><CircularProgress size={24} />{l('Đang tải danh mục…', 'Loading catalogs…')}</Box> : current?.error ?
          <Alert severity='error' action={<Button color='inherit' onClick={() => setRevision(x => x + 1)}>{l('Thử lại', 'Retry')}</Button>}>{message(current.error)}</Alert> :
          (current?.entries?.length ?? current?.targets?.items.length ?? 0) === 0 ? <Typography role='status'>{l('Không có dữ liệu trong danh mục này.', 'No entries in this catalog.')}</Typography> :
            <TableContainer><Table aria-label={names[group]} sx={{ minWidth: 520 }}>
              <TableHead><TableRow><TableCell>{l('Mã', 'Code')}</TableCell><TableCell>{l('Tên', 'Name')}</TableCell>
                <TableCell>{group === 'targets' ? l('Mapping', 'Mapping') : l('Thứ tự', 'Order')}</TableCell>
                {editable && <TableCell>{l('Thao tác', 'Actions')}</TableCell>}
              </TableRow></TableHead>
              <TableBody>{group === 'targets' ? current?.targets?.items.map(x => <TableRow key={x.id}>
                <TableCell>{x.initial ?? '—'}</TableCell><TableCell>{x.name}</TableCell><TableCell><Chip size='small' color='warning' label={l('Chờ xác nhận', 'Pending')} /></TableCell>
              </TableRow>) : current?.entries?.map(x => <TableRow key={x.id}>
                <TableCell>{x.code}</TableCell><TableCell>{x.name}</TableCell><TableCell>{x.sortOrder}</TableCell>
                {editable && <TableCell><Button aria-label={`${l('Sửa', 'Edit')}: ${x.code}`} onClick={() => open(x)}>{l('Sửa', 'Edit')}</Button></TableCell>}
              </TableRow>)}</TableBody>
            </Table></TableContainer>}
      </Box>
      {current?.targets && <TablePagination component='div' count={current.targets.totalCount} page={pageNumber - 1} rowsPerPage={pageSize}
        rowsPerPageOptions={[10, 20, 50]} labelRowsPerPage={l('Số dòng', 'Rows per page')}
        labelDisplayedRows={({ from, to, count }) => `${from}–${to} / ${count}`}
        getItemAriaLabel={type => type === 'next' ? l('Trang sau', 'Next page') : l('Trang trước', 'Previous page')}
        onPageChange={(_, value) => setPageNumber(value + 1)} onRowsPerPageChange={e => { setPageSize(Number(e.target.value)); setPageNumber(1) }} />}
    </CardContent>
    <Dialog open={!!editor} fullWidth maxWidth='sm' onClose={() => { if (!busy) setEditor(null) }}>
      <DialogTitle>{editor?.original ? l('Sửa danh mục', 'Edit entry') : l('Thêm danh mục', 'Add entry')}</DialogTitle>
      {editor && <Box component='form' onSubmit={e => { e.preventDefault(); void save() }}>
        <DialogContent className='flex flex-col gap-4'>
          {saveError && <Alert severity='error'>{saveError}</Alert>}
          {notice && <Alert severity='info'>{notice}</Alert>}
          <CustomTextField label={l('Mã', 'Code')} value={editor.code} disabled={busy || !!editor.original || mustReload}
            onChange={e => setEditor({ ...editor, code: e.target.value })} slotProps={{ htmlInput: { maxLength: 64 } }} />
          <CustomTextField label={l('Tên', 'Name')} value={editor.name} disabled={busy || mustReload}
            onChange={e => setEditor({ ...editor, name: e.target.value })} slotProps={{ htmlInput: { maxLength: 200 } }} />
          {editor.original && <>
            <CustomTextField label={l('Thứ tự', 'Order')} type='number' value={editor.sortOrder} disabled={busy || mustReload}
              onChange={e => setEditor({ ...editor, sortOrder: e.target.value })} slotProps={{ htmlInput: { min: 0, max: 10000, step: 1 } }} />
            <FormControlLabel label={l('Đang sử dụng', 'Active')} control={<Switch checked={editor.isActive} disabled={busy || mustReload} onChange={(_, checked) => setEditor({ ...editor, isActive: checked })} />} />
          </>}
        </DialogContent>
        <DialogActions>
          <Button disabled={busy} onClick={() => setEditor(null)}>{l('Đóng', 'Close')}</Button>
          {mustReload && <Button disabled={busy} onClick={() => void reloadEditor()}>{l('Tải dữ liệu mới', 'Reload data')}</Button>}
          <Button type='submit' variant='contained' disabled={busy || mustReload || !editable}>{busy ? l('Đang xử lý…', 'Processing…') : l('Lưu', 'Save')}</Button>
        </DialogActions>
      </Box>}
    </Dialog>
  </Card>
}

export default BusinessCatalogs
