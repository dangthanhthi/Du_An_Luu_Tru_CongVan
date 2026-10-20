'use client'

// React Imports

// Next Imports
import { useParams, usePathname, useSearchParams } from 'next/navigation'

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
import { useCachedCapabilityHints } from '@/hooks/useDasLauncher'
import { useAppDictionary } from '@/hooks/useDictionary'
import { i18n } from '@/configs/i18n'
import { buildDasNavigation } from '@/components/layout/shared/dasNavigation'

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
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const { t } = useAppDictionary()

  const { capabilities } = useCachedCapabilityHints()

  // Vars
  const { transitionDuration } = verticalNavOptions
  const locale = typeof params.lang === 'string' ? params.lang : i18n.defaultLocale
  const navigation = buildDasNavigation({ pathname, search: searchParams.toString(), lang: locale, capabilities })

  return (
    <HorizontalNav
      switchToVertical
      verticalNavContent={VerticalNavContent}
      verticalNavProps={{
        id: 'das-navigation',
        inert: verticalNavOptions.isBreakpointReached && !verticalNavOptions.isToggled ? true : undefined,
        'aria-hidden': verticalNavOptions.isBreakpointReached && !verticalNavOptions.isToggled ? true : undefined,
        role: verticalNavOptions.isBreakpointReached && verticalNavOptions.isToggled ? 'dialog' : undefined,
        'aria-modal': verticalNavOptions.isBreakpointReached && verticalNavOptions.isToggled ? true : undefined,
        'aria-label': locale === 'en' ? 'Main navigation' : 'Điều hướng chính',
        backdropLabel: locale === 'en' ? 'Close navigation' : 'Đóng điều hướng',
        customStyles: verticalNavigationCustomStyles(verticalNavOptions, theme),
        backgroundColor: 'var(--mui-palette-background-paper)'
      }}
    >
      {/* Menu primitives observe pathname only; remount when the active query item changes. */}
      <Menu
        key={`${pathname}:${navigation.activeItemId ?? 'none'}`}
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
        <MenuItem href={navigation.dashboard.href} icon={<i className={navigation.dashboard.icon} />}>
          {t.nav[navigation.dashboard.labelKey]}
        </MenuItem>

        {navigation.documentGroups.map(group => (
          <SubMenu key={group.kind} label={t.nav[group.labelKey]} icon={<i className={group.icon} />}>
            {group.items.map(item => (
              <MenuItem
                key={item.id}
                href={item.href}
                icon={<i className={item.icon} />}
                exactMatch={false}
                activeUrl={item.active ? pathname : '#das-navigation-inactive'}
                aria-current={item.active ? 'page' : undefined}
              >
                {t.nav[item.labelKey]}
              </MenuItem>
            ))}
          </SubMenu>
        ))}
        <MenuItem href={navigation.partners.href} icon={<i className={navigation.partners.icon} />}>
          {t.nav[navigation.partners.labelKey]}
        </MenuItem>
        {navigation.workspace.map(item => <MenuItem key={item.id} href={item.href} icon={<i className={item.icon} />} aria-current={item.active ? 'page' : undefined}>{t.nav[item.labelKey]}</MenuItem>)}

        {navigation.settings.length > 0 && (
          <SubMenu label={t.nav.settings} icon={<i className='tabler-settings' />}>
            {navigation.settings.map(item => (
              <MenuItem key={item.id} href={item.href} icon={<i className={item.icon} />}>
                {t.nav[item.labelKey]}
              </MenuItem>
            ))}
          </SubMenu>
        )}
      </Menu>
    </HorizontalNav>
  )
}

export default HorizontalMenu
