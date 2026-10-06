import assert from 'node:assert/strict'
import { afterEach, beforeEach, test } from 'node:test'
import { documentApi, partnerApi, tokenManager } from '../../src/services/api'

class Store implements Storage {
  private m = new Map<string,string>()
  get length(){return this.m.size}
  clear(){this.m.clear()} getItem(k:string){return this.m.get(k)??null}
  key(i:number){return [...this.m.keys()][i]??null}
  removeItem(k:string){this.m.delete(k)} setItem(k:string,v:string){this.m.set(k,v)}
}
const previousFetch = globalThis.fetch
const previousWindow = Object.getOwnPropertyDescriptor(globalThis,'window')
const previousStorage = Object.getOwnPropertyDescriptor(globalThis,'localStorage')
let storage:Store
beforeEach(()=>{
  storage=new Store()
  Object.defineProperty(globalThis,'window',{configurable:true,value:{}})
  Object.defineProperty(globalThis,'localStorage',{configurable:true,value:storage})
  storage.setItem('das_documents_store','[{"id":"old","documentNumber":"CV-DEN-2020-0130","title":"Historical"}]')
  storage.setItem('das_partners_store','[{"id":"old","fullName":"Cached old partner"}]')
})
afterEach(()=>{
  globalThis.fetch=previousFetch
  if(previousWindow)Object.defineProperty(globalThis,'window',previousWindow);else Reflect.deleteProperty(globalThis,'window')
  if(previousStorage)Object.defineProperty(globalThis,'localStorage',previousStorage);else Reflect.deleteProperty(globalThis,'localStorage')
})
for(const [name,run] of [
  ['document list',()=>documentApi.getList()],
  ['document detail',()=>documentApi.getById('old')],
  ['document update',()=>documentApi.update('old',{title:'Unsaved'})],
  ['document delete',()=>documentApi.delete('old')],
  ['partner list',()=>partnerApi.getList()],
  ['partner detail',()=>partnerApi.getById('old')],
  ['partner create',()=>partnerApi.create({fullName:'Unsaved'})],
  ['partner update',()=>partnerApi.update('old',{fullName:'Unsaved'})],
  ['partner delete',()=>partnerApi.delete('old')]
] as const){
  test(name+' never succeeds from local business cache after HTTP failure',async()=>{
    const documents=storage.getItem('das_documents_store'),partners=storage.getItem('das_partners_store')
    globalThis.fetch=async()=>Response.json({success:true,data:{id:'misleading'}},{status:503})
    await assert.rejects(run(),(error:unknown)=>(error as {status:number}).status===503)
    assert.equal(storage.getItem('das_documents_store'),documents)
    assert.equal(storage.getItem('das_partners_store'),partners)
  })
}
test('successful paged empty list is preserved and never replaced with cached rows',async()=>{
  const page={items:[],totalCount:0,pageNumber:1,pageSize:20}
  globalThis.fetch=async()=>Response.json({success:true,data:page})
  assert.deepEqual((await documentApi.getList()).data,page)
  assert.deepEqual((await partnerApi.getList()).data,page)
})
test('valid partner create returns only server ID and metadata',async()=>{
  const saved={id:'71943d92-d5e2-4daf-87ab-dbe22c0e2d53',fullName:'Server partner',shortName:'SP'}
  globalThis.fetch=async()=>Response.json({success:true,data:saved},{status:201})
  const before=storage.getItem('das_partners_store')
  assert.deepEqual((await partnerApi.create({fullName:'Server partner'})).data,saved)
  assert.equal(storage.getItem('das_partners_store'),before)
})
test('network failure and malformed JSON are visible errors without fallback documents',async()=>{
  globalThis.fetch=async()=>{throw new TypeError('Unavailable')}
  await assert.rejects(documentApi.getList())
  globalThis.fetch=async()=>new Response('bad JSON')
  await assert.rejects(documentApi.getList())
})
test('success false and invalid list shape cannot silently become a successful empty list',async()=>{
  globalThis.fetch=async()=>Response.json({success:false,data:[]})
  await assert.rejects(documentApi.getList())
  globalThis.fetch=async()=>Response.json({success:true,data:{unrecognized:[]}})
  await assert.rejects(documentApi.getList())
})
test('empty array legacy response is valid and contains no cached rows',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data:[]})
  assert.deepEqual((await documentApi.getList()).data,[])
})
test('refresh reads backend envelope and retries with returned access token',async()=>{
  tokenManager.setTokens('old-access','refresh-1')
  let calls=0
  globalThis.fetch=async(input,options)=>{
    calls++
    if(calls===1)return Response.json({success:false},{status:401})
    if(String(input).endsWith('/api/auth/refresh'))return Response.json({success:true,data:{accessToken:'new-access',refreshToken:'refresh-2'}})
    assert.equal(new Headers(options?.headers).get('Authorization'),'Bearer new-access')
    return Response.json({success:true,data:[]})
  }
  assert.deepEqual((await documentApi.getList()).data,[])
  assert.equal(tokenManager.getToken(),'new-access')
})

