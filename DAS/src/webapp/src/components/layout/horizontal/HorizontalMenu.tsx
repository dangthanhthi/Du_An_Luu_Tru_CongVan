'use client'

// React Imports
import { useState, useEffect } from 'react'

// Next Imports
import { useParams } from 'next/navigation'

// MUI Imports
import { useTheme } from '@mui/material/styles'

// Type Imports
import type { getDictionary } from '@/utils/getDictionary'
import type { VerticalMenuContextProps } from '@menu/components/vertical-menu/Menu'

// Component Imports
import HorizontalNav, { Menu, SubMenu, MenuItem } from '@menu/horizontal-menu'
import VerticalNavContent from './VerticalNavContent'

// Hook Imports
import useVerticalNav from '@menu/hooks/useVerticalNav'

// Styled Component Imports
import StyledHorizontalNavExpandIcon from '@menu/styles/horizontal/StyledHorizontalNavExpandIcon'
import StyledVerticalNavExpandIcon from '@menu/styles/vertical/StyledVerticalNavExpandIcon'

// Style Imports
import menuItemStyles from '@core/styles/horizontal/menuItemStyles'
import menuRootStyles from '@core/styles/horizontal/menuRootStyles'
import verticalMenuItemStyles from '@core/styles/vertical/menuItemStyles'
import verticalMenuSectionStyles from '@core/styles/vertical/menuSectionStyles'
import verticalNavigationCustomStyles from '@core/styles/vertical/navigationCustomStyles'
import { tokenManager } from '@/services/api'
import { useAppDictionary } from '@/hooks/useDictionary'

type RenderExpandIconProps = {
  level?: number
}

type Props = {
  dictionary: Awaited<ReturnType<typeof getDictionary>>
}

const RenderExpandIcon = ({ level }: RenderExpandIconProps) => (
  <StyledHorizontalNavExpandIcon level={level}>
    <i className='tabler-chevron-right' />
  </StyledHorizontalNavExpandIcon>
)

const RenderVerticalExpandIcon = ({
  open,
  transitionDuration
}: {
  open?: boolean
  transitionDuration?: VerticalMenuContextProps['transitionDuration']
}) => (
  <StyledVerticalNavExpandIcon open={open} transitionDuration={transitionDuration}>
    <i className='tabler-chevron-right' />
  </StyledVerticalNavExpandIcon>
)

const HorizontalMenu = ({ dictionary }: Props) => {
  // Hooks
  const verticalNavOptions = useVerticalNav()
  const theme = useTheme()
  const params = useParams()
  const { t } = useAppDictionary()

  // RBAC State
  const [userRole, setUserRole] = useState<string>('Admin')

  useEffect(() => {
    const user = tokenManager.getUser()
    if (user && user.role) {
      setUserRole(user.role)
    }
  }, [])

  // Vars
  const { transitionDuration } = verticalNavOptions
  const { lang: locale } = params

  const isAdmin = userRole === 'Admin'
  const isSecretary = userRole === 'Secretary'
  const isDirectorSec = userRole === 'SecretaryDirector'

  return (
    <HorizontalNav
      switchToVertical
      verticalNavContent={VerticalNavContent}
      verticalNavProps={{
        customStyles: verticalNavigationCustomStyles(verticalNavOptions, theme),
        backgroundColor: 'var(--mui-palette-background-paper)'
      }}
    >
      <Menu
        rootStyles={menuRootStyles(theme)}
        renderExpandIcon={({ level }) => <RenderExpandIcon level={level} />}
        menuItemStyles={menuItemStyles(theme, 'tabler-circle')}
        renderExpandedMenuItemIcon={{ icon: <i className='tabler-circle text-xs' /> }}
        popoutMenuOffset={{
          mainAxis: ({ level }) => (level && level > 0 ? 14 : 12),
          alignmentAxis: 0
        }}
        verticalMenuProps={{
          menuItemStyles: verticalMenuItemStyles(verticalNavOptions, theme),
          renderExpandIcon: ({ open }) => (
            <RenderVerticalExpandIcon open={open} transitionDuration={transitionDuration} />
          ),
          renderExpandedMenuItemIcon: { icon: <i className='tabler-circle text-xs' /> },
          menuSectionStyles: verticalMenuSectionStyles(verticalNavOptions, theme)
        }}
      >
        <MenuItem href={`/${locale}/dashboards/overview`} icon={<i className='tabler-chart-pie-2' />}>
          {t.nav.dashboard}
        </MenuItem>

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

        {isAdmin && (
          <SubMenu label={t.nav.system} icon={<i className='tabler-users' />}>
            <MenuItem href={`/${locale}/apps/user/list`}>{t.nav.userList}</MenuItem>
            <MenuItem href={`/${locale}/apps/roles`}>{t.nav.rolesPermissions}</MenuItem>
          </SubMenu>
        )}
      </Menu>
    </HorizontalNav>
  )
}

export default HorizontalMenu
