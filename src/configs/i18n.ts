export const i18n = {
  defaultLocale: 'en',
  locales: ['en', 'vi', 'fr', 'ar'],
  langDirection: {
    en: 'ltr',
    vi: 'ltr',
    fr: 'ltr',
    ar: 'rtl'
  }
} as const

export type Locale = (typeof i18n)['locales'][number]
