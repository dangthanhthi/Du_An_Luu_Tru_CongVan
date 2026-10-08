'use client'
import { useEffect, useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import TextField from '@mui/material/TextField'
import Alert from '@mui/material/Alert'
import CircularProgress from '@mui/material/CircularProgress'
import { useAppDictionary } from '@/hooks/useDictionary'
import { documentApi } from '@/services/api'
import { externalEntityApi } from '@/services/das/external-entities'
import { isDocumentGuid } from '@/services/das/documents'
import type { DocumentKind } from '@/types/das/documents'
export type ReferenceOption = { id: string; name: string }

export default function V2ReferencePicker({
  label,
  value,
  onChange,
  disabled,
  documentKind,
  multiple = true,
  required = false,
  error = false,
  helperText,
  placeholder
}: {
  label: string
  value: ReferenceOption[]
  onChange: (items: ReferenceOption[]) => void
  disabled: boolean
  documentKind?: DocumentKind
  multiple?: boolean
  required?: boolean
  error?: boolean
  helperText?: string
  placeholder?: string
}) {
  const { isEn } = useAppDictionary()
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [result, setResult] = useState<{ key: string; options: ReferenceOption[]; error?: string }>({ key: '', options: [] })
  const key = `${documentKind ?? 'partner'}:${search}`

  useEffect(() => {
    if (disabled) return
    const controller = new AbortController()
    setLoading(true)
    const timer = setTimeout(async () => {
      try {
        let options: ReferenceOption[]

        if (documentKind) {
          const page = await documentApi.getScopedList({ kind: documentKind, view: 'all', searchTerm: search, pageSize: 20 }, controller.signal)

          options = page.items.map(x => ({ id: x.id, name: `${x.registrationNumber} — ${x.subject}` }))
        } else {
          const { items: rows } = await externalEntityApi.getList({ searchTerm: search, isActive: true, pageSize: 20 }, controller.signal)

          if (!rows.every((x: any) => x && isDocumentGuid(x.id) && x.isActive === true && typeof x.fullName === 'string' && x.fullName.trim())) throw new Error(isEn ? 'Invalid partner response payload.' : 'Phản hồi đối tác không hợp lệ.')
          options = rows.map((x: any) => ({ id: x.id, name: x.fullName }))
        }
        if (!controller.signal.aborted) setResult({ key, options })
      } catch (failure) {
        if (!controller.signal.aborted) setResult({ key, options: [], error: failure instanceof Error ? failure.message : (isEn ? 'Search failed.' : 'Không thể tra cứu.') })
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }, 250)

    return () => { clearTimeout(timer); controller.abort() }
  }, [disabled, key, search, documentKind, isEn])
  const current = result.key === key ? result : undefined
  const options = [...value, ...(current?.options ?? []).filter(x => !value.some(v => v.id === x.id))]

  return <div>
    <Autocomplete
      multiple={multiple}
      options={options}
      value={multiple ? value : value[0] ?? null}
      disabled={disabled}
      loading={loading}
      openText={isEn ? 'Open options' : 'Mở lựa chọn'}
      closeText={isEn ? 'Close options' : 'Đóng lựa chọn'}
      clearText={isEn ? 'Clear selection' : 'Xóa lựa chọn'}
      loadingText={isEn ? 'Searching...' : 'Đang tìm kiếm...'}
      noOptionsText={isEn ? 'No options found' : 'Không tìm thấy kết quả'}
      filterOptions={x => x}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      getOptionLabel={x => x.name}
      onInputChange={(_event, text, reason) => { if (reason === 'input' || reason === 'clear') setSearch(text) }}
      onChange={(_event, selected) => onChange(Array.isArray(selected) ? selected : selected ? [selected] : [])}
      renderInput={params => (
        <TextField
          {...params}
          label={label}
          required={required}
          error={error}
          helperText={helperText}
          placeholder={placeholder}
          InputProps={{
            ...params.InputProps,
            endAdornment: (
              <>
                {loading ? <CircularProgress color='inherit' size={20} /> : null}
                {params.InputProps.endAdornment}
              </>
            )
          }}
        />
      )}
    />
    {current?.error && <Alert severity='error' className='mbs-2'>{current.error}</Alert>}
  </div>
}
