import assert from 'node:assert/strict'
import { test, afterEach } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
import { ApiRequestError } from '../../src/services/api'
import { catalogGroups, editableCatalogGroups } from '../../src/types/das/catalogs'
import { directoryApi } from '../../src/services/das/directory'

const tick = () => new Promise(r => setImmediate(r))
function deferred<T>() { let resolve!: (v: T) => void; let reject!: (e: unknown) => void; return { promise: new Promise<T>((r, j) => { resolve = r; reject = j }), resolve: (v: T) => resolve(v), reject: (e: unknown) => reject(e) } }
const source = (name: string) => new URL('../../src/' + name, import.meta.url)
const dictionary = { useAppDictionary: () => ({ isEn: true, t: { email: new Proxy({}, { get: (_o, k) => String(k) }) } }) }
const intent = { useSessionIntent: () => ({ assertCurrent() {} }) }
const button = (tree: any, label: string) => nodes(tree).find(n => n.type === 'Button' && textContent(n) === label)
const submitEvent = { preventDefault() {} }
const catalog = { id: '11111111-1111-4111-8111-111111111111', group: 'methods', code: 'EMAIL', name: 'Synthetic entry', version: 7, sortOrder: 0, isActive: true }

function emailFixture(blockWrite = false, blockRemove = false, response: any = { status: 503, success: false, code: 'INTEGRATION_DEFERRED' }) {
  const store = new Map<string, string>([['das_email_settings', JSON.stringify({ email: 'synthetic@example.test', appPassword: 'FAKE_NOT_A_CREDENTIAL' })]])
  let createCalls = 0
  const ui = componentHarness(source('views/apps/email-integration/index.tsx'), {
    '@/hooks/useDictionary': dictionary,
    '@core/components/mui/TextField': { default: 'CustomTextField' },
    '@/services/api': { documentApi: { create: async () => { createCalls++; return { success: false } } } }
  }, {
    Error,
    crypto: { randomUUID: () => 'synthetic-log' },
    localStorage: {
      getItem: (key: string) => store.get(key) ?? null,
      removeItem: (key: string) => {
        if (blockRemove) throw new Error('Synthetic storage remove denied')
        store.delete(key)
      },
      setItem(key: string, value: string) {
        if (blockWrite) throw new Error('Synthetic storage write denied')
        store.set(key, value)
      }
    },
    fetch: async () => ({ status: response.status, ok: response.status === 200, json: async () => response })
  })
  ui.render()
  ui.commit()
  return { ui, store, creates: () => createCalls }
}

test('R01-CANONICAL: normal mount removes fake credential from storage', () => {
  const f = emailFixture()
  assert.ok(!f.store.get('das_email_settings')!.includes('appPassword'))
  f.ui.unmount()
})

test('R01-CANONICAL: denied rewrite falls back to removeItem', () => {
  const f = emailFixture(true, false)
  assert.equal(f.store.has('das_email_settings'), false)
  f.ui.unmount()
})

test('R01-CANONICAL: both rewrite and remove denied triggers cleanup error Alert', () => {
  const f = emailFixture(true, true)
  const persists = Boolean(f.store.get('das_email_settings')?.includes('appPassword'))
  const warning = nodes(f.ui.render()).some(n => n.type === 'Alert' && /cleanup (failed|failure)|could not (clear|remove)|unable to (clear|remove)|credential.*(remain|failed)/i.test(textContent(n)))
  assert.equal(persists && warning, true)
  f.ui.unmount()
})

test('R02-CANONICAL: 503 deferred response stops scan without document creation or local logs', async () => {
  const f = emailFixture()
  await button(f.ui.render(), 'scanNow')!.props.onClick()
  assert.equal(f.creates(), 0)
  assert.equal(f.store.has('das_email_logs'), false)
  assert.ok(textContent(f.ui.render()).includes('503 INTEGRATION_DEFERRED'))
  f.ui.unmount()
})

