declare module 'tailwindcss-logical'

declare module '@tanstack/table-core' {
  import type { FilterFn } from '@tanstack/react-table'
  export interface FilterFns {
    fuzzy: FilterFn<unknown>
  }
  export type * from '@tanstack/react-table'
}
