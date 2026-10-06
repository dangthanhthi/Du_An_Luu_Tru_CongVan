export type DasNavigationContext = {
  pathname: string
  search: string
  lang: string
  capabilities: string[]
}

export type DasNavigationLabel =
  | 'dashboard'
  | 'incomingDocs'
  | 'outgoingDocs'
  | 'internalDocs'
  | 'registerNew'
  | 'allDocs'
  | 'mineDocs'
  | 'departmentDocs'
  | 'cancelledDocs'
  | 'externalEntities'
  | 'organizationDirectory'
  | 'businessCatalogs'
  | 'emailIntegration'
  | 'incompleteReports'
  | 'myStaff'

export type DasNavigationItem = {
  id: string
  labelKey: DasNavigationLabel
  icon: string
  href: string
  active: boolean
}

const documentKinds = [
  {
    kind: 'Incoming',
    labelKey: 'incomingDocs',
    icon: 'tabler-arrow-down-left',
    registerCapability: 'DocumentRegisterIncoming'
  },
  {
    kind: 'Outgoing',
    labelKey: 'outgoingDocs',
    icon: 'tabler-arrow-up-right',
    registerCapability: 'DocumentRegisterDepartment'
  },
  {
    kind: 'Internal',
    labelKey: 'internalDocs',
    icon: 'tabler-file-description',
    registerCapability: 'DocumentRegisterDepartment'
  }
] as const

const documentViews = [
  { view: 'all', labelKey: 'allDocs', icon: 'tabler-files' },
  { view: 'mine', labelKey: 'mineDocs', icon: 'tabler-user' },
  { view: 'department', labelKey: 'departmentDocs', icon: 'tabler-building' },
  { view: 'cancelled', labelKey: 'cancelledDocs', icon: 'tabler-file-x' }
] as const

export const buildDasNavigation = ({ pathname, search, lang, capabilities }: DasNavigationContext) => {
  const query = new URLSearchParams(search)
  const kinds = query.getAll('kind')
  const views = query.getAll('view')
  const activeKind = kinds.length === 1 ? documentKinds.find(entry => entry.kind === kinds[0])?.kind : undefined
  const activeView = views.length === 1 ? documentViews.find(entry => entry.view === views[0])?.view : undefined
  const grantedCapabilities = new Set(capabilities)
  const listPath = `/${lang}/apps/documents/list`
  const registerPath = `/${lang}/apps/documents/add`

  const dashboard: DasNavigationItem = {
    id: 'dashboard',
    labelKey: 'dashboard',
    icon: 'tabler-chart-pie-2',
    href: `/${lang}/dashboards/overview`,
    active: pathname === `/${lang}/dashboards/overview`
  }

  const documentGroups = documentKinds.map(({ kind, labelKey, icon, registerCapability }) => {
    const items: DasNavigationItem[] = []

    if (grantedCapabilities.has(registerCapability)) {
      items.push({
        id: `${kind}-register`,
        labelKey: 'registerNew',
        icon: 'tabler-plus',
        href: `${registerPath}?kind=${kind}`,
        active: pathname === registerPath && activeKind === kind
      })
    }

    for (const { view, labelKey: viewLabel, icon: viewIcon } of documentViews) {
      items.push({
        id: `${kind}-${view}`,
        labelKey: viewLabel,
        icon: viewIcon,
        href: `${listPath}?kind=${kind}&view=${view}`,
        active: pathname === listPath && activeKind === kind && activeView === view
      })
    }

    return { kind, labelKey, icon, items }
  })

  const partners: DasNavigationItem = {
    id: 'partners',
    labelKey: 'externalEntities',
    icon: 'tabler-building',
    href: `/${lang}/apps/partners/list`,
    active: pathname === `/${lang}/apps/partners/list`
  }

  // Directory reads are scoped by the server for the authenticated user.
  const settings: DasNavigationItem[] = [
    {
      id: 'organization-directory',
      labelKey: 'organizationDirectory',
      icon: 'tabler-sitemap',
      href: `/${lang}/apps/settings/organization`,
      active: pathname === `/${lang}/apps/settings/organization`
    }
  ]

  if (grantedCapabilities.has('CatalogManage')) {
    settings.push({ id: 'business-catalogs', labelKey: 'businessCatalogs', icon: 'tabler-list-details',
      href: `/${lang}/apps/settings/catalogs`, active: pathname === `/${lang}/apps/settings/catalogs` })
  }

  if (grantedCapabilities.has('MailboxManage')) {
    settings.push({
      id: 'email-fax',
      labelKey: 'emailIntegration',
      icon: 'tabler-mail-cog',
      href: `/${lang}/apps/email-integration`,
      active: pathname === `/${lang}/apps/email-integration`
    })
  }

  const workspace: DasNavigationItem[] = [
    { id: 'incomplete-reports', labelKey: 'incompleteReports', icon: 'tabler-report', href: `/${lang}/apps/reports/incomplete`, active: pathname === `/${lang}/apps/reports/incomplete` },
    { id: 'my-staff', labelKey: 'myStaff', icon: 'tabler-users', href: `/${lang}/apps/tasks/my-staff`, active: pathname === `/${lang}/apps/tasks/my-staff` }
  ]
  const activeItem = [dashboard, ...documentGroups.flatMap(group => group.items), partners, ...settings, ...workspace]
    .find(item => item.active)

  return { dashboard, documentGroups, partners, settings, workspace, activeItemId: activeItem?.id ?? null }
}