test('R02-CANONICAL: unexpected 200 response cannot create documents or persist fake success logs', async () => {
  const f = emailFixture(false, false, {
    status: 200,
    success: true,
    items: [{ messageId: 'synth-msg', sender: 'test@example.com', subject: 'Test', attachment: 'test.pdf' }]
  })
  await button(f.ui.render(), 'scanNow')!.props.onClick()
  assert.equal(f.creates(), 0)
  const logs = JSON.parse(f.store.get('das_email_logs') ?? '[]')
  assert.equal(logs.some((l: any) => l.status === 'success' || l.docNumber === 'CV-DEN-2026-AUTO'), false)
  f.ui.unmount()
})

function catalogFixture(overrides: any = {}) {
  const creates: any[] = [], targets: any[] = []
  const ui = componentHarness(source('views/apps/settings/BusinessCatalogs.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { tokenManager: { getUser: () => ({ capabilities: ['CatalogManage'] }) } },
    '@/services/das/http': { V2ApiError: ApiRequestError },
    '@/types/das/catalogs': { catalogGroups, editableCatalogGroups },
    '@core/components/mui/TextField': { default: 'CustomTextField' },
    '@/services/das/catalogs': {
      catalogApi: {
        getGroup: async () => [catalog],
        create: async (...a: any[]) => { creates.push(a); return catalog },
        getDistributionTargets: async (query: any) => {
          targets.push(query)
          return {
            items: query.pageNumber === 1 ? Array.from({ length: 20 }, (_, i) => ({
              id: `33333333-3333-4333-8333-${String(i + 1).padStart(12, '0')}`,
              name: `Target ${i}`,
              initial: null,
              mappingState: 'Pending',
              version: 1
            })) : [],
            pageNumber: query.pageNumber,
            pageSize: query.pageSize,
            totalCount: query.pageNumber === 1 ? 21 : 0
          }
        },
        ...overrides
      }
    }
  }, { Error })
  ui.render()
  ui.commit()
  const change = async (group: string) => {
    nodes(ui.render()).find(n => n.type === 'Tabs')!.props.onChange(null, group)
    ui.render()
    ui.commit()
    await tick()
  }
  const set = (label: string, value: string) => nodes(ui.render()).find(n => n.props.label === label)!.props.onChange({ target: { value } })
  return { ui, creates, targets, change, set }
}

test('R03-CATALOG-CANONICAL: duplicate submit dispatches exactly one create', async () => {
  const reply = deferred<any>()
  let calls = 0
  const f = catalogFixture({ create: () => { calls++; return reply.promise } })
  await f.change('methods')
  button(f.ui.render(), 'Add entry')!.props.onClick()
  f.set('Code', 'LOCAL')
  f.set('Name', 'Synthetic')
  const handler = nodes(f.ui.render()).find(n => n.type === 'Box' && n.props.component === 'form')!.props.onSubmit
  handler(submitEvent)
  handler(submitEvent)
  try {
    assert.equal(calls, 1)
  } finally {
    reply.resolve(catalog)
    await tick()
    f.ui.unmount()
  }
})

test('R04-CANONICAL: catalog reload403 revokes canManage', async () => {
  const f = catalogFixture({
    update: async () => { throw new ApiRequestError(409, 'Synthetic conflict') },
    getById: async () => { throw new ApiRequestError(403, 'Synthetic denial') }
  })
  await f.change('methods')
  nodes(f.ui.render()).find(n => n.props['aria-label'] === 'Edit: EMAIL')!.props.onClick()
  nodes(f.ui.render()).find(n => n.type === 'Box' && n.props.component === 'form')!.props.onSubmit(submitEvent)
  await tick()
  button(f.ui.render(), 'Reload data')!.props.onClick()
  await tick()
  button(f.ui.render(), 'Close')!.props.onClick()
  assert.equal(!!button(f.ui.render(), 'Add entry'), false)
  f.ui.unmount()
})

