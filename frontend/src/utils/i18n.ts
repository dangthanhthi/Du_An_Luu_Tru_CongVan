// Config Imports
import { i18n, type Locale } from '@configs/i18n'

// Check if the url is missing the locale
export const isUrlMissingLocale = (url: string) => {
  return i18n.locales.every(locale => !(url.startsWith(`/${locale}/`) || url === `/${locale}`))
}

// Get the localized url safely without throwing or corrupting routes
export const getLocalizedUrl = (url: string, languageCode?: string): string => {
  const targetLang = (typeof languageCode === 'string' && languageCode) ? languageCode : (i18n.defaultLocale || 'en')
  if (!url || url === '/') return `/${targetLang}/dashboards/overview`

  const segments = url.split('/').filter(Boolean)
  if (segments.length === 0) return `/${targetLang}/dashboards/overview`

  if (i18n.locales.includes(segments[0] as Locale)) {
    segments[0] = targetLang
  } else {
    segments.unshift(targetLang)
  }

  return '/' + segments.join('/')
}
