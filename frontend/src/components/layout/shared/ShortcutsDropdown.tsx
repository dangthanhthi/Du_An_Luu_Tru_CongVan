'use client'

// React Imports
import { useCallback, useRef, useState, useEffect } from 'react'
import type { ReactNode } from 'react'

// Next Imports
import Link from 'next/link'

// MUI Imports
import IconButton from '@mui/material/IconButton'
import Popper from '@mui/material/Popper'
import Fade from '@mui/material/Fade'
import Paper from '@mui/material/Paper'
import ClickAwayListener from '@mui/material/ClickAwayListener'
import Typography from '@mui/material/Typography'
import Divider from '@mui/material/Divider'
import Grid from '@mui/material/Grid'
import useMediaQuery from '@mui/material/useMediaQuery'
import type { Theme } from '@mui/material/styles'

// Third Party Components
import classnames from 'classnames'
import PerfectScrollbar from 'react-perfect-scrollbar'

// Type Imports

// Component Imports
import CustomAvatar from '@core/components/mui/Avatar'

// Config Imports
import themeConfig from '@configs/themeConfig'

// Hook Imports
import { useSettings } from '@core/hooks/useSettings'

// Util Imports
import { useAppDictionary } from '@/hooks/useDictionary'

export type ShortcutsType = {
  url: string
  icon: string
  title: string
  subtitle: string
}

const ScrollWrapper = ({ children, hidden }: { children: ReactNode; hidden: boolean }) => {
  if (hidden) {
    return <div className='overflow-x-hidden bs-full'>{children}</div>
  } else {
    return (
      <PerfectScrollbar className='bs-full' options={{ wheelPropagation: false, suppressScrollX: true }}>
        {children}
      </PerfectScrollbar>
    )
  }
}

const ShortcutsDropdown = ({ shortcuts, onOpen }: { shortcuts: ShortcutsType[]; onOpen?: () => void }) => {
  // States
  const [open, setOpen] = useState(false)

  // Refs
  const anchorRef = useRef<HTMLButtonElement>(null)
  const ref = useRef<HTMLDivElement | null>(null)

  // Hooks
  const hidden = useMediaQuery((theme: Theme) => theme.breakpoints.down('lg'))
  const isSmallScreen = useMediaQuery((theme: Theme) => theme.breakpoints.down('sm'))
  const { settings } = useSettings()
  const { t } = useAppDictionary()

  const handleClose = useCallback(() => {
    setOpen(false)
  }, [])

  const handleToggle = useCallback(() => {
    if (!open) onOpen?.()
    setOpen(prevOpen => !prevOpen)
  }, [open, onOpen])

  useEffect(() => {
    const adjustPopoverHeight = () => {
      if (ref.current) {
        // Calculate available height, subtracting any fixed UI elements' height as necessary
        const availableHeight = window.innerHeight - 100

        ref.current.style.height = `${Math.min(availableHeight, 550)}px`
      }
    }

    window.addEventListener('resize', adjustPopoverHeight)
    adjustPopoverHeight()

    return () => window.removeEventListener('resize', adjustPopoverHeight)
  }, [open])

  return (
    <>
      <IconButton ref={anchorRef} onClick={handleToggle} className='text-textPrimary'
        aria-label={t.search.openShortcuts} aria-expanded={open}>
        <i className='tabler-layout-grid-add' />
      </IconButton>
      <Popper
        open={open}
        transition
        disablePortal
        placement='bottom-end'
        ref={ref}
        anchorEl={anchorRef.current}
        {...(isSmallScreen
          ? {
              className: 'is-full  !mbs-3 z-[1] max-bs-[517px]',
              modifiers: [
                {
                  name: 'preventOverflow',
                  options: {
                    padding: themeConfig.layoutPadding
                  }
                }
              ]
            }
          : { className: 'is-96  !mbs-3 z-[1] max-bs-[517px]' })}
      >
        {({ TransitionProps, placement }) => (
          <Fade {...TransitionProps} style={{ transformOrigin: placement === 'bottom-end' ? 'right top' : 'left top' }}>
            <Paper className={classnames('bs-full', settings.skin === 'bordered' ? 'border shadow-none' : 'shadow-lg')}>
              <ClickAwayListener onClickAway={handleClose}>
                <div className='bs-full flex flex-col'>
                  <div className='flex items-center justify-between plb-3.5 pli-4 is-full gap-2'>
                    <Typography variant='h6' className='flex-auto'>
                      {t.search.shortcuts}
                    </Typography>
                  </div>
                  <Divider />
                  <ScrollWrapper hidden={hidden}>
                    <Grid container>
                      {shortcuts.map((shortcut, index) => (
                        <Grid
                          size={{ xs: 6 }}
                          key={index}
                          onClick={handleClose}
                          className='[&:not(:last-of-type):not(:nth-last-of-type(2))]:border-be odd:border-ie'
                        >
                          <Link
                            href={shortcut.url}
                            className='flex items-center flex-col p-6 gap-3 bs-full hover:bg-actionHover'
                          >
                            <CustomAvatar size={50} className='bg-actionSelected text-textPrimary'>
                              <i className={classnames('text-[1.625rem]', shortcut.icon)} />
                            </CustomAvatar>
                            <div className='flex flex-col items-center text-center'>
                              <Typography className='font-medium' color='text.primary'>
                                {shortcut.title}
                              </Typography>
                              <Typography variant='body2'>{shortcut.subtitle}</Typography>
                            </div>
                          </Link>
                        </Grid>
                      ))}
                    </Grid>
                  </ScrollWrapper>
                </div>
              </ClickAwayListener>
            </Paper>
          </Fade>
        )}
      </Popper>
    </>
  )
}

export default ShortcutsDropdown