test('R05-CATALOG-CANONICAL: zero-total resets query page to 1', async () => {
  const f = catalogFixture()
  await f.change('targets')
  nodes(f.ui.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1)
  f.ui.render(); f.ui.commit(); await tick(); f.ui.render(); f.ui.commit(); await tick()
  assert.equal(nodes(f.ui.render()).find(n => n.type === 'TablePagination')!.props.page, 0)
  assert.equal(f.targets.at(-1).pageNumber, 1)
  f.ui.unmount()
})

test('R03-PARTNER-CANONICAL: partner duplicate submit dispatches exactly one POST', async () => {
  const reply = deferred<any>()
  let calls = 0
  const ui = componentHarness(source('views/apps/partners/list/AddPartnerDrawer.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    '@/services/das/external-entities': { externalEntityApi: { create: () => { calls++; return reply.promise } } }
  }, { Error })
  const render = () => ui.render({ original: null, canManage: true, onSaved() {}, onClose() {}, onRevoked() {} })
  nodes(render()).find(n => n.props.label === 'Full Name')!.props.onChange({ target: { value: 'Synthetic' } })
  const handler = nodes(render()).find(n => n.type === 'form')!.props.onSubmit
  const p1 = handler(submitEvent), p2 = handler(submitEvent)
  try {
    assert.equal(calls, 1)
  } finally {
    reply.resolve({})
    await Promise.all([p1, p2])
    ui.unmount()
  }
})

test('R05-PARTNER-CANONICAL: shrinking partner total resets page to 0', async () => {
  const timers: any[] = [], queries: any[] = []
  const ui = componentHarness(source('views/apps/partners/list/PartnerListTable.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    './AddPartnerDrawer': { default: 'PartnerDrawer' },
    '@core/styles/table.module.css': { default: { table: 'synthetic' } },
    '@/services/das/external-entities': {
      externalEntityApi: {
        getOptions: async () => ({ canManage: true }),
        getList: async (q: any) => {
          queries.push(q)
          return {
            items: q.pageNumber === 1 ? Array.from({ length: 10 }, (_, i) => ({
              id: `44444444-4444-4444-8444-${String(i + 1).padStart(12, '0')}`,
              fullName: `Partner ${i}`,
              entityType: 'Both',
              isActive: true,
              isDeleted: false,
              version: 1
            })) : [],
            pageNumber: q.pageNumber,
            pageSize: q.pageSize,
            totalCount: q.pageNumber === 1 ? 11 : 0
          }
        }
      }
    }
  }, { Error, setTimeout: (work: any) => { timers.push(work); return work }, clearTimeout() {} })
  ui.render(); ui.commit(); await timers.shift()()
  nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1)
  ui.render(); ui.commit(); await timers.shift()(); ui.render(); ui.commit()
  if (timers.length) { await timers.shift()(); ui.render(); ui.commit() }
  assert.equal(nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.page, 0)
  assert.equal(queries.at(-1).pageNumber, 1)
  ui.unmount()
})

const savedDoc = {
  header: {
    id: '22222222-2222-4222-8222-222222222222',
    kind: 'Internal',
    status: 'InProgress',
    version: 7,
    subject: 'Synthetic',
    registrationNumber: '27-01-0007/HL/ADM',
    companyCode: 'HL',
    allowedActions: ['Distribute', 'Cancel', 'Restore']
  },
  recipients: [],
  relatedDocumentIds: [],
  details: null,
  pdfState: 'None'
}

