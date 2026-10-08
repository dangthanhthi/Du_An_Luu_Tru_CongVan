// Next Imports
import { headers } from 'next/headers'

// MUI Imports
import InitColorSchemeScript from '@mui/system/InitColorSchemeScript'
import Script from 'next/script'

// Third-party Imports
import 'react-perfect-scrollbar/dist/css/styles.css'

// Type Imports
import type { ChildrenType } from '@core/types'
import type { Locale } from '@configs/i18n'

// Component Imports

// HOC Imports
import TranslationWrapper from '@/hocs/TranslationWrapper'

// Config Imports
import { i18n } from '@configs/i18n'

// Util Imports
import { getSystemMode } from '@core/utils/serverHelpers'

// Style Imports
import '@/app/globals.css'

// Generated Icon CSS Imports
import '@assets/iconify-icons/generated-icons.css'

export const generateMetadata = async ({ params }: { params: Promise<{ lang: string }> }) => {
  const { lang } = await params

  return lang === 'en'
    ? { title: { default: 'DAS — Document Administration', template: '%s — DAS' }, description: 'Register, manage and track official documents in DAS.' }
    : { title: { default: 'DAS — Quản lý công văn', template: '%s — DAS' }, description: 'Đăng ký, quản lý và theo dõi công văn trong DAS.' }
}

const RootLayout = async (props: ChildrenType & { params: Promise<{ lang: string }> }) => {
  const params = await props.params

  const { children } = props

  // Type guard to ensure lang is a valid Locale
  const lang: Locale = i18n.locales.includes(params.lang as Locale) ? (params.lang as Locale) : i18n.defaultLocale

  // Vars
  const headersList = await headers()
  const systemMode = await getSystemMode()
  const direction = i18n.langDirection[lang]
  // Preserve Material storage keys while Next owns execution during locale navigation.
  const themeScript = InitColorSchemeScript({ attribute: 'data', defaultMode: systemMode,
    modeStorageKey: 'mui-mode', colorSchemeStorageKey: 'mui-color-scheme' })

  return (
    <TranslationWrapper headersList={headersList} lang={lang}>
      <html id='__next' lang={lang} dir={direction} suppressHydrationWarning>
        <body className='flex is-full min-bs-full flex-auto flex-col'>
          <Script id='das-theme-init' strategy='beforeInteractive'
            dangerouslySetInnerHTML={themeScript.props.dangerouslySetInnerHTML} />
          {children}
        </body>
      </html>
    </TranslationWrapper>
  )
}

export default RootLayout
