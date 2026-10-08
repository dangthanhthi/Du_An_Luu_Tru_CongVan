import assert from 'node:assert/strict'
import { test } from 'node:test'
import { ApiRequestError } from '../../src/services/api'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const original = { id: 'partner-id', fullName: 'Original', entityType: 'Both', isActive: true, isDeleted: false, version: 1 }
function fixture(failure: ApiRequestError, reloadFailure?: ApiRequestError) {
  let updates = 0, revoked = 0
  const harness = componentHarness(new URL('../../src/views/apps/partners/list/AddPartnerDrawer.tsx', import.meta.url), {
    '@/hooks/useSessionIntent': { useSessionIntent: () => ({ assertCurrent() {} }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/api': { ApiRequestError },
    '@/services/das/external-entities': { externalEntityApi: {
      update: async () => { updates++; throw failure },
      getById: async () => { if (reloadFailure) throw reloadFailure; return { ...original, fullName: 'Server value', version: 2 } }
    } }
  })
  const render = () => harness.render({ original, canManage: true, onSaved() { assert.fail('Failed mutation cannot report success') }, onClose() {}, onRevoked() { revoked++ } })
  const submit = async () => nodes(render()).find(node => node.type === 'form')!.props.onSubmit({ preventDefault() {} })
  const save = () => nodes(render()).find(node => node.props.type === 'submit')!
  return { render, submit, save, updates: () => updates, revoked: () => revoked }
}
test('duplicate partner identity is editable, without forcing discard/version reload', async () => {
  const ui = fixture(new ApiRequestError(409, 'Duplicate', undefined, 'PARTNER_DUPLICATE'))
  await ui.submit()
  assert.equal(ui.save().props.disabled, false)
  assert.ok(!textContent(ui.render()).includes('Discard changes'))
})
test('version conflict requires an explicit authoritative reload before another update', async () => {
  const ui = fixture(new ApiRequestError(409, 'Changed', undefined, 'PARTNER_VERSION_CONFLICT'))
  await ui.submit()
  assert.equal(ui.save().props.disabled, true)
  const reload = nodes(ui.render()).find(node => textContent(node) === 'Discard changes & reload version')!
  await reload.props.onClick()
  assert.equal(ui.save().props.disabled, false)
  assert.equal(nodes(ui.render()).find(node => node.props.label === 'Full Name')!.props.value, 'Server value')
  assert.equal(ui.updates(), 1)
})
test('an unknown update result blocks resend and offers read-only reconciliation', async () => {
  const ui = fixture(new ApiRequestError(0, 'Network unavailable'))
  await ui.submit(); await ui.submit()
  assert.equal(ui.updates(), 1)
  assert.equal(ui.save().props.disabled, true)
  const reload = nodes(ui.render()).find(node => node.type === 'Button' && textContent(node).includes('reload'))!
  assert.ok(reload, 'Unknown update needs authoritative GET reconciliation')
  await reload.props.onClick()
  assert.equal(ui.save().props.disabled, false)
  assert.equal(ui.updates(), 1)
})
test('a revoked permission during conflict reload removes write authority', async () => {
  const ui = fixture(new ApiRequestError(409, 'Changed', undefined, 'PARTNER_VERSION_CONFLICT'), new ApiRequestError(403, 'Denied'))
  await ui.submit()
  await nodes(ui.render()).find(node => textContent(node) === 'Discard changes & reload version')!.props.onClick()
  assert.equal(ui.revoked(), 1)
  assert.equal(ui.save().props.disabled, true)
})