test('R03-STATUS-CANONICAL: repeated status dispatch issues exactly one request', async () => {
  const reply = deferred<any>()
  let calls = 0
  const ui = componentHarness(source('views/apps/documents/detail/index.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    'next/navigation': { useParams: () => ({ lang: 'en' }) },
    'next/link': { default: 'Link' },
    '../V2PdfPanel': { default: 'PdfPanel' },
    '../DocumentTaskPanel': { default: 'TaskPanel' },
    '@/services/das/documents': { documentsV2Api: { detail: async () => savedDoc, status: () => { calls++; return reply.promise } } }
  }, { Error, Event, window: { dispatchEvent() {} } })
  ui.render(); ui.commit(); await tick()
  const handler = button(ui.render(), 'Distribute')!.props.onClick
  const p1 = handler(), p2 = handler()
  try {
    assert.equal(calls, 1)
  } finally {
    reply.resolve({})
    await Promise.all([p1, p2])
    ui.unmount()
  }
})

test('R06-CANONICAL: cancel 400 does not mention recipient distribution errors', async () => {
  const ui = componentHarness(source('views/apps/documents/detail/index.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    'next/navigation': { useParams: () => ({ lang: 'en' }) },
    'next/link': { default: 'Link' },
    '../V2PdfPanel': { default: 'PdfPanel' },
    '../DocumentTaskPanel': { default: 'TaskPanel' },
    '@/services/das/documents': {
      documentsV2Api: {
        detail: async () => savedDoc,
        status: async () => { throw new ApiRequestError(400, 'Synthetic invalid cancellation') }
      }
    }
  }, { Error, Event, window: { dispatchEvent() {} } })
  ui.render(); ui.commit(); await tick()
  button(ui.render(), 'Cancel Document')!.props.onClick()
  nodes(ui.render()).find(n => n.props.label === 'Cancellation reason')!.props.onChange({ target: { value: 'Reason' } })
  await button(ui.render(), 'Confirm Cancel')!.props.onClick()
  const dialog = nodes(ui.render()).find(n => n.type === 'Dialog')!
  assert.ok(!textContent(dialog).includes('edit the document to add recipients'))
  assert.ok(textContent(dialog).includes('Cancellation request is invalid'))
  ui.unmount()
})

test('R03-PDF-CANONICAL: repeated file input dispatches single upload', async () => {
  const reply = deferred<any>()
  let uploads = 0
  const ui = componentHarness(source('views/apps/documents/V2PdfPanel.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/components/DocumentPDFPreview': { default: 'PdfPreview' },
    '@/services/das/document-pdf': {
      documentPdfApi: {
        upload: () => { uploads++; return reply.promise },
        uploadInfo: async () => ({ state: 'Available', canAttach: true }),
        replace: async () => ({})
      }
    }
  }, { Error, crypto: { randomUUID: () => 'synthetic-op' } })
  const handler = nodes(ui.render({
    document: { ...savedDoc, header: { ...savedDoc.header, allowedActions: ['ReplacePdf'] } },
    onChange() {}
  })).find(n => n.type === 'input')!.props.onChange
  handler({ target: { files: [{}], value: 'f' } })
  handler({ target: { files: [{}], value: 'f' } })
  try {
    assert.equal(uploads, 1)
  } finally {
    reply.resolve({ id: 'file-1' })
    await tick()
    ui.unmount()
  }
})

test('R07-CANONICAL: read completion on page 1 does not revert current page 2', async () => {
  const requests: number[] = [], reply = deferred<any>()
  let reads = 0
  const ui = componentHarness(source('components/layout/shared/DasNotificationsDropdown.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    'next/link': { default: 'Link' },
    '@/services/das/notifications': {
      notificationsApi: {
        list: async (p: number) => {
          requests.push(p)
          return {
            items: [{ id: 'notif-1', title: 'Page ' + p, message: 'Msg', isRead: false, actionUrl: null, createdAt: '2026-10-07T00:00:00Z' }],
            totalCount: 30
          }
        },
        unread: async () => 1,
        read: () => { reads++; return reply.promise },
        readAll: () => { reads++; return reply.promise }
      }
    }
  }, { Error, setInterval: () => 1, clearInterval() {} })
  ui.render(); ui.commit(); await tick()
  const pending = button(ui.render(), 'Mark all')!.props.onClick()
  button(ui.render(), 'Next')!.props.onClick()
  ui.render(); ui.commit(); await tick()
  assert.equal(requests.at(-1), 2)
  reply.resolve({})
  await pending
  assert.equal(requests.at(-1), 2)
  ui.unmount()
})

