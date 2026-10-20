// Hook Imports
import useHorizontalNav from '@menu/hooks/useHorizontalNav'
import AccessibleNavToggle from '../vertical/NavToggle'

const NavToggle = () => {
  // Hooks
  const { isBreakpointReached } = useHorizontalNav()

  return isBreakpointReached ? <AccessibleNavToggle /> : null
}

export default NavToggle
