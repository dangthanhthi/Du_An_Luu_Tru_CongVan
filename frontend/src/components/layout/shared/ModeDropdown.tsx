'use client'

// React Imports
import { useRef, useState } from 'react'
import { useParams } from 'next/navigation'

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
import type { Mode } from '@core/types'

// Hook Imports
import { useSettings } from '@core/hooks/useSettings'

const ModeDropdown = () => {
  // States
  const [open, setOpen] = useState(false)
  const [tooltipOpen, setTooltipOpen] = useState(false)

  // Refs
  const anchorRef = useRef<HTMLButtonElement>(null)

  // Hooks
  const { settings, updateSettings } = useSettings()
  const params = useParams()
  const isEn = (params?.lang as string) === 'en'

  const modeLabels: Record<Mode, string> = {
    light: isEn ? 'Light' : 'Sáng',
    dark: isEn ? 'Dark' : 'Tối',
    system: isEn ? 'System' : 'Hệ thống'
  }

  const currentMode: Mode = settings.mode ?? 'system'

  const tooltipTitle = isEn
    ? `${modeLabels[currentMode]} Mode`
    : `Chế độ ${modeLabels[currentMode]}`

  const handleClose = () => {
    setOpen(false)
    setTooltipOpen(false)
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

  const handleModeSwitch = (mode: Mode) => {
    handleClose()
    anchorRef.current?.focus()

    if (settings.mode !== mode) {
      updateSettings({ mode: mode })
    }
  }

  const getModeIcon = () => {
    if (settings.mode === 'system') {
      return 'tabler-device-laptop'
    } else if (settings.mode === 'dark') {
      return 'tabler-moon-stars'
    } else {
      return 'tabler-sun'
    }
  }

  return (
    <>
      <Tooltip
        title={tooltipTitle}
        onOpen={() => setTooltipOpen(true)}
        onClose={() => setTooltipOpen(false)}
        open={open ? false : tooltipOpen ? true : false}
        slotProps={{ popper: { className: 'capitalize' } }}
      >
        <IconButton
          ref={anchorRef}
          onClick={handleToggle}
          className='text-textPrimary'
          aria-label={isEn ? 'Switch theme mode' : 'Chuyển chế độ giao diện'}
          aria-haspopup='true'
          aria-expanded={open}
          onKeyDown={handleKeyDown}
          aria-controls={open ? 'mode-menu-list' : undefined}
        >
          <i className={getModeIcon()} />
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
                <MenuList autoFocusItem={open} id='mode-menu-list' onKeyDown={handleKeyDown}>
                  <MenuItem
                    className='gap-3'
                    onClick={() => handleModeSwitch('light')}
                    selected={settings.mode === 'light'}
                  >
                    <i className='tabler-sun' />
                    {modeLabels.light}
                  </MenuItem>
                  <MenuItem
                    className='gap-3'
                    onClick={() => handleModeSwitch('dark')}
                    selected={settings.mode === 'dark'}
                  >
                    <i className='tabler-moon-stars' />
                    {modeLabels.dark}
                  </MenuItem>
                  <MenuItem
                    className='gap-3'
                    onClick={() => handleModeSwitch('system')}
                    selected={settings.mode === 'system'}
                  >
                    <i className='tabler-device-laptop' />
                    {modeLabels.system}
                  </MenuItem>
                </MenuList>
              </ClickAwayListener>
            </Paper>
          </Fade>
        )}
      </Popper>
    </>
  )
}

export default ModeDropdown