test('R03-NOTIFICATION-CANONICAL: read callback dispatches single mutation before render', async () => {
  const requests: number[] = [], reply = deferred<any>()
  let reads = 0
  const ui = componentHarness(source('components/layout/shared/DasNotificationsDropdown.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    'next/link': { default: 'Link' },
    '@/services/das/notifications': {
      notificationsApi: {
        list: async (p: number) => {
          requests.push(p)
          return { items: [], totalCount: 0 }
        },
        unread: async () => 1,
        read: () => { reads++; return reply.promise },
        readAll: () => { reads++; return reply.promise }
      }
    }
  }, { Error, setInterval: () => 1, clearInterval() {} })
  ui.render(); ui.commit(); await tick()
  const handler = button(ui.render(), 'Mark all')!.props.onClick
  const p1 = handler(), p2 = handler()
  try {
    assert.equal(reads, 1)
  } finally {
    reply.resolve({})
    await Promise.all([p1, p2])
    ui.unmount()
  }
})

const originalFetch = globalThis.fetch
afterEach(() => { globalThis.fetch = originalFetch })
const unit = { id: '11111111-1111-4111-8111-111111111111', name: 'Synthetic ADM', code: 'ADM', parentId: null, isDepartment: true, isActive: true }
const dirPage = { items: [unit], pageNumber: 1, pageSize: 20, totalCount: 1, authorizationRevision: 1, verifiedAt: '2026-10-07T00:00:00Z' }
const reject502 = (error: unknown) => error instanceof Error && (error as { status?: number }).status === 502

test('R08-CANONICAL: directory rejects wrong pageNumber with 502', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { ...dirPage, pageNumber: 2 } })
  await assert.rejects(directoryApi.getDepartments({ pageNumber: 1 }), reject502)
})

test('R08-CANONICAL: directory rejects widened pageSize with 502', async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { ...dirPage, pageSize: 100 } })
  await assert.rejects(directoryApi.getDepartments({ pageSize: 20 }), reject502)
})

test('R08-CANONICAL: directory rejects duplicate user IDs with 502', async () => {
  globalThis.fetch = async () => Response.json({
    success: true,
    data: {
      ...dirPage,
      totalCount: 2,
      items: [{ id: unit.id, displayName: 'User A' }, { id: unit.id, displayName: 'User A' }]
    }
  })
  await assert.rejects(directoryApi.getUsers({ departmentId: unit.id, purpose: 'originator' }), reject502)
})

test('R08-CANONICAL: directory rejects cardinality exceeding pageSize with 502', async () => {
  globalThis.fetch = async () => Response.json({
    success: true,
    data: { ...dirPage, pageSize: 1, totalCount: 2, items: [unit, { ...unit, id: '22222222-2222-4222-8222-222222222222' }] }
  })
  await assert.rejects(directoryApi.getDepartments({ pageSize: 1 }), reject502)
})

// --- S01-S06 CANONICAL REGRESSIONS ---

test('S01-CANONICAL: unexpected email test 200 does not report successful connection when deferred', async () => {
  let fetches = 0
  const ui = componentHarness(source('views/apps/email-integration/index.tsx'), {
    '@/hooks/useDictionary': dictionary,
    '@core/components/mui/TextField': { default: 'CustomTextField' },
    '@/services/api': { documentApi: { create: async () => { assert.fail('Deferred email cannot create') } } }
  }, {
    Error,
    crypto: { randomUUID: () => 'synthetic-log' },
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    fetch: async () => { fetches++; return { status: 200, ok: true, json: async () => ({ success: true, message: 'Synthetic connection success' }) } }
  })
  ui.render(); ui.commit()
  try {
    await button(ui.render(), 'testConnection')!.props.onClick()
    assert.equal(nodes(ui.render()).some(n => n.type === 'Alert' && n.props.severity === 'success'), false)
  } finally { ui.unmount() }
})

