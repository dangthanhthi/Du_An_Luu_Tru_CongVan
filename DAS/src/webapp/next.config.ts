import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  basePath: process.env.BASEPATH,
  // Tesseract starts a separate Node worker at runtime. Next's file tracer
  // cannot discover that entry point from the dynamic worker path.
  outputFileTracingIncludes: {
    '/api/ocr/analyze': [
      './node_modules/tesseract.js/src/**/*',
      './node_modules/tesseract.js-core/**/*',
      './node_modules/regenerator-runtime/**/*',
      './node_modules/wasm-feature-detect/**/*',
      './node_modules/is-url/**/*',
      './node_modules/bmp-js/**/*',
      './node_modules/node-fetch/**/*',
      './node_modules/pdf-parse/**/*',
      './node_modules/pdfjs-dist/**/*',
      './node_modules/@napi-rs/canvas/**/*',
      './node_modules/@napi-rs/canvas-linux-x64-gnu/**/*'
    ]
  },
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
