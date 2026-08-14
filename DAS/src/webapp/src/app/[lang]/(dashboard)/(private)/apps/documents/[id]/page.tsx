// Component Imports
import DocumentDetail from '@views/apps/documents/detail'

const DocumentDetailPage = async (props: { params: Promise<{ id: string }> }) => {
  const params = await props.params
  return <DocumentDetail id={params.id} />
}

export default DocumentDetailPage