test('S02-CANONICAL: scan and test controls disabled and callbacks short-circuited under integration deferral', async () => {
  let fetches = 0
  const ui = componentHarness(source('views/apps/email-integration/index.tsx'), {
    '@/hooks/useDictionary': dictionary,
    '@core/components/mui/TextField': { default: 'CustomTextField' },
    '@/services/api': { documentApi: { create: async () => { assert.fail('Deferred email cannot create') } } }
  }, {
    Error,
    crypto: { randomUUID: () => 'synthetic-log' },
    localStorage: { getItem: () => null, setItem() {}, removeItem() {} },
    fetch: async () => { fetches++; return { status: 200, ok: true, json: async () => ({ success: true }) } }
  })
  ui.render(); ui.commit()
  try {
    assert.equal(Boolean(button(ui.render(), 'scanNow')!.props.disabled), true)
    assert.equal(Boolean(button(ui.render(), 'testConnection')!.props.disabled), true)
    await button(ui.render(), 'scanNow')!.props.onClick()
    await button(ui.render(), 'testConnection')!.props.onClick()
    assert.equal(fetches, 0)
  } finally { ui.unmount() }
})

test('S03-CANONICAL: denied storage read and removal reports that legacy credential cleanup cannot be verified', () => {
  const ui = componentHarness(source('views/apps/email-integration/index.tsx'), {
    '@/hooks/useDictionary': dictionary,
    '@core/components/mui/TextField': { default: 'CustomTextField' },
    '@/services/api': { documentApi: { create: async () => { assert.fail('Deferred email cannot create') } } }
  }, {
    Error,
    crypto: { randomUUID: () => 'synthetic-log' },
    localStorage: {
      getItem: () => { throw new Error('Synthetic storage read denied') },
      setItem() {},
      removeItem: () => { throw new Error('Synthetic storage remove denied') }
    },
    fetch: async () => ({ status: 200, ok: true, json: async () => ({}) })
  })
  ui.render(); ui.commit()
  try {
    const warning = nodes(ui.render()).some(n => n.type === 'Alert' && /cleanup.*(failed|failure|unable|verify)|could not.*(clear|read)|unable.*(clear|read)|cannot.*(verify|read)/i.test(textContent(n)))
    assert.equal(warning, true)
  } finally { ui.unmount() }
})

for (const status of [409, 500]) {
  test(`S04-CANONICAL-${status}: required reload must prevent another lifecycle mutation before reconciliation`, async () => {
    let calls = 0
    const ui = componentHarness(source('views/apps/documents/detail/index.tsx'), {
      '@/hooks/useSessionIntent': intent,
      '@/hooks/useDictionary': dictionary,
      '@/services/api': { ApiRequestError },
      'next/navigation': { useParams: () => ({ lang: 'en' }) },
      'next/link': { default: 'Link' },
      '../V2PdfPanel': { default: 'PdfPanel' },
      '../DocumentTaskPanel': { default: 'TaskPanel' },
      '@/services/das/documents': {
        documentsV2Api: {
          detail: async () => savedDoc,
          status: async () => { calls++; throw new ApiRequestError(status, 'Synthetic failure') }
        }
      }
    }, { Error, Event, window: { dispatchEvent() {} } })
    ui.render(); ui.commit()
    await tick()
    try {
      await button(ui.render(), 'Distribute')!.props.onClick()
      assert.ok(textContent(ui.render()).toLowerCase().includes('reload'))
      await button(ui.render(), 'Distribute')!.props.onClick()
      assert.equal(calls, 1, 'Callback sends a second mutation before a required reload')
    } finally { ui.unmount() }
  })
}

