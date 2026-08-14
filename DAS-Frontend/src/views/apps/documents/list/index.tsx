// MUI Imports
import Grid from '@mui/material/Grid'

// Component Imports
import DocumentListTable from './DocumentListTable'

const DocumentList = () => {
  return (
    <Grid container spacing={6}>
      <Grid size={{ xs: 12 }}>
        <DocumentListTable />
      </Grid>
    </Grid>
  )
}

export default DocumentList
