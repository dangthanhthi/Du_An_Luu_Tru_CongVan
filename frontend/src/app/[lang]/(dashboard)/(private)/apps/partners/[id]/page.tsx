import PartnerDetail from '@/views/apps/partners/detail/PartnerDetail'

export default async function PartnerHistoryPage({ params, searchParams }: {
  params: Promise<{ lang: string; id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>>
}) {
  const { lang, id } = await params, query = await searchParams
  const validQuery = Object.keys(query).every(key => key === 'tab') && (query.tab === undefined || query.tab === 'history')

  return <PartnerDetail id={id} lang={lang} validQuery={validQuery} />
}
