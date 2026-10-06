import assert from 'node:assert/strict'
import { afterEach, test } from 'node:test'
import { directoryApi } from '../../src/services/das/directory'
const original = globalThis.fetch
afterEach(()=>{ globalThis.fetch = original })
const department={id:'00000000-0000-0000-0000-000000000001',name:'Management',code:'MGT',parentId:null,isDepartment:true,isActive:true}
const page={items:[department],pageNumber:1,pageSize:20,totalCount:1,authorizationRevision:1,verifiedAt:'2026-10-04T00:00:00+00:00'}
test('directory client uses exact versioned scope and preserves server revision',async()=>{
  let requested=''
  globalThis.fetch=async(input)=>{requested=String(input);return Response.json({success:true,data:page})}
  assert.deepEqual(await directoryApi.getDepartments({includeGroups:true}),page)
  const url=new URL(requested)
  assert.equal(url.pathname,'/api/v2/directory/departments')
  assert.equal(url.searchParams.get('includeGroups'),'true')
})
for(const status of [401,403,503])test('directory HTTP '+status+' never becomes empty success',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data:page},{status})
  await assert.rejects(directoryApi.getDepartments(),(e:unknown)=>(e as {status:number}).status===status)
})
for(const data of [[],{...page,items:[{id:'broken'}]},{...page,authorizationRevision:0},{...page,verifiedAt:'invalid'}])
test('malformed directory response is rejected',async()=>{
  globalThis.fetch=async()=>Response.json({success:true,data})
  await assert.rejects(directoryApi.getDepartments(),(e:unknown)=>(e as {status:number}).status===502)
})
test('invalid page is rejected before querying broader data',async()=>{
  let called=false
  globalThis.fetch=async()=>{called=true;return Response.json({success:true,data:page})}
  await assert.rejects(directoryApi.getDepartments({pageSize:101}))
  assert.equal(called,false)
})
test('originator scope and request cancellation are forwarded',async()=>{
  const controller=new AbortController()
  globalThis.fetch=async(input,init)=>{
    const url=new URL(String(input))
    assert.equal(url.searchParams.get('departmentId'),department.id)
    assert.equal(url.searchParams.get('purpose'),'originator')
    assert.equal(init?.signal,controller.signal)
    return Response.json({success:true,data:{...page,items:[{id:'staff-id',displayName:'Staff'}]}})
  }
  assert.equal((await directoryApi.getUsers({departmentId:department.id,purpose:'originator'},controller.signal)).items[0].displayName,'Staff')
})

