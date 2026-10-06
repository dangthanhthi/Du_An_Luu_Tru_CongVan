// MUI Imports
import Grid from '@mui/material/Grid'

// Component Imports
import PartnerListTable from './PartnerListTable'

const PartnerList = () => {
  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <PartnerListTable />
      </Grid>
    </Grid>
  )
}

export default PartnerList
