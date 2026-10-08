'use client'

// React Imports
import { useRef, useState, useEffect } from 'react'

// Next Imports
import { usePathname } from 'next/navigation'

// MUI Imports
import Chip from '@mui/material/Chip'
import Fade from '@mui/material/Fade'
import Paper from '@mui/material/Paper'
import Popper from '@mui/material/Popper'
import { useTheme } from '@mui/material/styles'
import ClickAwayListener from '@mui/material/ClickAwayListener'
import Switch from '@mui/material/Switch'
import type { Breakpoint } from '@mui/material/styles'

// Third-party Imports
import classnames from 'classnames'
import { useDebounce, useMedia } from 'react-use'
import { HexColorPicker, HexColorInput } from 'react-colorful'

// Type Imports
import type { Settings } from '@core/contexts/settingsContext'
import type { Direction } from '@core/types'
import type { PrimaryColorConfig } from '@configs/primaryColorConfig'

// Icon Imports
import SkinDefault from '@core/svg/SkinDefault'
import SkinBordered from '@core/svg/SkinBordered'
import LayoutVertical from '@core/svg/LayoutVertical'
import LayoutCollapsed from '@core/svg/LayoutCollapsed'
import LayoutHorizontal from '@core/svg/LayoutHorizontal'
import ContentCompact from '@core/svg/ContentCompact'
import ContentWide from '@core/svg/ContentWide'
import DirectionLtr from '@core/svg/DirectionLtr'
import DirectionRtl from '@core/svg/DirectionRtl'

// Config Imports
import primaryColorConfig from '@configs/primaryColorConfig'

// Hook Imports
import { useSettings } from '@core/hooks/useSettings'

// Style Imports
import styles from './styles.module.css'

