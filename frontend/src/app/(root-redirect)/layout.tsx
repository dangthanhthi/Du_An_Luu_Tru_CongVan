import type { ReactNode } from 'react'

// Root redirect has its own layout; locale routes retain their existing HTML root.
export default function RedirectLayout({ children }: { children: ReactNode }) {
  return <html lang='vi'><body>{children}</body></html>
}
