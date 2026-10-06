'use client'
import { useEffect, useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import TextField from '@mui/material/TextField'
import Alert from '@mui/material/Alert'
import { documentApi } from '@/services/api'
import { externalEntityApi } from '@/services/das/external-entities'
import { isDocumentGuid } from '@/services/das/documents'
import type { DocumentKind } from '@/types/das/documents'
export type ReferenceOption = { id: string; name: string }

export default function V2ReferencePicker({ label, value, onChange, disabled, documentKind, multiple = true }: {
  label: string; value: ReferenceOption[]; onChange: (items: ReferenceOption[]) => void; disabled: boolean; documentKind?: DocumentKind; multiple?: boolean
}) {
  const [search, setSearch] = useState('')
  const [result, setResult] = useState<{ key: string; options: ReferenceOption[]; error?: string }>({ key: '', options: [] })
  const key = `${documentKind ?? 'partner'}:${search}`

  useEffect(() => {
    if (disabled) return
    const controller = new AbortController()
    const timer = setTimeout(async () => {
      try {
        let options: ReferenceOption[]

        if (documentKind) {
          const page = await documentApi.getScopedList({ kind: documentKind, view: 'all', searchTerm: search, pageSize: 20 }, controller.signal)

          options = page.items.map(x => ({ id: x.id, name: `${x.registrationNumber} — ${x.subject}` }))
        } else {
          const { items: rows } = await externalEntityApi.getList({ searchTerm: search, isActive: true, pageSize: 20 }, controller.signal)

          if (!rows.every((x: any) => x && isDocumentGuid(x.id) && x.isActive === true && typeof x.fullName === 'string' && x.fullName.trim())) throw new Error('Phản hồi đối tác không hợp lệ.')
          options = rows.map((x: any) => ({ id: x.id, name: x.fullName }))
        }
        if (!controller.signal.aborted) setResult({ key, options })
      } catch (failure) {
        if (!controller.signal.aborted) setResult({ key, options: [], error: failure instanceof Error ? failure.message : 'Không thể tra cứu.' })
      }
    }, 250)

    return () => { clearTimeout(timer); controller.abort() }
  }, [disabled, key, search, documentKind])
  const current = result.key === key ? result : undefined
  const options = [...value, ...(current?.options ?? []).filter(x => !value.some(v => v.id === x.id))]

  return <div>
    <Autocomplete multiple={multiple} options={options} value={multiple ? value : value[0] ?? null} disabled={disabled}
      filterOptions={x => x} isOptionEqualToValue={(a, b) => a.id === b.id} getOptionLabel={x => x.name}
      onInputChange={(_event, text, reason) => { if (reason === 'input' || reason === 'clear') setSearch(text) }}
      onChange={(_event, selected) => onChange(Array.isArray(selected) ? selected : selected ? [selected] : [])}
      renderInput={params => <TextField {...params} label={label} />} />
    {current?.error && <Alert severity='error' className='mbs-2'>{current.error}</Alert>}
  </div>
}
