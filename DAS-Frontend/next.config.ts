import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  basePath: process.env.BASEPATH,
  typescript: {
    ignoreBuildErrors: true
  },
  redirects: async () => {
    return [
      {
        source: '/',
        destination: '/vi/dashboards/overview',
        permanent: false,
        locale: false
      },
      // Clean any accidental nested / cached double locales like /en/vi/...
      {
        source: '/:lang1(en|vi|fr|ar)/:lang2(en|vi|fr|ar)/:path*',
        destination: '/:lang2/:path*',
        permanent: false,
        locale: false
      },
      {
        source: '/:lang(en|vi|fr|ar)',
        destination: '/:lang/dashboards/overview',
        permanent: false,
        locale: false
      },
      {
        source: '/:path((?!en|vi|fr|ar|front-pages|images|samples|api|favicon.ico).*)*',
        destination: '/vi/:path*',
        permanent: false,
        locale: false
      }
    ]
  }
}

export default nextConfig
