import { buildDasNavigation, type DasNavigationItem, type DasNavigationLabel } from './dasNavigation'

export type DasLauncherItem = { id: string; name: string; url: string; icon: string }
export type DasLauncherSection = { title: string; items: DasLauncherItem[] }
export type DasLauncherLabels = Record<DasNavigationLabel | 'overview' | 'operations' | 'settings', string>

export const buildDasLauncher = (lang: string, capabilities: string[], labels: DasLauncherLabels): DasLauncherSection[] => {
  const navigation = buildDasNavigation({ pathname: '', search: '', lang, capabilities })
  const item = (entry: DasNavigationItem, prefix?: string): DasLauncherItem => ({
    id: entry.id, name: prefix ? `${prefix} — ${labels[entry.labelKey]}` : labels[entry.labelKey],
    url: entry.href, icon: entry.icon
  })

  return [
    { title: labels.overview, items: [item(navigation.dashboard)] },
    ...navigation.documentGroups.map(group => ({
      title: labels[group.labelKey], items: group.items.map(entry => item(entry, labels[group.labelKey]))
    })),
    { title: labels.operations, items: [navigation.partners, ...navigation.workspace].map(entry => item(entry)) },
    { title: labels.settings, items: navigation.settings.map(entry => item(entry)) }
  ]
}

const searchable = (value: string) => value.trim().toLowerCase().normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd')

export const filterDasLauncher = (sections: DasLauncherSection[], query: string): DasLauncherSection[] => {
  const search = searchable(query)

  return sections.map(section => ({ ...section, items: section.items.filter(item =>
    searchable(section.title).includes(search) || searchable(item.name).includes(search)) }))
    .filter(section => section.items.length > 0)
}

export const buildDasShortcuts = (sections: DasLauncherSection[]) => {
  const selected = new Set(['dashboard', 'Incoming-all', 'Outgoing-all', 'Internal-all', 'my-staff',
    'incomplete-reports', 'partners', 'organization-directory', 'business-catalogs', 'email-fax'])

  return sections.flatMap(section => section.items.filter(item => selected.has(item.id)).map(item => ({
    url: item.url, icon: item.icon, title: item.name, subtitle: section.title
  })))
}

// Cache can only select presentation links. The API still checks authenticated resource scopes.
export const getCachedCapabilityHints = (hasToken: boolean, stored: unknown): string[] => {
  if (!hasToken || !stored || typeof stored !== 'object' || Array.isArray(stored)) return []
  const capabilities = (stored as Record<string, unknown>).capabilities

  return Array.isArray(capabilities) ? capabilities.filter((value): value is string =>
    typeof value === 'string' && value.trim().length > 0) : []
}
