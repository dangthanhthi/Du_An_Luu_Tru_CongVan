import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'
import { ApiRequestError } from '../../src/services/api'

const tick = () => new Promise(resolve => setImmediate(resolve))

test('Locale selection reloads the localized document while retaining filters and record identity', () => {
  const ui = componentHarness(new URL('../../src/components/layout/shared/LanguageDropdown.tsx', import.meta.url), {
    'next/link': { default: 'NextLink' },
    'next/navigation': {
      usePathname: () => '/vi/apps/documents/list',
      useParams: () => ({ lang: 'vi' }),
      useSearchParams: () => new URLSearchParams('kind=Incoming&view=all&search=J20')
    },
    '@core/hooks/useSettings': { useSettings: () => ({ settings: {} }) }
  }, { URLSearchParams })
  const tree = ui.render()
  const menu = nodes(tree).find(n => n.type === 'Popper')!.props.children({ TransitionProps: {}, placement: 'bottom-start' })
  const english = nodes(menu).find(n => n.type === 'MenuItem' && textContent(n).includes('English'))!

  assert.equal(english.props.component, 'a', 'A language change must load the root theme script in a fresh HTML document')
  assert.equal(english.props.href, '/en/apps/documents/list?kind=Incoming&view=all&search=J20')
  ui.unmount()
})

test('English overview uses English copy including retry and loading, retaining authorized totals', async () => {
  const ui = componentHarness(new URL('../../src/views/dashboards/overview/IncompleteOverview.tsx', import.meta.url), {
    'next/link': { default: 'Link' },
    'next/navigation': { useParams: () => ({ lang: 'en' }) },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    '@/services/das/reports-staff': { incompleteReportsApi: { list: async () => ({ total: 7, evaluatedAt: '2026-10-07T01:00:00Z', groups: [] }) } }
  })
  ui.render(); ui.commit(); await tick()
  const tree = ui.render()
  assert.ok(textContent(tree).includes('Incomplete document overview'))
  assert.ok(textContent(tree).includes('Reload overview'))
  assert.ok(!textContent(tree).includes('hồ sơ'))
  assert.equal(nodes(tree).find(n => n.type === 'Link')?.props.href, '/en/apps/reports/incomplete')
  ui.unmount()
})

for (const [status, expected] of [[401, 'Phiên đăng nhập đã hết hạn'], [403, 'Bạn không có quyền xem công văn này'], [404, 'Không tìm thấy công văn']] as const) {
  test(`Document detail load ${status} explains the error without showing document or PDF`, async () => {
    const ui = componentHarness(new URL('../../src/views/apps/documents/detail/index.tsx', import.meta.url), {
      '@/hooks/useSessionIntent': { useSessionIntent: () => ({}) },
      '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: false }) },
      'next/navigation': { useParams: () => ({ lang: 'vi' }) }, 'next/link': { default: 'Link' },
      '@/services/api': { ApiRequestError },
      '@/services/das/documents': { documentsV2Api: { detail: async () => { throw new ApiRequestError(status, 'Generic transport message') } } },
      '../V2PdfPanel': { default: 'PdfPanel' }, '@/views/apps/history/HistoryPanel': { default: 'HistoryPanel' }, '../DocumentTaskPanel': { default: 'TaskPanel' }
    })
    ui.render({ id: 'fixture' }); ui.commit(); await tick()
    const tree = ui.render({ id: 'fixture' })
    assert.ok(textContent(tree).includes(expected))
    assert.ok(!nodes(tree).some(n => n.type === 'PdfPanel'))
    ui.unmount()
  })
}

test('PDF preview uses a renderer instead of relying on a native iframe', () => {
  const ui = componentHarness(new URL('../../src/components/DocumentPDFPreview.tsx', import.meta.url), {
    '@/hooks/useDictionary': { useAppDictionary: () => ({ isEn: true }) },
    './PdfCanvasPreview': { default: 'PdfCanvasPreview' }
  })
  const tree = ui.render({ pdfUrl: 'blob:actual-authorized-pdf', fileName: 'signed.pdf' })
  assert.ok(!nodes(tree).some(n => n.type === 'iframe'))
  assert.ok(nodes(tree).some(n => n.type === 'PdfCanvasPreview'))
})

test('Root layout gives Next ownership of theme initialization while retaining MUI storage keys', async () => {
  let options: any
  const ui = componentHarness(new URL('../../src/app/[lang]/layout.tsx', import.meta.url), {
    'next/headers': { headers: async () => ({}) },
    'next/script': { default: 'NextScript' },
    '@mui/system/InitColorSchemeScript': { default: (props: any) => { options = props; return { props: { dangerouslySetInnerHTML: { __html: '/* mui theme initialization */' } } } } },
    'react-perfect-scrollbar/dist/css/styles.css': {},
    '@/hocs/TranslationWrapper': { default: 'TranslationWrapper' },
    '@configs/i18n': { i18n: { locales: ['vi', 'en'], defaultLocale: 'vi', langDirection: { vi: 'ltr', en: 'ltr' } } },
    '@core/utils/serverHelpers': { getSystemMode: async () => 'dark' },
    '@/app/globals.css': {}, '@assets/iconify-icons/generated-icons.css': {}
  })
  const tree = await ui.render({ params: Promise.resolve({ lang: 'en' }), children: 'Content' })
  const scripts = nodes(tree).filter(n => n.type === 'NextScript')
  assert.equal(scripts.length, 1)
  assert.equal(scripts[0].props.strategy, 'beforeInteractive')
  assert.equal(options.modeStorageKey, 'mui-mode')
  assert.equal(options.colorSchemeStorageKey, 'mui-color-scheme')
  assert.equal(options.defaultMode, 'dark')
  assert.ok(!nodes(tree).some(n => n.type === 'script'))
})

test('Vietnamese document pagination labels empty and populated counts', async () => {
  const ui = componentHarness(new URL('../../src/views/apps/documents/list/DocumentListTable.tsx', import.meta.url), {
    'next/link': { default: 'Link' },
    'next/navigation': { useParams: () => ({ lang: 'vi' }), useRouter: () => ({ replace() {} }), useSearchParams: () => new URLSearchParams('kind=Incoming&view=all') },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ t: { nav: {}, documents: {} } }) },
    '@/services/api': { documentApi: { getScopedList: async () => ({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 }) } },
    '@/services/das/documents': { documentsV2Api: { options: async () => ({ canRegister: false }) } },
    '@/utils/i18n': { getLocalizedUrl: (path: string) => '/vi' + path },
    '@core/styles/table.module.css': { default: {} },
    '@core/components/mui/TextField': { default: 'TextField' }
  }, { URLSearchParams, window: { addEventListener() {}, removeEventListener() {} } })
  ui.render(); ui.commit(); await tick()
  const pagination = nodes(ui.render()).find(n => n.type === 'TablePagination')!
  assert.equal(pagination.props.labelDisplayedRows({ from: 0, to: 0, count: 0 }), '0–0 trên 0')
  assert.equal(pagination.props.labelDisplayedRows({ from: 11, to: 20, count: 25 }), '11–20 trên 25')
  assert.equal(pagination.props.getItemAriaLabel('next'), 'Trang tiếp theo')
  ui.unmount()
})