test('S05-CANONICAL: Restore 400 fallback copy must not claim that restoration always targets InProgress', async () => {
  const doc = { ...savedDoc, header: { ...savedDoc.header, status: 'Cancelled', allowedActions: ['Restore'] } }
  const ui = componentHarness(source('views/apps/documents/detail/index.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    'next/navigation': { useParams: () => ({ lang: 'en' }) },
    'next/link': { default: 'Link' },
    '../V2PdfPanel': { default: 'PdfPanel' },
    '../DocumentTaskPanel': { default: 'TaskPanel' },
    '@/services/das/documents': {
      documentsV2Api: {
        detail: async () => doc,
        status: async () => { throw new ApiRequestError(400, '') }
      }
    }
  }, { Error, Event, window: { dispatchEvent() {} } })
  ui.render(); ui.commit()
  await tick()
  try {
    await button(ui.render(), 'Restore')!.props.onClick()
    const text = textContent(ui.render())
    assert.ok(text.includes('Restoration request is invalid'))
    assert.equal(text.includes('restored to in-progress status'), false)
  } finally { ui.unmount() }
})

test('S06-CANONICAL: partner clamp invalidates stale first-page cache when dataset shrinks to zero', async () => {
  const timers: (() => any)[] = [], requests: number[] = []
  const delayedCorrection = deferred<any>()
  const pageData = (page: number, shrunk = false) => ({
    items: page === 1 && !shrunk ? Array.from({ length: 10 }, (_, i) => ({
      id: `44444444-4444-4444-8444-${String(i + 1).padStart(12, '0')}`,
      fullName: 'Synthetic partner',
      entityType: 'Both',
      isActive: true,
      isDeleted: false,
      version: 1
    })) : [],
    pageNumber: page,
    pageSize: 10,
    totalCount: page === 1 && !shrunk ? 11 : 0
  })
  const ui = componentHarness(source('views/apps/partners/list/PartnerListTable.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    './AddPartnerDrawer': { default: 'PartnerDrawer' },
    '@core/styles/table.module.css': { default: { table: 'synthetic' } },
    '@/services/das/external-entities': {
      externalEntityApi: {
        getOptions: async () => ({ canManage: true }),
        getList: async (q: any) => {
          requests.push(q.pageNumber)
          if (requests.length === 3) return delayedCorrection.promise
          return pageData(q.pageNumber, requests.length > 1)
        }
      }
    }
  }, { Error, setTimeout: (work: () => any) => { timers.push(work); return work }, clearTimeout() {} })
  try {
    ui.render(); ui.commit(); await timers.shift()!()
    nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1)
    ui.render(); ui.commit()
    const oldPageLoad = timers.shift()!()
    await tick()
    ui.render(); ui.commit()
    let correctedLoad: any
    if (timers.length) correctedLoad = timers.shift()!()
    delayedCorrection.resolve(pageData(1, true))
    await Promise.all([oldPageLoad, correctedLoad])
    ui.render(); ui.commit()
    assert.equal(nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.page, 0)
    assert.equal(nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.count, 0)
    assert.equal(textContent(ui.render()).includes('Synthetic partner'), false)
  } finally {
    delayedCorrection.resolve(pageData(1, true))
    ui.unmount()
  }
})

// --- T01-T03 CANONICAL OWNER REGRESSIONS ---

const partnerItem = (name: string) => ({ id: '44444444-4444-4444-8444-000000000001', fullName: name, entityType: 'Both', isActive: true, isDeleted: false, version: 1 })

