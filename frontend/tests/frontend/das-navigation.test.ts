import assert from 'node:assert/strict'
import { test } from 'node:test'

import { buildDasNavigation } from '../../src/components/layout/shared/dasNavigation'

const context = {
  pathname: '/vi/apps/documents/list',
  search: '',
  lang: 'vi',
  capabilities: [] as string[]
}

const documentItems = (navigation: ReturnType<typeof buildDasNavigation>) =>
  navigation.documentGroups.flatMap(group => group.items)

for (const kind of ['Incoming', 'Outgoing', 'Internal']) {
  for (const view of ['all', 'mine', 'department', 'cancelled']) {
    test(`Only ${kind} ${view} is active on its document list query`, () => {
      const navigation = buildDasNavigation({ ...context, search: `?view=${view}&kind=${kind}&pageNumber=2` })
      const activeItems = documentItems(navigation).filter(item => item.active)

      assert.deepEqual(activeItems.map(item => item.href), [
        `/vi/apps/documents/list?kind=${kind}&view=${view}`
      ])
      assert.equal(navigation.activeItemId, activeItems[0].id)
    })
  }
}

test('Unknown capabilities leave all twelve read links visible and hide privileged links', () => {
  const navigation = buildDasNavigation(context)

  assert.deepEqual(navigation.documentGroups.map(group => group.kind), ['Incoming', 'Outgoing', 'Internal'])
  assert.equal(documentItems(navigation).length, 12)
  assert.equal(documentItems(navigation).some(item => item.href.includes('/add')), false)
  assert.deepEqual(navigation.settings.map(item => item.href), ['/vi/apps/settings/organization'])
  assert.equal(navigation.partners.href, '/vi/apps/partners/list')
})

test('Incoming register capability only enables Incoming registration', () => {
  const navigation = buildDasNavigation({ ...context, capabilities: ['DocumentRegisterIncoming'] })

  assert.deepEqual(documentItems(navigation).filter(item => item.href.includes('/add')).map(item => item.href), [
    '/vi/apps/documents/add?kind=Incoming'
  ])
})

test('Department register capability only enables Outgoing and Internal registration', () => {
  const navigation = buildDasNavigation({ ...context, capabilities: ['DocumentRegisterDepartment'] })

  assert.deepEqual(documentItems(navigation).filter(item => item.href.includes('/add')).map(item => item.href), [
    '/vi/apps/documents/add?kind=Outgoing',
    '/vi/apps/documents/add?kind=Internal'
  ])
})

test('Role names and unrelated capabilities do not grant register or privileged settings links', () => {
  const navigation = buildDasNavigation({ ...context, capabilities: ['Admin', 'Secretary', 'DocumentEdit'] })

  assert.equal(documentItems(navigation).some(item => item.href.includes('/add')), false)
  assert.deepEqual(navigation.settings.map(item => item.href), ['/vi/apps/settings/organization'])
})

test('MailboxManage and CatalogManage expose their implemented settings routes', () => {
  const navigation = buildDasNavigation({
    ...context,
    pathname: '/vi/apps/email-integration',
    capabilities: ['MailboxManage', 'CatalogManage', 'ReportViewAll', 'ReportViewDepartment']
  })

  assert.deepEqual(navigation.settings.map(item => item.href), [
    '/vi/apps/settings/organization',
    '/vi/apps/settings/catalogs',
    '/vi/apps/email-integration'
  ])
  assert.equal(navigation.settings.find(item => item.id === 'email-fax')?.active, true)
  assert.equal(navigation.activeItemId, 'email-fax')
})

test('CatalogManage alone grants the catalog settings link and exact active state', () => {
  const navigation = buildDasNavigation({ ...context, pathname: '/vi/apps/settings/catalogs', capabilities: ['CatalogManage'] })
  assert.deepEqual(navigation.settings.map(item => item.id), ['organization-directory', 'business-catalogs'])
  assert.equal(navigation.activeItemId, 'business-catalogs')
})

test('Register active state requires a matching kind and its capability', () => {
  const options = { ...context, pathname: '/vi/apps/documents/add', search: 'kind=Internal' }
  const unauthorized = buildDasNavigation(options)
  const authorized = buildDasNavigation({ ...options, capabilities: ['DocumentRegisterDepartment'] })

  assert.equal(unauthorized.activeItemId, null)
  assert.deepEqual(documentItems(authorized).filter(item => item.active).map(item => item.href), [
    '/vi/apps/documents/add?kind=Internal'
  ])
})

for (const search of [
  '',
  'view=all',
  'kind=Incoming',
  'kind=incoming&view=all',
  'kind=Unknown&view=all',
  'kind=Incoming&view=unknown',
  'kind=Incoming&kind=Outgoing&view=all',
  'kind=Incoming&view=all&view=mine'
]) {
  test(`Invalid or ambiguous document query has no active item: ${search || '(empty)'}`, () => {
    const navigation = buildDasNavigation({ ...context, search })

    assert.equal(navigation.activeItemId, null)
    assert.deepEqual(documentItems(navigation).filter(item => item.active), [])
    assert.equal(documentItems(navigation).some(item => item.href.includes('Unknown')), false)
  })
}

test('A detail or edit path never activates the list item from its query', () => {
  for (const pathname of ['/vi/apps/documents/server-id', '/vi/apps/documents/edit/server-id']) {
    const navigation = buildDasNavigation({ ...context, pathname, search: 'kind=Incoming&view=all' })

    assert.equal(navigation.activeItemId, null)
  }
})

test('All destinations retain the current locale, including configured locales without their own dictionary', () => {
  for (const lang of ['vi', 'en', 'fr', 'ar']) {
    const navigation = buildDasNavigation({
      ...context,
      lang,
      pathname: `/${lang}/apps/documents/list`,
      search: 'kind=Outgoing&view=mine',
      capabilities: ['DocumentRegisterIncoming', 'DocumentRegisterDepartment', 'MailboxManage']
    })
    const items = [navigation.dashboard, ...documentItems(navigation), navigation.partners, ...navigation.settings]

    assert.equal(items.every(item => item.href.startsWith(`/${lang}/`)), true)
    assert.deepEqual(items.filter(item => item.active).map(item => item.href), [
      `/${lang}/apps/documents/list?kind=Outgoing&view=mine`
    ])
  }
})

test('Dashboard and partner read links use exact paths without inferring document scope', () => {
  for (const pathname of ['/vi/dashboards/overview', '/vi/apps/partners/list']) {
    const navigation = buildDasNavigation({ ...context, pathname })
    const items = [navigation.dashboard, ...documentItems(navigation), navigation.partners, ...navigation.settings]

    assert.equal(items.filter(item => item.active).length, 1)
    assert.equal(items.find(item => item.active)?.href, pathname)
  }
})

test('Authenticated directory read link needs no management capability and never grants read-all or identity editors', () => {
  for (const capabilities of [[], ['UserManage'], ['ReportViewAll'], ['Admin']]) {
    const navigation = buildDasNavigation({ ...context, pathname: '/vi/apps/settings/organization', capabilities })

    assert.deepEqual(navigation.settings.map(item => item.href), ['/vi/apps/settings/organization'])
    assert.equal(navigation.settings[0].active, true)
    assert.equal(navigation.activeItemId, navigation.settings[0].id)
    assert.equal(documentItems(navigation).some(item => item.href.includes('/add')), false)
  }
})
