import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes, textContent } from './helpers/component-harness'

const tick = () => new Promise(resolve => setImmediate(resolve))

for (const lang of ['vi', 'en']) test(`Document list distinguishes unverified, incomplete and complete dossiers in ${lang}`, async () => {
  const items = [undefined, false, true].map((isComplete, index) => ({
    id: `fixture-${index}`, kind: 'Internal', registrationNumber: `27-01-000${index + 1}/INT/HL/ADM`,
    subject: `Fixture ${index + 1}`, status: 'Distributed', registrationDate: '2027-01-01', issuedDate: '2027-01-01',
    companyCode: 'HL', departmentName: 'ADM', sensitivity: 'Normal', version: 1, allowedActions: [],
    ...(isComplete === undefined ? {} : { isComplete })
  }))
  const ui = componentHarness(new URL('../../src/views/apps/documents/list/DocumentListTable.tsx', import.meta.url), {
    'next/link': { default: 'Link' },
    'next/navigation': { useParams: () => ({ lang }), useRouter: () => ({ replace() {} }), useSearchParams: () => new URLSearchParams('kind=Internal&view=all') },
    '@/hooks/useDictionary': { useAppDictionary: () => ({ t: { nav: {}, documents: {} } }) },
    '@/services/api': { documentApi: { getScopedList: async () => ({ items, totalCount: 3, pageNumber: 1, pageSize: 20 }) } },
    '@/services/das/documents': { documentsV2Api: { options: async () => ({ canRegister: false }) } },
    '@/utils/i18n': { getLocalizedUrl: (path: string) => `/${lang}${path}` },
    '@core/styles/table.module.css': { default: {} },
    '@core/components/mui/TextField': { default: 'TextField' }
  }, { URLSearchParams, window: { addEventListener() {}, removeEventListener() {} } })
  ui.render(); ui.commit(); await tick()
  const rows = nodes(ui.render()).filter(n => n.type === 'tr').slice(1)
  const labels = rows.map(row => nodes(row).filter(n => n.type === 'Chip').map(n => n.props.label))
  assert.deepEqual(labels, lang === 'vi'
    ? [['Đã phân phối', 'Chưa xác minh'], ['Đã phân phối', 'Chưa đủ hồ sơ'], ['Đã phân phối', 'Đủ hồ sơ']]
    : [['Distributed', 'Not verified'], ['Distributed', 'Incomplete'], ['Distributed', 'Complete']])
  const unknown = nodes(rows[0]).find(n => n.type === 'Chip' && n.props.label === (lang === 'vi' ? 'Chưa xác minh' : 'Not verified'))!
  assert.ok(unknown.props.title.includes(lang === 'vi' ? 'Mở công văn' : 'Open the document'))
  assert.ok(textContent(rows[0]).includes('Fixture 1'))
  ui.unmount()
})
