import assert from 'node:assert/strict'
import { test } from 'node:test'

import { buildDasNavigation } from '../../src/components/layout/shared/dasNavigation'
import { buildDasLauncher, buildDasShortcuts, filterDasLauncher, getCachedCapabilityHints } from '../../src/components/layout/shared/dasLauncher'

const labels = {
  dashboard: 'Bảng Điều Khiển', overview: 'Tổng Quan', operations: 'Nghiệp Vụ', settings: 'Cấu Hình',
  incomingDocs: 'Công Văn Đến', outgoingDocs: 'Công Văn Đi', internalDocs: 'Công Văn Nội Bộ',
  registerNew: 'Đăng Ký Mới', allDocs: 'Tất Cả Công Văn', mineDocs: 'Công Văn Của Tôi',
  departmentDocs: 'Công Văn Của Phòng', cancelledDocs: 'Công Văn Đã Hủy',
  externalEntities: 'Cơ Quan / Đối Tác', organizationDirectory: 'Tổ Chức Và Nhân Sự',
  businessCatalogs: 'Danh Mục Nghiệp Vụ', emailIntegration: 'Email / Fax', incompleteReports: 'Hồ sơ chưa hoàn tất', myStaff: 'My Staff'
}
const items = (capabilities: string[] = [], lang = 'vi') => buildDasLauncher(lang, capabilities, labels).flatMap(s => s.items)
const navigationItems = (capabilities: string[], lang: string) => {
  const nav = buildDasNavigation({ pathname: '', search: '', lang, capabilities })

  return [nav.dashboard, ...nav.documentGroups.flatMap(g => g.items), nav.partners, ...nav.workspace, ...nav.settings]
}

test('Launcher destinations match implemented DAS navigation with My Staff and no template targets', () => {
  assert.deepEqual(items().map(i => i.url).sort(), navigationItems([], 'vi').map(i => i.href).sort())
  assert.equal(items().find(i => i.id === 'my-staff')?.url, '/vi/apps/tasks/my-staff')
})

test('All twelve document views keep distinct kind/view query and contextual names', () => {
  const docs = items().filter(i => i.url.includes('/documents/list'))

  assert.equal(docs.length, 12)
  assert.equal(new Set(docs.map(i => i.name)).size, 12)
  assert.equal(docs.find(i => i.id === 'Incoming-mine')?.name, 'Công Văn Đến — Công Văn Của Tôi')
})

test('Only matching capabilities expose registration and managed settings in the launcher', () => {
  assert.deepEqual(items(['DocumentRegisterIncoming']).filter(i => i.url.includes('/add')).map(i => i.url), [
    '/vi/apps/documents/add?kind=Incoming'
  ])
  const granted = ['DocumentRegisterDepartment', 'CatalogManage', 'MailboxManage']

  assert.deepEqual(items(granted).map(i => i.url).sort(), navigationItems(granted, 'vi').map(i => i.href).sort())
})

test('Role names never enable registration, personnel or permission editor template links', () => {
  assert.deepEqual(items(['Admin', 'Secretary', 'UserManage']).map(i => i.url), items().map(i => i.url))
})

test('Launcher URLs preserve every configured locale', () => {
  for (const lang of ['vi', 'en', 'fr', 'ar']) {
    assert.ok(items([], lang).every(i => i.url.startsWith(`/${lang}/`)))
  }
})

test('Fully granted launcher catalogue has unique IDs and destinations', () => {
  const all = items(['DocumentRegisterIncoming', 'DocumentRegisterDepartment', 'CatalogManage', 'MailboxManage'])

  assert.equal(new Set(all.map(i => i.id)).size, all.length)
  assert.equal(new Set(all.map(i => i.url)).size, all.length)
})

test('Search finds My Staff by case-insensitive name', () => {
  assert.deepEqual(filterDasLauncher(buildDasLauncher('vi', [], labels), ' my STAFF ').flatMap(s => s.items).map(i => i.url), [
    '/vi/apps/tasks/my-staff'
  ])
})

test('Vietnamese queries without accents find the document kind', () => {
  const results = filterDasLauncher(buildDasLauncher('vi', [], labels), 'cong van den').flatMap(s => s.items)

  assert.equal(results.length, 4)
  assert.ok(results.every(i => i.id.startsWith('Incoming-')))
  assert.equal(filterDasLauncher(buildDasLauncher('vi', [], labels), 'bang dieu khien')[0]?.items[0]?.id, 'dashboard')
})

test('An unknown query returns no results instead of unrelated template suggestions', () => {
  assert.deepEqual(filterDasLauncher(buildDasLauncher('vi', [], labels), 'xyz-no-such-function'), [])
})

test('Shortcuts include My Staff and only use destinations from the current catalogue', () => {
  const sections = buildDasLauncher('en', [], labels)
  const shortcuts = buildDasShortcuts(sections)
  const urls = new Set(sections.flatMap(s => s.items).map(i => i.url))

  assert.ok(shortcuts.some(i => i.url === '/en/apps/tasks/my-staff'))
  assert.ok(shortcuts.every(i => urls.has(i.url)))
})

test('Absent token and malformed cached metadata supply no capability presentation hints', () => {
  for (const stored of [null, [], 'Admin', 42, { capabilities: 'CatalogManage' }]) {
    assert.deepEqual(getCachedCapabilityHints(true, stored), [])
  }
  assert.deepEqual(getCachedCapabilityHints(false, { capabilities: ['CatalogManage'] }), [])
  assert.deepEqual(getCachedCapabilityHints(true, { role: 'Admin', capabilities: ['MailboxManage', null, 2, ''] }), ['MailboxManage'])
})