test('scoped document list rejects invalid paging before sending a request',async()=>{
  let calls=0
  globalThis.fetch=async()=>{calls++;return Response.json({success:true,data:{items:[],totalCount:0,pageNumber:1,pageSize:20}})}
  await assert.rejects(documentApi.getList({kind:'Incoming',view:'all',pageSize:0}),(e:any)=>e.status===400)
  assert.equal(calls,0)
})
test('v2 document list rejects unpaged arrays and mismatched server scope',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data:[]})
  await assert.rejects(documentApi.getList({kind:'Incoming',view:'all'}),(e:any)=>e.status===502)
  globalThis.fetch=async()=>Response.json({success:true,data:{items:[{id:'real',kind:'Outgoing',registrationNumber:'HISTORICAL',subject:'Server',status:'InProgress'}],totalCount:1,pageNumber:1,pageSize:20}})
  await assert.rejects(documentApi.getList({kind:'Incoming',view:'all'}),(e:any)=>e.status===502)
})
test('document detail propagates request cancellation',async()=>{
  const controller=new AbortController()
  globalThis.fetch=async(_input,options)=>{
    assert.equal(options?.signal,controller.signal)
    throw new DOMException('Cancelled','AbortError')
  }
  await assert.rejects(documentApi.getById('old',controller.signal),(e:any)=>e.name==='AbortError')
})
test('cancelling an authenticated retry preserves the refreshed session',async()=>{
  tokenManager.setTokens('old-access','refresh-1')
  let calls=0
  globalThis.fetch=async input=>{
    calls++
    if(calls===1)return Response.json({success:false},{status:401})
    if(String(input).endsWith('/api/auth/refresh'))return Response.json({success:true,data:{accessToken:'new-access',refreshToken:'refresh-2'}})
    throw new DOMException('Cancelled','AbortError')
  }
  await assert.rejects(documentApi.getById('old'),(e:any)=>e.name==='AbortError')
  assert.equal(tokenManager.getToken(),'new-access')
  assert.equal(tokenManager.getRefreshToken(),'refresh-2')
})

test('lost write response after successful refresh remains an unknown network outcome',async()=>{
  tokenManager.setTokens('old-access','refresh-1')
  let calls=0
  globalThis.fetch=async input=>{
    calls++
    if(calls===1)return Response.json({success:false},{status:401})
    if(String(input).endsWith('/api/auth/refresh'))return Response.json({success:true,data:{accessToken:'new-access',refreshToken:'refresh-2'}})
    throw new TypeError('Response lost after possible server commit')
  }
  await assert.rejects(documentApi.update('old',{title:'Write'}),(e:any)=>e.status===0)
  assert.equal(calls,3)
  assert.equal(tokenManager.getToken(),'new-access')
  assert.equal(tokenManager.getRefreshToken(),'refresh-2')
})
test('v2 document list rejects optional metadata that cannot be rendered as text',async()=>{
  for(const name of ['issuedDate','referenceNumber','companyCode','departmentName']){
    globalThis.fetch=async()=>Response.json({success:true,data:{items:[{id:'real',kind:'Incoming',registrationNumber:'HISTORICAL',subject:'Server',status:'InProgress',[name]:{unexpected:'object'}}],totalCount:1,pageNumber:1,pageSize:20}})
    await assert.rejects(documentApi.getList({kind:'Incoming',view:'all'}),(e:any)=>e.status===502)
  }
})

