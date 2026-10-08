import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { catalogGroups, editableCatalogGroups } from '../../src/types/das/catalogs'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))
const entry = { id: 'catalog-fixture', group: 'methods', code: 'EMAIL', name: 'Email fixture', version: 7, sortOrder: 2, isActive: true }
function fixture(overrides: any = {}, manage = true) {
  const calls: any[] = []
  const api = {
    getGroup: async (group: string) => group === 'methods' ? [entry] : [],
    getById: async () => ({ ...entry, version: 8, name: 'Authoritative name' }),
    create: async (...args: any[]) => { calls.push({ mode: 'create', args }); return entry },
    update: async (...args: any[]) => { calls.push({ mode: 'update', args }); return { ...entry, version: 8 } },
    ...overrides
  }
  const ui = componentHarness(new URL('../../src/views/apps/settings/BusinessCatalogs.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/api': { tokenManager: { getUser: () => ({ capabilities: manage ? ['CatalogManage'] : [] }) } },
    '@/services/das/http': { V2ApiError: ApiRequestError },
    '@/services/das/catalogs': { catalogApi: api },
    '@/types/das/catalogs': { catalogGroups, editableCatalogGroups },
    '@core/components/mui/TextField': { default: 'CustomTextField' }
  }, { Error })
  const render = () => ui.render()
  const button = (label: string) => nodes(render()).find(n => n.type === 'Button' && textContent(n) === label)!
  const set = (label: string, value: string) => nodes(render()).find(n => n.props.label === label)!.props.onChange({ target: { value } })
  const submit = () => nodes(render()).find(n => n.type === 'Box' && n.props.component === 'form')!.props.onSubmit({ preventDefault() {} })
  render(); ui.commit()
  const methods = async () => { nodes(render()).find(n => n.type === 'Tabs')!.props.onChange(null, 'methods'); render(); ui.commit(); await tick() }
  return { ui, render, button, set, submit, calls, methods }
}

test('Catalog fixed groups and a reader never expose the create control', async () => {
  const f = fixture(); await tick()
  assert.ok(textContent(f.render()).includes('fixed in this release'))
  assert.equal(f.button('Add entry'), undefined)
  f.ui.unmount()
  const reader = fixture({}, false); await reader.methods()
  assert.equal(reader.button('Add entry'), undefined)
  assert.ok(textContent(reader.render()).includes('CatalogManage is required'))
  reader.ui.unmount()
})

test('Catalog create reloads its group and edit preserves server identity while deactivating', async () => {
  const f = fixture(); await f.methods()
  f.button('Add entry').props.onClick(); f.set('Code', 'LOCAL'); f.set('Name', 'Local fixture')
  f.submit(); await tick()
  assert.equal(f.calls[0].mode, 'create'); assert.equal(f.calls[0].args[0].group, 'methods')
  assert.equal(f.calls[0].args[0].code, 'LOCAL')
  f.render(); f.ui.commit(); await tick()
  nodes(f.render()).find(n => n.props['aria-label'] === 'Edit: EMAIL')!.props.onClick()
  f.set('Name', 'Changed fixture')
  nodes(f.render()).find(n => n.type === 'FormControlLabel')!.props.control.props.onChange(null, false)
  f.submit(); await tick()
  assert.equal(f.calls[1].mode, 'update'); assert.equal(f.calls[1].args[0].version, 7)
  assert.equal(f.calls[1].args[1].isActive, false); assert.equal(f.calls[1].args[1].name, 'Changed fixture')
  assert.ok(textContent(f.render()).includes('Catalog saved'))
  f.ui.unmount()
})

test('Catalog duplicate create remains editable while stale update requires a server reload', async () => {
  const f = fixture({ create: async () => { throw new ApiRequestError(409, 'Duplicate') }, update: async () => { throw new ApiRequestError(409, 'Stale') } })
  await f.methods(); f.button('Add entry').props.onClick(); f.set('Code', 'EMAIL'); f.set('Name', 'Another fixture')
  f.submit(); await tick()
  assert.equal(f.button('Save').props.disabled, false); assert.ok(textContent(f.render()).includes('code already exists'))
  f.button('Close').props.onClick(); nodes(f.render()).find(n => n.props['aria-label'] === 'Edit: EMAIL')!.props.onClick()
  f.submit(); await tick()
  assert.equal(f.button('Save').props.disabled, true)
  f.button('Reload data').props.onClick(); await tick()
  assert.equal(f.button('Save').props.disabled, false)
  assert.equal(nodes(f.render()).find(n => n.props.label === 'Name')!.props.value, 'Authoritative name')
  f.ui.unmount()
})

test('Catalog request ownership aborts the old group and cannot reveal its late contents', async () => {
  let resolve!: (value: any) => void
  const pending = new Promise(r => { resolve = r })
  const signals: AbortSignal[] = []
  const f = fixture({ getGroup: (group: string, signal: AbortSignal) => { signals.push(signal); return group === 'companies' ? pending : Promise.resolve([entry]) } })
  await f.methods(); assert.equal(signals[0].aborted, true)
  resolve([{ ...entry, name: 'Stale private name' }]); await tick()
  assert.ok(!textContent(f.render()).includes('Stale private name'))
  assert.ok(textContent(f.render()).includes('Email fixture'))
  f.ui.unmount(); assert.equal(signals[1].aborted, true)
})
