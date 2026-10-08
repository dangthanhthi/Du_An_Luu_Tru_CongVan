import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { documentsV2Api } from '../../src/services/das/documents'
import { documentPdfApi } from '../../src/services/das/document-pdf'
const id='11111111-1111-4111-8111-111111111111', dept='22222222-2222-4222-8222-222222222222'
const write={id,registrationNumber:'26-10-0001/HL/ADM',version:1,status:'InProgress'}
const draft={kind:'Internal',companyCode:'HL',subject:'Submitted',originatorUserId:id,ownerDepartmentId:dept,sensitivity:'Normal'} as const
const original=globalThis.fetch
afterEach(()=>{globalThis.fetch=original})
test('v2 registration sends stable key and trusts only server number',async()=>{
  const requests:RequestInit[]=[]
  globalThis.fetch=async(url,init)=>{assert.equal(new URL(String(url)).pathname,'/api/v2/documents');requests.push(init!);return Response.json({success:true,data:write})}
  assert.deepEqual(await documentsV2Api.register(draft,'same-key'),write)
  await documentsV2Api.register(draft,'same-key')
  for(const r of requests){assert.equal(new Headers(r.headers).get('Idempotency-Key'),'same-key');assert.equal(JSON.parse(String(r.body)).subject,'Submitted');assert.equal(JSON.parse(String(r.body)).registrationNumber,undefined)}
})
test('v2 detail uses v2 route and rejects wrong resource',async()=>{
  globalThis.fetch=async url=>{assert.equal(new URL(String(url)).pathname,`/api/v2/documents/${id}`);return Response.json({success:true,data:{header:{...write,id:dept}}})}
  await assert.rejects(documentsV2Api.detail(id),(e:any)=>e.status===502)
})
test('status posts version and action, never a browser status projection',async()=>{
  globalThis.fetch=async(url,init)=>{assert.equal(new URL(String(url)).pathname,`/api/v2/documents/${id}/status`);assert.deepEqual(JSON.parse(String(init!.body)),{expectedVersion:4,action:'Restore'});return Response.json({success:true,data:{...write,version:5,status:'Distributed'}})}
  assert.equal((await documentsV2Api.status(id,4,'Restore')).version,5)
})
test('network ambiguity remains failure and same registration key can be retried',async()=>{
  globalThis.fetch=async()=>{throw new TypeError('lost reply')}
  await assert.rejects(documentsV2Api.register(draft,'same-key'),(e:any)=>e.status===0)
})
const detail={header:{...write,kind:'Internal',subject:'Saved',registrationDate:'2026-10-05',companyCode:'HL',sensitivity:'Normal',allowedActions:['Edit']},originatorUserId:id,ownerDepartmentId:dept,inputterUserId:id,lastModifierUserId:id,remark:null,details:null,recipients:[],relatedDocumentIds:[],pdfState:'None'}
for (const field of ['methodNameSnapshot', 'documentTypeNameSnapshot', 'categoryNameSnapshot']) test(`v2 detail rejects malformed historical label ${field}`, async () => {
  globalThis.fetch = async () => Response.json({ success: true, data: { ...detail, details: { [field]: { invalid: 'object' } } } })
  await assert.rejects(documentsV2Api.detail(id), (error: any) => error.status === 502)
})
test('v2 detail preserves saved historical labels independently of current catalogs', async () => {
  const saved = { ...detail, details: { methodCode: 'EMAIL', methodNameSnapshot: 'Historical method', documentTypeNameSnapshot: 'Historical type', categoryNameSnapshot: 'Historical category' } }
  globalThis.fetch = async () => Response.json({ success: true, data: saved })
  assert.deepEqual(await documentsV2Api.detail(id), saved)
})
test('valid v2 detail is accepted and abort propagates',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data:detail})
  assert.deepEqual(await documentsV2Api.detail(id),detail)
  globalThis.fetch=async()=>{throw new DOMException('aborted','AbortError')}
  await assert.rejects(documentsV2Api.detail(id),(e:any)=>e.name==='AbortError')
})
for(const [name,change] of [
  ['bad-version',(d:any)=>d.header.version=Number.MAX_SAFE_INTEGER+1],
  ['bad-action',(d:any)=>d.header.allowedActions=['Admin']],
  ['bad-details',(d:any)=>d.details={others:{text:'invalid'}}],
  ['bad-recipient',(d:any)=>d.recipients=[{referenceType:'ExternalEntity',referenceId:dept,name:{private:'value'}}]],
  ['bad-related',(d:any)=>d.relatedDocumentIds=['legacy-id']],
  ['bad-pdf-state',(d:any)=>d.pdfState='Clean']
] as const)test(`v2 detail rejects ${name}`,async()=>{const d=structuredClone(detail);change(d);globalThis.fetch=async()=>Response.json({success:true,data:d});await assert.rejects(documentsV2Api.detail(id),(e:any)=>e.status===502)})
test('options do not accept browser-invented target references',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data:{kind:'Internal',userId:id,canRegister:true,targets:[{originatorUserId:'fake',departmentId:dept}],catalogs:[],distributionTargets:[]}})
  await assert.rejects(documentsV2Api.options('Internal'),(e:any)=>e.status===502)
})
test('version conflict and forbidden remain errors',async()=>{
  for(const status of [403,409,503]){globalThis.fetch=async()=>Response.json({success:false},{status});await assert.rejects(documentsV2Api.status(id,1,'Restore'),(e:any)=>e.status===status)}
})
test('PDF content requires authenticated binary endpoint and valid signature',async()=>{
  globalThis.fetch=async(url,init)=>{assert.equal(new URL(String(url)).pathname,`/api/files/${id}`);assert.equal(init!.redirect,'error');return new Response('%PDF-1.7 test',{headers:{'Content-Type':'application/pdf'}})}
  assert.equal((await documentPdfApi.bytes(id)).type,'application/pdf')
  globalThis.fetch=async()=>new Response('not pdf',{headers:{'Content-Type':'application/pdf'}})
  await assert.rejects(documentPdfApi.bytes(id),(e:any)=>e.status===502)
})
test('PDF denied, wrong MIME and oversized content are refused',async()=>{
  for(const r of [new Response('no',{status:404}),new Response('html',{headers:{'Content-Type':'text/html'}}),new Response('%PDF-',{headers:{'Content-Type':'application/pdf','Content-Length':String(26*1024*1024)}})]){
    globalThis.fetch=async()=>r
    await assert.rejects(documentPdfApi.bytes(id))
  }
})
test('PDF replacement preserves operation and expected version on retry',async()=>{
  let count=0
  globalThis.fetch=async(url,init)=>{assert.equal(new URL(String(url)).pathname,`/api/v2/documents/${id}/pdf`);assert.deepEqual(JSON.parse(String(init!.body)),{operationId:dept,fileId:id,expectedVersion:2});count++;return Response.json({success:true,data:{operationId:dept,documentId:id,fileId:id,version:3}})}
  await documentPdfApi.replace(id,dept,id,2);await documentPdfApi.replace(id,dept,id,2);assert.equal(count,2)
})
