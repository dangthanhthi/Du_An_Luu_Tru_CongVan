// Component Imports
import NotAuthorized from '@views/NotAuthorized'

// Server Action Imports
import { getServerMode } from '@core/utils/serverHelpers'

const Error401 = async ({ params }: { params: Promise<{ lang: string }> }) => {
  // Vars
  const mode = await getServerMode()
  const { lang } = await params
  const vi = lang === 'vi'

  return <NotAuthorized mode={mode} sessionStatus
    title={vi ? 'Vui lòng đăng nhập để tiếp tục' : 'Please sign in to continue'}
    description={vi ? 'Bạn cần đăng nhập để truy cập hệ thống quản lý công văn DAS.' : 'Sign in to access the DAS document administration system.'}
    action={{ label: vi ? 'Về trang chủ' : 'Back to home', href: `/${lang}` }}
    secondaryAction={{ label: vi ? 'Đăng nhập' : 'Sign in', href: `/${lang}/login` }} />
}

export default Error401
