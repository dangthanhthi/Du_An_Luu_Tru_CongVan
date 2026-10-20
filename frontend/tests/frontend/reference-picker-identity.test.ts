import assert from 'node:assert/strict'
import { test } from 'node:test'
import { componentHarness, nodes } from './helpers/component-harness'
const source=new URL('../../src/views/apps/documents/V2ReferencePicker.tsx',import.meta.url)
test('duplicate sender names use distinct identities and descriptive choice rows',()=>{
  const ui=componentHarness(source,{'@/hooks/useDictionary':{useAppDictionary:()=>({isEn:true})},'@/services/api':{documentApi:{}},'@/services/das/external-entities':{externalEntityApi:{}},'@/services/das/documents':{isDocumentGuid:()=>true}})
  const auto=nodes(ui.render({label:'Sender',disabled:true,multiple:false,value:[{id:'one',name:'ABC',description:'Address A'},{id:'two',name:'ABC',description:'Address B'}],onChange(){}})).find(n=>n.type==='Autocomplete')!
  assert.equal(auto.props.getOptionKey({id:'one',name:'ABC'}),'one');assert.equal(auto.props.getOptionKey({id:'two',name:'ABC'}),'two')
  const row=auto.props.renderOption({}, {id:'two',name:'ABC',description:'Address B'})
  assert.ok(nodes(row).some(n=>n.props.children==='Address B'));ui.unmount()
})
