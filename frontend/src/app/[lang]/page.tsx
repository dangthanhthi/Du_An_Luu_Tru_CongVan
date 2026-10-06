import { redirect } from 'next/navigation'
import { i18n, type Locale } from '@configs/i18n'

export default async function LangPage(props: { params: Promise<{ lang: string }> }) {
  const params = await props.params
  const lang: Locale = i18n.locales.includes(params.lang as Locale) ? (params.lang as Locale) : i18n.defaultLocale

  redirect(`/${lang}/dashboards/overview`)
}
