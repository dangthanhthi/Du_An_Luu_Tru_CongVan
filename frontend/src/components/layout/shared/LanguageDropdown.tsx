'use client'

// React Imports
import { useRef, useState } from 'react'

// Next Imports
import { usePathname, useParams, useSearchParams } from 'next/navigation'

// MUI Imports
import Tooltip from '@mui/material/Tooltip'
import IconButton from '@mui/material/IconButton'
import Popper from '@mui/material/Popper'
import Fade from '@mui/material/Fade'
import Paper from '@mui/material/Paper'
import ClickAwayListener from '@mui/material/ClickAwayListener'
import MenuList from '@mui/material/MenuList'
import MenuItem from '@mui/material/MenuItem'

// Type Imports
import type { Locale } from '@configs/i18n'

// Hook Imports
import { useSettings } from '@core/hooks/useSettings'

type LanguageDataType = {
  langCode: Locale
  langName: string
}

const getLocalePath = (pathName: string, locale: string) => {
  if (!pathName || pathName === '/') {
    return `/${locale}/dashboards/overview`
  }

  const segments = pathName.split('/').filter(Boolean)
  const knownLocales = ['en', 'vi', 'fr', 'ar']

  while (segments.length > 0 && knownLocales.includes(segments[0])) {
    segments.shift()
  }

  if (segments.length === 0) {
    return `/${locale}/dashboards/overview`
  }

  return `/${locale}/${segments.join('/')}`
}

// Vars
const languageData: LanguageDataType[] = [
  {
    langCode: 'vi',
    langName: 'Tiếng Việt'
  },
  {
    langCode: 'en',
    langName: 'English'
  },
  {
    langCode: 'fr',
    langName: 'Français'
  },
  {
    langCode: 'ar',
    langName: 'العربية (Arabic)'
  }
]

const LanguageDropdown = () => {
  // States
  const [open, setOpen] = useState(false)

  // Refs
  const anchorRef = useRef<HTMLButtonElement>(null)

  // Hooks
  const pathName = usePathname()
  const searchParams = useSearchParams()
  const { settings } = useSettings()
  const { lang } = useParams()
  const queryString = searchParams?.toString()

  const handleClose = () => {
    setOpen(false)
  }

  const handleToggle = () => {
    setOpen(prevOpen => !prevOpen)
  }

  const handleKeyDown = (event: React.KeyboardEvent) => {
    if (event.key === 'Tab' || event.key === 'Escape') {
      if (event.key === 'Escape') event.preventDefault()
      handleClose()
      anchorRef.current?.focus()
    } else if (event.key === 'ArrowDown') {
      event.preventDefault()
      setOpen(true)
    }
  }

  return (
    <>
      <Tooltip title={lang === 'vi' ? 'Ngôn ngữ' : 'Language'}>
        <IconButton
          ref={anchorRef}
          onClick={handleToggle}
          className='text-textPrimary'
          aria-label={lang === 'vi' ? 'Chọn ngôn ngữ' : 'Select language'}
          aria-haspopup='true'
          aria-expanded={open}
          onKeyDown={handleKeyDown}
          aria-controls={open ? 'language-menu-list' : undefined}
        >
          <i className='tabler-language' />
        </IconButton>
      </Tooltip>
      <Popper
        open={open}
        transition
        disablePortal
        placement='bottom-start'
        anchorEl={anchorRef.current}
        className='min-is-[160px] !mbs-3 z-[1]'
      >
        {({ TransitionProps, placement }) => (
          <Fade
            {...TransitionProps}
            style={{ transformOrigin: placement === 'bottom-start' ? 'left top' : 'right top' }}
          >
            <Paper className={settings.skin === 'bordered' ? 'border shadow-none' : 'shadow-lg'}>
              <ClickAwayListener onClickAway={handleClose}>
                <MenuList autoFocusItem={open} id='language-menu-list' onKeyDown={handleKeyDown}>
                  {languageData.map(locale => {
                    const basePath = getLocalePath(pathName, locale.langCode)
                    const fullHref = queryString ? `${basePath}?${queryString}` : basePath

                    return (
                      <MenuItem
                        key={locale.langCode}
                        // Locale changes replace the root document (lang/dir/theme script).
                        // A native link runs initialization before hydration on the new page.
                        component='a'
                        href={fullHref}
                        onClick={handleClose}
                        selected={lang === locale.langCode}
                      >
                        {locale.langName}
                      </MenuItem>
                    )
                  })}
                </MenuList>
              </ClickAwayListener>
            </Paper>
          </Fade>
        )}
      </Popper>
    </>
  )
}

export default LanguageDropdown
