'use client'

// Third-party Imports
import classnames from 'classnames'

// Type Imports

// Component Imports
import NavToggle from './NavToggle'
import NavSearch from '@components/layout/shared/search'
import LanguageDropdown from '@components/layout/shared/LanguageDropdown'
import ModeDropdown from '@components/layout/shared/ModeDropdown'
import ShortcutsDropdown from '@components/layout/shared/ShortcutsDropdown'
import DasNotificationsDropdown from '@components/layout/shared/DasNotificationsDropdown'
import UserDropdown from '@components/layout/shared/UserDropdown'

import { useDasLauncher } from '@/hooks/useDasLauncher'

// Util Imports
import { verticalLayoutClasses } from '@layouts/utils/layoutClasses'

const NavbarContent = () => {
  const { shortcuts, refreshCapabilities } = useDasLauncher()

  return (
    <div className={classnames(verticalLayoutClasses.navbarContent, 'flex flex-wrap items-center justify-between gap-x-2 gap-y-1 is-full')}>
      <div className='flex shrink-0 items-center gap-2'>
        <NavToggle />
        <NavSearch />
      </div>
      <div className='flex min-w-0 flex-wrap items-center justify-end'>
        <LanguageDropdown />
        <ModeDropdown />
        <ShortcutsDropdown shortcuts={shortcuts} onOpen={refreshCapabilities} />
        <DasNotificationsDropdown />
        <UserDropdown />
      </div>
    </div>
  )
}

export default NavbarContent
