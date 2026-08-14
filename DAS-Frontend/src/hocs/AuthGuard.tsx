// Type Imports
import type { Locale } from '@configs/i18n'
import type { ChildrenType } from '@core/types'

export default async function AuthGuard({ children, locale }: ChildrenType & { locale: Locale }) {
  // Direct DAS JWT Authentication
  return <>{children}</>
}
