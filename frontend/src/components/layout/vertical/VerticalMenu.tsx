'use client'

// React Imports

// Next Imports
import { useParams, usePathname, useSearchParams } from 'next/navigation'

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
import { useCachedCapabilityHints } from '@/hooks/useDasLauncher'
import { useAppDictionary } from '@/hooks/useDictionary'
import { i18n } from '@/configs/i18n'
import { buildDasNavigation } from '@/components/layout/shared/dasNavigation'

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
  const pathname = usePathname()
  const searchParams = useSearchParams()

  const { capabilities } = useCachedCapabilityHints()

  // Vars
  const { isBreakpointReached, transitionDuration } = verticalNavOptions
  const locale = typeof params.lang === 'string' ? params.lang : i18n.defaultLocale
  const navigation = buildDasNavigation({ pathname, search: searchParams.toString(), lang: locale, capabilities })

  const ScrollWrapper = isBreakpointReached ? 'div' : PerfectScrollbar

  // Dictionary Hook
  const { t } = useAppDictionary()

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
      {/* Menu primitives observe pathname only; remount when the active query item changes. */}
      <Menu
        key={`${pathname}:${navigation.activeItemId ?? 'none'}`}
        popoutMenuOffset={{ mainAxis: 23 }}
        menuItemStyles={menuItemStyles(verticalNavOptions, theme)}
        renderExpandIcon={({ open }) => <RenderExpandIcon open={open} transitionDuration={transitionDuration} />}
        renderExpandedMenuItemIcon={{ icon: <i className='tabler-circle text-xs' /> }}
        menuSectionStyles={menuSectionStyles(verticalNavOptions, theme)}
      >
        <MenuSection label={t.nav.overview}>
          <MenuItem href={navigation.dashboard.href} icon={<i className={navigation.dashboard.icon} />}>
            {t.nav[navigation.dashboard.labelKey]}
          </MenuItem>
        </MenuSection>

        <MenuSection label={t.nav.operations}>
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
        </MenuSection>

        {navigation.settings.length > 0 && (
          <MenuSection label={t.nav.settings}>
            {navigation.settings.map(item => (
              <MenuItem key={item.id} href={item.href} icon={<i className={item.icon} />}>
                {t.nav[item.labelKey]}
              </MenuItem>
            ))}
          </MenuSection>
        )}
      </Menu>
    </ScrollWrapper>
  )
}

export default VerticalMenu
