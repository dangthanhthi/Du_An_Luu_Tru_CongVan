// MUI Imports
import Grid from '@mui/material/Grid'

// Component Imports
import StatCards from '@views/dashboards/overview/StatCards'
import DocumentChart from '@views/dashboards/overview/DocumentChart'
import RecentDocuments from '@views/dashboards/overview/RecentDocuments'
import DocumentByStatus from '@views/dashboards/overview/DocumentByStatus'

const DashboardOverview = async () => {
  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <StatCards />
      </Grid>
      <Grid size={{ xs: 12, lg: 8 }}>
        <DocumentChart />
      </Grid>
      <Grid size={{ xs: 12, lg: 4 }}>
        <DocumentByStatus />
      </Grid>
      <Grid size={{ xs: 12 }}>
        <RecentDocuments />
      </Grid>
    </Grid>
  )
}

export default DashboardOverview
