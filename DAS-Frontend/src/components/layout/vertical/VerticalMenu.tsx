'use client'

// React Imports
import { useState, useEffect } from 'react'

// Next Imports
import { useParams } from 'next/navigation'

// MUI Imports
import { useTheme } from '@mui/material/styles'

// Third-party Imports
import PerfectScrollbar from 'react-perfect-scrollbar'

// Type Imports
import type { getDictionary } from '@/utils/getDictionary'
import type { VerticalMenuContextProps } from '@menu/components/vertical-menu/Menu'

// Component Imports
import { Menu, SubMenu, MenuItem, MenuSection } from '@menu/vertical-menu'

// Hook Imports
import useVerticalNav from '@menu/hooks/useVerticalNav'

// Styled Component Imports
import StyledVerticalNavExpandIcon from '@menu/styles/vertical/StyledVerticalNavExpandIcon'

// Style Imports
import menuItemStyles from '@core/styles/vertical/menuItemStyles'
import menuSectionStyles from '@core/styles/vertical/menuSectionStyles'
import { tokenManager } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

type RenderExpandIconProps = {
  open?: boolean
  transitionDuration?: VerticalMenuContextProps['transitionDuration']
}

type Props = {
  dictionary: Awaited<ReturnType<typeof getDictionary>>
  scrollMenu: (container: any, isPerfectScrollbar: boolean) => void
}

const RenderExpandIcon = ({ open, transitionDuration }: RenderExpandIconProps) => (
  <StyledVerticalNavExpandIcon open={open} transitionDuration={transitionDuration}>
    <i className='tabler-chevron-right' />
  </StyledVerticalNavExpandIcon>
)

const VerticalMenu = ({ scrollMenu }: Props) => {
  // Hooks
  const theme = useTheme()
  const verticalNavOptions = useVerticalNav()
  const params = useParams()

  // RBAC State
  const [userRole, setUserRole] = useState<string>('Admin')

  useEffect(() => {
    const user = tokenManager.getUser()
    if (user && user.role) {
      setUserRole(user.role)
    }
  }, [])

  // Vars
  const { isBreakpointReached, transitionDuration } = verticalNavOptions
  const { lang: locale } = params

  const ScrollWrapper = isBreakpointReached ? 'div' : PerfectScrollbar

  // Dictionary Hook
  const { t } = useAppDictionary()

  const isAdmin = userRole === 'Admin'
  const isSecretary = userRole === 'Secretary'
  const isDirectorSec = userRole === 'SecretaryDirector'
  const isEmployee = userRole === 'Employee'

  return (
    <ScrollWrapper
      {...(isBreakpointReached
        ? {
            className: 'bs-full overflow-y-auto overflow-x-hidden',
            onScroll: container => scrollMenu(container, false)
          }
        : {
            options: { wheelPropagation: false, suppressScrollX: true },
            onScrollY: container => scrollMenu(container, true)
          })}
    >
      <Menu
        popoutMenuOffset={{ mainAxis: 23 }}
        menuItemStyles={menuItemStyles(verticalNavOptions, theme)}
        renderExpandIcon={({ open }) => <RenderExpandIcon open={open} transitionDuration={transitionDuration} />}
        renderExpandedMenuItemIcon={{ icon: <i className='tabler-circle text-xs' /> }}
        menuSectionStyles={menuSectionStyles(verticalNavOptions, theme)}
      >
        {/* Khối 1: Tổng Quan */}
        <MenuSection label={t.nav.overview}>
          <MenuItem href={`/${locale}/dashboards/overview`} icon={<i className='tabler-chart-pie-2' />}>
            {t.nav.dashboard}
          </MenuItem>
        </MenuSection>

        {/* Khối 2: Nghiệp Vụ Quản Lý Công Văn & Đối Tác */}
        <MenuSection label={t.nav.operations}>
          <SubMenu label={t.nav.docManagement} icon={<i className='tabler-file-text' />}>
            <MenuItem href={`/${locale}/apps/documents/list`} icon={<i className='tabler-files' />}>
              {t.nav.docList || 'Danh Sách Công Văn'}
            </MenuItem>
            {(isAdmin || isSecretary) && (
              <MenuItem href={`/${locale}/apps/documents/add`} icon={<i className='tabler-plus' />}>
                {t.nav.addDoc}
              </MenuItem>
            )}
            {(isAdmin || isSecretary) && (
              <MenuItem href={`/${locale}/apps/email-integration`} icon={<i className='tabler-mail-cog' />}>
                {t.nav.emailIntegration}
              </MenuItem>
            )}
          </SubMenu>

          {(isAdmin || isSecretary || isDirectorSec) && (
            <SubMenu label={t.nav.partnerManagement} icon={<i className='tabler-building' />}>
              <MenuItem href={`/${locale}/apps/partners/list`}>{t.nav.partnerList}</MenuItem>
            </SubMenu>
          )}
        </MenuSection>

        {/* Khối 3: Hệ Thống - CHỈ ADMIN */}
        {isAdmin && (
          <MenuSection label={t.nav.system}>
            <SubMenu label={t.nav.userManagement} icon={<i className='tabler-users' />}>
              <MenuItem href={`/${locale}/apps/user/list`}>{t.nav.userList}</MenuItem>
              <MenuItem href={`/${locale}/apps/roles`}>{t.nav.rolesPermissions}</MenuItem>
            </SubMenu>
          </MenuSection>
        )}
      </Menu>
    </ScrollWrapper>
  )
}

export default VerticalMenu