type CustomizerProps = {
  breakpoint?: Breakpoint | 'xxl' | `${number}px` | `${number}rem` | `${number}em`
  dir?: Direction
  disableDirection?: boolean
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

type DebouncedColorPickerProps = {
  settings: Settings
  isColorFromPrimaryConfig: PrimaryColorConfig | undefined
  handleChange: (field: keyof Settings | 'primaryColor', value: Settings[keyof Settings] | string) => void
}

const DebouncedColorPicker = (props: DebouncedColorPickerProps) => {
  // Props
  const { settings, isColorFromPrimaryConfig, handleChange } = props

  // States
  const [debouncedColor, setDebouncedColor] = useState(settings.primaryColor ?? primaryColorConfig[0].main)

  // Hooks
  useDebounce(() => handleChange('primaryColor', debouncedColor), 200, [debouncedColor])

  return (
    <>
      <HexColorPicker
        color={!isColorFromPrimaryConfig ? (settings.primaryColor ?? primaryColorConfig[0].main) : '#eee'}
        onChange={setDebouncedColor}
      />
      <HexColorInput
        id='das-customizer-color-input' aria-label='Theme color code / Mã màu giao diện' className={styles.colorInput}
        color={!isColorFromPrimaryConfig ? (settings.primaryColor ?? primaryColorConfig[0].main) : '#eee'}
        onChange={setDebouncedColor}
        prefixed
        placeholder='Type a color'
      />
    </>
  )
}

const Customizer = ({ breakpoint = 'lg', dir = 'ltr', disableDirection = false }: CustomizerProps) => {
  // States
  const [isOpen, setIsOpen] = useState(false)
  const [direction, setDirection] = useState(dir)
  const [isMenuOpen, setIsMenuOpen] = useState(false)

  // Refs
  const anchorRef = useRef<HTMLButtonElement | null>(null)
  const triggerRef = useRef<HTMLButtonElement | null>(null)
  const closeRef = useRef<HTMLButtonElement | null>(null)

  // Hooks
  const theme = useTheme()
  const pathName = usePathname()
  const { settings, updateSettings, resetSettings, isSettingsChanged } = useSettings()
  const isSystemDark = useMedia('(prefers-color-scheme: dark)', false)

  // Sync direction when dir prop changes
  useEffect(() => {
    setDirection(dir)
  }, [dir])

  const handleDirectionChange = (newDir: Direction) => {
    setDirection(newDir)
    document.documentElement.setAttribute('dir', newDir)
    document.body.setAttribute('dir', newDir)
    const nextElem = document.getElementById('__next')
    if (nextElem) {
      nextElem.setAttribute('dir', newDir)
    }
    const targetLocale = newDir === 'rtl' ? 'ar' : 'en'
    const newPath = getLocalePath(pathName, targetLocale)
    window.location.href = newPath + window.location.search
  }

  const currentLang = pathName ? pathName.split('/')[1] : 'en'

  const handleLanguageChange = (targetLang: string) => {
    const newPath = getLocalePath(pathName, targetLang)
    window.location.href = newPath + window.location.search
  }

  // Vars
  let breakpointValue: CustomizerProps['breakpoint']

  switch (breakpoint) {
    case 'xxl':
      breakpointValue = '1920px'
      break
    case 'xl':
      breakpointValue = `${theme.breakpoints.values.xl}px`
      break
    case 'lg':
      breakpointValue = `${theme.breakpoints.values.lg}px`
      break
    case 'md':
      breakpointValue = `${theme.breakpoints.values.md}px`
      break
    case 'sm':
      breakpointValue = `${theme.breakpoints.values.sm}px`
      break
    case 'xs':
      breakpointValue = `${theme.breakpoints.values.xs}px`
      break
    default:
      breakpointValue = breakpoint
  }

  const breakpointReached = useMedia(`(max-width: ${breakpointValue})`, false)
  const isMobileScreen = useMedia('(max-width: 600px)', false)
  const isColorFromPrimaryConfig = primaryColorConfig.find(item => item.main === settings.primaryColor)


  const handleToggle = () => {
    if (isOpen) {
      setIsMenuOpen(false)
      triggerRef.current?.focus()
    }
    setIsOpen(!isOpen)
  }

  useEffect(() => {
    if (isOpen && !breakpointReached) closeRef.current?.focus()
  }, [isOpen, breakpointReached])

  useEffect(() => {
    if (!isOpen || breakpointReached) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.preventDefault()
      event.stopPropagation()
      if (isMenuOpen) {
        setIsMenuOpen(false)
        anchorRef.current?.focus()
      } else {
        setIsOpen(false)
        triggerRef.current?.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [isOpen, isMenuOpen, breakpointReached])

  // Update Settings
  const handleChange = (field: keyof Settings | 'direction', value: Settings[keyof Settings] | Direction) => {
    // Update direction state
    if (field === 'direction') {
      setDirection(value as Direction)
    } else {
      // Update settings in cookie
      updateSettings({ [field]: value })
    }
  }

  const handleMenuClose = (event: MouseEvent | TouchEvent): void => {
    if (anchorRef.current && anchorRef.current.contains(event.target as HTMLElement)) {
      return
    }

    setIsMenuOpen(false)
  }

  return (
    !breakpointReached && (
      <div
        className={classnames('customizer', styles.customizer, {
          [styles.show]: isOpen,
          [styles.smallScreen]: isMobileScreen
        })}
      >
        <button type='button' ref={triggerRef} aria-label={currentLang === 'vi' ? 'Tùy chỉnh giao diện' : 'Customize theme'} aria-expanded={isOpen} aria-controls='das-theme-customizer' className={styles.toggler} onClick={handleToggle}>
          <i className='tabler-settings text-[22px]' />
        </button>
        <div id='das-theme-customizer' role='region' aria-labelledby='das-customizer-title' className='flex flex-col bs-full min-bs-0' inert={!isOpen} aria-hidden={!isOpen}>
          <div className={styles.header}>
            <div className='flex flex-col'>
              <h4 id='das-customizer-title' className={styles.customizerTitle}>{currentLang === 'vi' ? 'Tùy chỉnh giao diện' : 'Theme Customizer'}</h4>
              <p className={styles.customizerSubtitle}>{currentLang === 'vi' ? 'Tùy chỉnh và xem trước trực tiếp' : 'Customize & Preview in Real Time'}</p>
            </div>
            <div className='flex gap-4'>
              <button type='button' aria-label={currentLang === 'vi' ? 'Đặt lại giao diện' : 'Reset theme'} onClick={resetSettings} className={classnames(styles.iconButton, 'relative flex')}>
                <i className='tabler-refresh text-textPrimary' />
                <div className={classnames(styles.dotStyles, { [styles.show]: isSettingsChanged })} />
              </button>
              <button type='button' data-customizer-close ref={closeRef} className={styles.iconButton} aria-label={currentLang === 'vi' ? 'Đóng tùy chỉnh giao diện' : 'Close theme customizer'} onClick={handleToggle}><i className='tabler-x text-textPrimary' aria-hidden='true' /></button>
            </div>
          </div>
          <div className='bs-full overflow-y-auto overflow-x-hidden'>
            <div className={styles.customizerBody}>
              <div className='flex flex-col gap-6'>
                <Chip label={currentLang === 'vi' ? 'Màu sắc' : 'Theming'} size='small' color='primary' variant='tonal' className='self-start rounded-sm' />
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>{currentLang === 'vi' ? 'Màu chủ đạo' : 'Primary Color'}</p>
                  <div className='flex items-center justify-between'>
                    {primaryColorConfig.map(item => (
                      <button type='button' aria-label={(currentLang === 'vi' ? 'Màu chủ đạo ' : 'Primary color ') + item.main} data-setting={`primaryColor:${item.main}`} aria-pressed={settings.primaryColor === item.main}
                        key={item.main}
                        className={classnames(styles.primaryColorWrapper, {
                          [styles.active]: settings.primaryColor === item.main
                        })}
                        onClick={() => handleChange('primaryColor', item.main)}
                      >
                        <div className={styles.primaryColor} style={{ backgroundColor: item.main }} />
                      </button>
                    ))}
                    <button type='button' data-customizer-color aria-label={currentLang === 'vi' ? "Chọn màu khác" : "Choose custom color"} aria-expanded={isMenuOpen} aria-controls={isMenuOpen ? 'das-customizer-color-popup' : undefined}
                      ref={anchorRef}
                      className={classnames(styles.primaryColorWrapper, {
                        [styles.active]: !isColorFromPrimaryConfig
                      })}
                      onClick={() => setIsMenuOpen(prev => !prev)}
                    >
                      <span
                        className={classnames(styles.primaryColor, 'flex items-center justify-center')}
                        style={{
                          backgroundColor: !isColorFromPrimaryConfig
                            ? settings.primaryColor
                            : 'var(--mui-palette-action-selected)',
                          color: isColorFromPrimaryConfig
                            ? 'var(--mui-palette-text-primary)'
                            : 'var(--mui-palette-primary-contrastText)'
                        }}
                      >
                        <i className='tabler-color-picker text-xl' />
                      </span>
                    </button>
                    <Popper
                      transition
                      open={isMenuOpen}
                      disablePortal
                      anchorEl={anchorRef.current}
                      placement='bottom-end'
                      className='z-[1]'
                    >
                      {({ TransitionProps }) => (
                        <Fade {...TransitionProps} style={{ transformOrigin: 'right top' }}>
                          <Paper id='das-customizer-color-popup' elevation={6} className={styles.colorPopup}>
                            <ClickAwayListener onClickAway={handleMenuClose}>
                              <div>
                                <DebouncedColorPicker
                                  settings={settings}
                                  isColorFromPrimaryConfig={isColorFromPrimaryConfig}
                                  handleChange={handleChange}
                                />
                              </div>
                            </ClickAwayListener>
                          </Paper>
                        </Fade>
                      )}
                    </Popper>
                  </div>
                </div>
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>{currentLang === 'vi' ? 'Chế độ' : 'Mode'}</p>
                  <div className='flex items-center justify-between'>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Sáng" : "Light"} data-setting='mode:light' aria-pressed={settings.mode === 'light'}
                        className={classnames(styles.itemWrapper, styles.modeWrapper, {
                          [styles.active]: settings.mode === 'light'
                        })}
                        onClick={() => handleChange('mode', 'light')}
                      >
                        <i className='tabler-sun text-[30px]' />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Sáng' : 'Light'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Tối" : "Dark"} data-setting='mode:dark' aria-pressed={settings.mode === 'dark'}
                        className={classnames(styles.itemWrapper, styles.modeWrapper, {
                          [styles.active]: settings.mode === 'dark'
                        })}
                        onClick={() => handleChange('mode', 'dark')}
                      >
                        <i className='tabler-moon-stars text-[30px]' />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Tối' : 'Dark'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Hệ thống" : "System"} data-setting='mode:system' aria-pressed={settings.mode === 'system'}
                        className={classnames(styles.itemWrapper, styles.modeWrapper, {
                          [styles.active]: settings.mode === 'system'
                        })}
                        onClick={() => handleChange('mode', 'system')}
                      >
                        <i className='tabler-device-laptop text-[30px]' />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Hệ thống' : 'System'}
                      </p>
                    </div>
                  </div>
                </div>
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>{currentLang === 'vi' ? 'Kiểu giao diện' : 'Skin'}</p>
                  <div className='flex items-center gap-4'>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Mặc định" : "Default"} data-setting='skin:default' aria-pressed={settings.skin === 'default'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.skin === 'default' })}
                        onClick={() => handleChange('skin', 'default')}
                      >
                        <SkinDefault />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Mặc định' : 'Default'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Có viền" : "Bordered"} data-setting='skin:bordered' aria-pressed={settings.skin === 'bordered'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.skin === 'bordered' })}
                        onClick={() => handleChange('skin', 'bordered')}
                      >
                        <SkinBordered />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Có viền' : 'Bordered'}
                      </p>
                    </div>
                  </div>
                </div>
                {settings.mode === 'dark' ||
                (settings.mode === 'system' && isSystemDark) ||
                settings.layout === 'horizontal' ? null : (
                  <div className='flex items-center justify-between'>
                    <label className='font-medium cursor-pointer' htmlFor='customizer-semi-dark'>
                      {currentLang === 'vi' ? 'Điều hướng tối' : 'Semi Dark'}
                    </label>
                    <Switch
                      id='customizer-semi-dark'
                      checked={settings.semiDark === true}
                      onChange={() => handleChange('semiDark', !settings.semiDark)}
                    />
                  </div>
                )}
              </div>
              <hr className={styles.hr} />
              <div className='flex flex-col gap-6'>
                <Chip label={currentLang === 'vi' ? 'Bố cục' : 'Layout'} variant='tonal' size='small' color='primary' className='self-start rounded-sm' />
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>{currentLang === 'vi' ? 'Bố cục điều hướng' : 'Layouts'}</p>
                  <div className='flex items-center justify-between'>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Dọc" : "Vertical"} data-setting='layout:vertical' aria-pressed={settings.layout === 'vertical'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.layout === 'vertical' })}
                        onClick={() => handleChange('layout', 'vertical')}
                      >
                        <LayoutVertical />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Dọc' : 'Vertical'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Thu gọn" : "Collapsed"} data-setting='layout:collapsed' aria-pressed={settings.layout === 'collapsed'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.layout === 'collapsed' })}
                        onClick={() => handleChange('layout', 'collapsed')}
                      >
                        <LayoutCollapsed />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Thu gọn' : 'Collapsed'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Ngang" : "Horizontal"} data-setting='layout:horizontal' aria-pressed={settings.layout === 'horizontal'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.layout === 'horizontal' })}
                        onClick={() => handleChange('layout', 'horizontal')}
                      >
                        <LayoutHorizontal />
                      </button>
                      <p className={styles.itemLabel} >
                        {currentLang === 'vi' ? 'Ngang' : 'Horizontal'}
                      </p>
                    </div>
                  </div>
                </div>
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>{currentLang === 'vi' ? 'Chiều rộng nội dung' : 'Content'}</p>
                  <div className='flex items-center gap-4'>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Gọn" : "Compact"} aria-pressed={settings.contentWidth === 'compact'}
                        className={classnames(styles.itemWrapper, {
                          [styles.active]: settings.contentWidth === 'compact'
                        })}
                        onClick={() =>
                          updateSettings({
                            navbarContentWidth: 'compact',
                            contentWidth: 'compact',
                            footerContentWidth: 'compact'
                          })
                        }
                      >
                        <ContentCompact />
                      </button>
                      <p
                        className={styles.itemLabel}

                      >
                        {currentLang === 'vi' ? 'Gọn' : 'Compact'}
                      </p>
                    </div>
                    <div className='flex flex-col items-start gap-0.5'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Rộng" : "Wide"} aria-pressed={settings.contentWidth === 'wide'}
                        className={classnames(styles.itemWrapper, { [styles.active]: settings.contentWidth === 'wide' })}
                        onClick={() =>
                          updateSettings({ navbarContentWidth: 'wide', contentWidth: 'wide', footerContentWidth: 'wide' })
                        }
                      >
                        <ContentWide />
                      </button>
                      <p
                        className={styles.itemLabel}

                      >
                        {currentLang === 'vi' ? 'Rộng' : 'Wide'}
                      </p>
                    </div>
                  </div>
                </div>
                {/* Language Switcher */}
                <div className='flex flex-col gap-2'>
                  <p className='font-medium'>Ngôn Ngữ / Language</p>
                  <div className='flex items-center gap-4'>
                    <button type='button' aria-label={currentLang === 'vi' ? "Tiếng Việt" : "Tiếng Việt"} aria-pressed={currentLang === 'vi'}
                      className='cursor-pointer'
                      onClick={() => handleLanguageChange('vi')}
                    >
                      <span className='flex flex-col items-start gap-0.5'>
                        <span
                          className={classnames(styles.itemWrapper, {
                            [styles.active]: currentLang === 'vi'
                          })}
                          style={{ minWidth: 80, height: 44, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600, fontSize: 13 }}
                        >
                          🇻🇳 Tiếng Việt
                        </span>
                        <p className={styles.itemLabel}>
                          Tiếng Việt <br />
                          (VI)
                        </p>
                      </span>
                    </button>

                    <button type='button' aria-label={currentLang === 'vi' ? "English" : "English"} aria-pressed={currentLang === 'en'}
                      className='cursor-pointer'
                      onClick={() => handleLanguageChange('en')}
                    >
                      <span className='flex flex-col items-start gap-0.5'>
                        <span
                          className={classnames(styles.itemWrapper, {
                            [styles.active]: currentLang === 'en' || (currentLang !== 'vi' && currentLang !== 'ar' && currentLang !== 'fr')
                          })}
                          style={{ minWidth: 80, height: 44, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 600, fontSize: 13 }}
                        >
                          🇬🇧 English
                        </span>
                        <p className={styles.itemLabel}>
                          English <br />
                          (EN)
                        </p>
                      </span>
                    </button>
                  </div>
                </div>

                {!disableDirection && (
                  <div className='flex flex-col gap-2'>
                    <p className='font-medium'>{currentLang === 'vi' ? 'Hướng chữ' : 'Direction'}</p>
                    <div className='flex items-center gap-4'>
                      <button type='button' aria-label={currentLang === 'vi' ? "Trái sang phải" : "Left to Right"} aria-pressed={direction === 'ltr'}
                        className='cursor-pointer'
                        onClick={() => handleDirectionChange('ltr')}
                      >
                        <span className='flex flex-col items-start gap-0.5'>
                          <span
                            className={classnames(styles.itemWrapper, {
                              [styles.active]: direction === 'ltr'
                            })}
                          >
                            <DirectionLtr />
                          </span>
                          <p className={styles.itemLabel}>
                            {currentLang === 'vi' ? 'Trái sang phải' : 'Left to Right'} <br />
                            (LTR)
                          </p>
                        </span>
                      </button>
                      <button type='button' aria-label={currentLang === 'vi' ? "Phải sang trái" : "Right to Left"} aria-pressed={direction === 'rtl'}
                        className='cursor-pointer'
                        onClick={() => handleDirectionChange('rtl')}
                      >
                        <span className='flex flex-col items-start gap-0.5'>
                          <span
                            className={classnames(styles.itemWrapper, {
                              [styles.active]: direction === 'rtl'
                            })}
                          >
                            <DirectionRtl />
                          </span>
                          <p className={styles.itemLabel}>
                            {currentLang === 'vi' ? 'Phải sang trái' : 'Right to Left'} <br />
                            (RTL)
                          </p>
                        </span>
                      </button>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    )
  )
}

export default Customizer
