import { redirect } from 'next/navigation'
import { i18n } from '@configs/i18n'

export default function RootPage() {
  redirect(`/${i18n.defaultLocale}/dashboards/overview`)
}
