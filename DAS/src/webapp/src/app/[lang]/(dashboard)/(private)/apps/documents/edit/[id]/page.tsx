// Component Imports
import EditDocumentForm from '@views/apps/documents/edit/EditDocumentForm'

const EditDocumentPage = async (props: { params: Promise<{ id: string }> }) => {
  const params = await props.params
  return <EditDocumentForm id={params.id} />
}

export default EditDocumentPage