async function partnerOwnerFixture() {
  const timers: (() => any)[] = [], requests: { query: any; signal?: AbortSignal }[] = []
  const corrected = deferred<any>()
  const page = (q: any, empty = false) => ({ items: empty ? [] : [partnerItem('Old synthetic scope')], pageNumber: q.pageNumber, pageSize: q.pageSize, totalCount: empty ? 0 : 11 })
  const ui = componentHarness(source('views/apps/partners/list/PartnerListTable.tsx'), {
    '@/hooks/useSessionIntent': intent,
    '@/hooks/useDictionary': dictionary,
    '@/services/api': { ApiRequestError },
    './AddPartnerDrawer': { default: 'PartnerDrawer' },
    '@core/styles/table.module.css': { default: { table: 'synthetic' } },
    '@/services/das/external-entities': {
      externalEntityApi: {
        getOptions: async () => ({ canManage: true }),
        getList: async (q: any, signal?: AbortSignal) => {
          requests.push({ query: q, signal })
          if (q.searchTerm === 'FreshScope') return { ...page(q), items: [partnerItem('FreshScope authoritative row')], totalCount: 1 }
          if (requests.length === 3) return corrected.promise
          return page(q, requests.length > 1)
        }
      }
    }
  }, { Error, setTimeout: (f: () => any) => { timers.push(f); return f }, clearTimeout() {} })
  ui.render(); ui.commit(); await timers.shift()!()
  nodes(ui.render()).find(n => n.type === 'TablePagination')!.props.onPageChange(null, 1)
  ui.render(); ui.commit()
  let pending = timers.shift()!(); await tick()
  ui.render(); ui.commit()
  if (requests.length < 3 && timers.length) { pending = timers.shift()!(); await tick() }
  assert.equal(requests.length, 3, 'Fixture must reach one pending corrected-page GET')
  return {
    ui,
    requests,
    corrected,
    pending,
    page,
    async fresh() {
      nodes(ui.render()).find(n => n.props.label === 'Search entity')!.props.onChange({ target: { value: 'FreshScope' } })
      ui.render(); ui.commit(); await timers.shift()!()
      assert.ok(textContent(ui.render()).includes('FreshScope authoritative row'), 'New query must finish before old GET settles')
    },
    async dispose() {
      corrected.resolve(page(requests[2].query, true))
      await pending
      ui.unmount()
    }
  }
}

test('T01-CANONICAL-SUCCESS: stale corrected GET must not overwrite a newer loaded partner query', async () => {
  const f = await partnerOwnerFixture()
  try {
    await f.fresh()
    f.corrected.resolve(f.page(f.requests[2].query, true))
    await f.pending
    assert.ok(textContent(f.ui.render()).includes('FreshScope authoritative row'))
  } finally { await f.dispose() }
})

test('T01-CANONICAL-FAILURE: stale corrected GET rejection must not overwrite a newer loaded partner query', async () => {
  const f = await partnerOwnerFixture()
  try {
    await f.fresh()
    f.corrected.reject(new ApiRequestError(503, 'Synthetic stale failure'))
    await f.pending
    assert.ok(textContent(f.ui.render()).includes('FreshScope authoritative row'))
  } finally { await f.dispose() }
})

for (const status of [401, 403]) {
  test(`T02-CANONICAL-${status}: corrected partner GET denial must revoke management hints`, async () => {
    const f = await partnerOwnerFixture()
    try {
      f.corrected.reject(new ApiRequestError(status, 'Synthetic correction denial'))
      await f.pending
      assert.equal(nodes(f.ui.render()).some(n => n.type === 'FormControlLabel' && n.props.label === 'Include deleted'), false)
    } finally { await f.dispose() }
  })
}

test('T03-CANONICAL: corrected partner GET must have an owner cancellation signal disposed on unmount', async () => {
  const f = await partnerOwnerFixture()
  try {
    const signal = f.requests[2].signal
    assert.ok(signal instanceof AbortSignal, 'Corrected GET must receive an AbortSignal')
    f.ui.unmount()
    assert.equal(signal.aborted, true)
  } finally { await f.dispose() }
})
