// MUI Imports
import Grid from '@mui/material/Grid'

// Component Imports
import DocumentList from '@views/apps/documents/list'

const DocumentApp = async () => {
  return (
    <Grid container>
      <Grid size={{ xs: 12 }}>
        <DocumentList />
      </Grid>
    </Grid>
  )
}

export default DocumentApp
