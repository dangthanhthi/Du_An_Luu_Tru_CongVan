'use client'

// React Imports
import { useRef, useState, useEffect } from 'react'
import type { MouseEvent } from 'react'

// Next Imports
import { useParams, useRouter } from 'next/navigation'

// MUI Imports
import { styled } from '@mui/material/styles'
import Badge from '@mui/material/Badge'
import Avatar from '@mui/material/Avatar'
import Popper from '@mui/material/Popper'
import Fade from '@mui/material/Fade'
import Paper from '@mui/material/Paper'
import ClickAwayListener from '@mui/material/ClickAwayListener'
import MenuList from '@mui/material/MenuList'
import Typography from '@mui/material/Typography'
import Divider from '@mui/material/Divider'
import MenuItem from '@mui/material/MenuItem'
import Button from '@mui/material/Button'

// Type Imports
import type { Locale } from '@configs/i18n'

// Hook Imports
import { useSettings } from '@core/hooks/useSettings'

// Util Imports
import { getLocalizedUrl } from '@/utils/i18n'
import { tokenManager, authApi } from '@/services/api'
import { getUserMenuIdentity, type UserMenuIdentity } from './userMenuIdentity'
import { useAppDictionary } from '@/hooks/useDictionary'

// Styled component for badge content
const BadgeContentSpan = styled('span')({
  width: 8,
  height: 8,
  borderRadius: '50%',
  cursor: 'pointer',
  backgroundColor: 'var(--mui-palette-success-main)',
  boxShadow: '0 0 0 2px var(--mui-palette-background-paper)'
})

const readMenuIdentity = () => {
  try {
    const hasToken = Boolean(tokenManager.getToken()?.trim())

    return getUserMenuIdentity(hasToken, hasToken ? tokenManager.getUser() : null)
  } catch {
    return null
  }
}

const UserDropdown = () => {
  // States
  const [open, setOpen] = useState(false)
  const [user, setUser] = useState<UserMenuIdentity | null>(null)

  // Refs
  const anchorRef = useRef<HTMLDivElement>(null)

  // Hooks
  const router = useRouter()
  const { settings } = useSettings()
  const { lang: locale } = useParams()
  const { t } = useAppDictionary()

  useEffect(() => {
    const syncIdentity = () => setUser(readMenuIdentity())

    syncIdentity()
    window.addEventListener('storage', syncIdentity)

    return () => window.removeEventListener('storage', syncIdentity)
  }, [])

  const handleDropdownOpen = () => {
    setUser(readMenuIdentity())
    setOpen(previous => !previous)
  }

  const handleDropdownClose = (event?: MouseEvent<HTMLLIElement> | (MouseEvent | TouchEvent) | any, url?: string) => {
    if (url) {
      router.push(getLocalizedUrl(url, locale as Locale))
    }

    if (anchorRef.current && anchorRef.current.contains(event?.target as HTMLElement)) {
      return
    }

    setOpen(false)
  }

  const handleUserLogout = async () => {
    try {
      await authApi.logout()
    } catch {}
    // authApi clears before transport; a late response must preserve a newer login.
    window.location.href = getLocalizedUrl('/login', locale as Locale)
  }

  const getRoleTitle = (role?: string | null) => {
    switch (role) {
      case 'Admin': return t.userMenu.admin
      case 'Secretary': return t.userMenu.secretary
      case 'SecretaryDirector': return t.userMenu.secretaryDirector
      case 'Employee': return t.userMenu.employee
      default: return role || ''
    }
  }

  const displayName = user ? user.name || t.userMenu.unknownUser : t.userMenu.signedOut

  return (
    <>
      <Badge
        ref={anchorRef}
        overlap='circular'
        badgeContent={<BadgeContentSpan onClick={handleDropdownOpen} />}
        invisible={!user}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        className='mis-2'
      >
        <Avatar
          ref={anchorRef}
          alt={displayName}
          role='button'
          tabIndex={0}
          aria-label={t.userMenu.openMenu}
          aria-haspopup='menu'
          aria-expanded={open}
          onClick={handleDropdownOpen}
          onKeyDown={event => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault()
              handleDropdownOpen()
            }
          }}
          className='cursor-pointer bs-[38px] is-[38px]'
        ><i className='tabler-user' aria-hidden='true' /></Avatar>
      </Badge>
      <Popper
        open={open}
        transition
        disablePortal
        placement='bottom-end'
        anchorEl={anchorRef.current}
        className='min-is-[240px] !mbs-3 z-[1]'
      >
        {({ TransitionProps, placement }) => (
          <Fade
            {...TransitionProps}
            style={{
              transformOrigin: placement === 'bottom-end' ? 'right top' : 'left top'
            }}
          >
            <Paper className={settings.skin === 'bordered' ? 'border shadow-none' : 'shadow-lg'}>
              <ClickAwayListener onClickAway={e => handleDropdownClose(e)}>
                <MenuList>
                  <div className='flex items-center plb-2 pli-6 gap-2' tabIndex={-1}>
                    <Avatar alt={displayName}><i className='tabler-user' aria-hidden='true' /></Avatar>
                    <div className='flex items-start flex-col'>
                      <Typography className='font-medium' color='text.primary'>
                        {displayName}
                      </Typography>
                      {user?.role && <Typography variant='caption' color='text.secondary'>
                        {getRoleTitle(user?.role)}
                      </Typography>}
                    </div>
                  </div>
                  <Divider className='mlb-1' />
                  <MenuItem className='mli-2 gap-3' onClick={e => handleDropdownClose(e, '/dashboards/overview')}>
                    <i className='tabler-smart-home text-[22px]' />
                    <Typography color='text.primary'>{t.userMenu.dashboard}</Typography>
                  </MenuItem>
                  <div className='flex items-center plb-1.5 pli-3'>
                    <Button
                      fullWidth
                      color={user ? 'error' : 'primary'}
                      size='small'
                      variant='contained'
                      onClick={user ? handleUserLogout : () => handleDropdownClose(undefined, '/login')}
                      endIcon={<i className={user ? 'tabler-logout' : 'tabler-login'} />}
                    >
                      {user ? t.userMenu.logout : t.userMenu.signIn}
                    </Button>
                  </div>
                </MenuList>
              </ClickAwayListener>
            </Paper>
          </Fade>
        )}
      </Popper>
    </>
  )
}

export default UserDropdown
