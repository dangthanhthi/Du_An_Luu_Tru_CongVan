const fs = require('fs');
const path = require('path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.DAS_PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.DAS_UI_BASE_URL || 'http://127.0.0.1:3210';
const output = path.resolve(process.env.DAS_UI_QA_DIR || '.artifacts/qa/g1-ui');
fs.mkdirSync(output, {recursive:true});
const ids = {m:'00000000-0000-0000-0000-000000000001',a:'00000000-0000-0000-0000-000000000002',g:'00000000-0000-0000-0000-000000000003',b:'00000000-0000-0000-0000-000000000004'};
const units = [
 {id:ids.a,name:'Phòng kiểm thử A',code:'FIN',parentId:ids.m,isDepartment:true,isActive:true},
 {id:ids.g,name:'Nhóm kiểm thử',code:null,parentId:ids.a,isDepartment:false,isActive:true},
 {id:ids.b,name:'Phòng kiểm thử B',code:'ADM',parentId:ids.m,isDepartment:true,isActive:true}
];
const envelope = items=>({success:true,data:{items,pageNumber:1,pageSize:20,totalCount:items.length,authorizationRevision:1,verifiedAt:'2026-10-04T00:00:00+00:00'}});
const results = [], errors = [];
(async()=>{
 const browser = await chromium.launch({headless:true,executablePath:process.env.DAS_BROWSER_EXE});
 const context = await browser.newContext({viewport:{width:1440,height:1000},colorScheme:'light'});
 const page = await context.newPage();
 page.on('pageerror', e=>errors.push(e.message));
 let mode='data', released=false, pendingAResolve, observedAResolve;
 let observedA, pendingA;
 function resetRace(){observedA=new Promise(r=>observedAResolve=r);pendingA=new Promise(r=>pendingAResolve=r);released=false;}
 resetRace();
 const cors={'access-control-allow-origin':'*','access-control-allow-methods':'GET,OPTIONS','access-control-allow-headers':'*'};
 await page.route('**/api/v2/directory/**', async route=>{
   if(route.request().method()==='OPTIONS'){await route.fulfill({status:204,headers:cors});return;}
   const url=new URL(route.request().url());
   if(url.pathname.endsWith('/departments')){
     await route.fulfill({status:mode==='error'?503:200,headers:cors,json:mode==='error'?{success:false,message:'Fixture unavailable'}:envelope(mode==='empty'?[]:units)});
   }else{
     const id=url.searchParams.get('departmentId');
     if(mode==='race'&&id===ids.a){observedAResolve();await pendingA;}
     try{await route.fulfill({status:200,headers:cors,json:envelope([{id:id===ids.a?'staff-a':'staff-b',displayName:id===ids.a?'Nhân sự kiểm thử A':'Nhân sự kiểm thử B'}])});}catch(e){if(!released)throw e;}
   }
 });
 await page.route('**/api/documents?*', r=>r.fulfill({status:200,headers:cors,json:{success:true,data:{items:[],totalCount:0,pageNumber:1,pageSize:100}}}));
 await page.route('**/api/v2/documents?*', r=>{
   const query=new URL(r.request().url()).searchParams;
   return r.fulfill({status:200,headers:cors,json:{success:true,data:{items:[],totalCount:0,pageNumber:Number(query.get('pageNumber')||1),pageSize:Number(query.get('pageSize')||20)}}});
 });
 async function check(name, action){
   try{await action();results.push({name,status:'Passed'});}
   catch(e){results.push({name,status:'Failed',message:e.message});await page.screenshot({path:path.join(output,name.replace(/[^a-z0-9]/gi,'-')+'-failed.png'),fullPage:true});}
 }
 await check('directory-reads-server-data-and-group-has-no-staff-action',async()=>{
   await page.goto(base+'/vi/apps/settings/organization');
   await page.getByRole('table',{name:'Phòng ban và nhóm'}).waitFor();
   assert.equal(await page.getByRole('button',{name:'Xem nhân sự: Nhóm kiểm thử'}).count(),0);
   await page.getByRole('button',{name:'Xem nhân sự: Phòng kiểm thử B'}).click();
   await page.getByRole('cell',{name:'Nhân sự kiểm thử B',exact:true}).waitFor();
   assert.equal(await page.getByRole('button',{name:/Tạo phòng|Thêm người dùng/}).count(),0);
   await page.screenshot({path:path.join(output,'directory-desktop-light.png'),fullPage:true});
 });
 await check('changing-department-cancels-late-staff-response',async()=>{
   mode='race';resetRace();await page.reload();
   await page.getByRole('button',{name:'Xem nhân sự: Phòng kiểm thử A'}).click();
   await observedA;
   await page.getByRole('button',{name:'Xem nhân sự: Phòng kiểm thử B'}).click();
   await page.getByRole('cell',{name:'Nhân sự kiểm thử B',exact:true}).waitFor();
   released=true;pendingAResolve();
   assert.equal(await page.getByRole('cell',{name:'Nhân sự kiểm thử A',exact:true}).count(),0);
   mode='data';
 });
 await check('directory-503-is-error-and-retry-recovers',async()=>{
   mode='error';await page.reload();
   await page.getByRole('alert').filter({hasText:'Danh bạ tổ chức đang tạm thời không khả dụng.'}).waitFor();
   assert.equal(await page.getByRole('table',{name:'Phòng ban và nhóm'}).count(),0);
   mode='data';await page.getByRole('button',{name:'Thử lại',exact:true}).click();
   await page.getByRole('table',{name:'Phòng ban và nhóm'}).waitFor();
 });
 await check('directory-empty-success-does-not-show-fixture-rows',async()=>{
   mode='empty';await page.reload();
   await page.getByText('Không có phòng ban hoặc nhóm trong phạm vi của bạn.',{exact:true}).waitFor();
   assert.equal(await page.getByRole('button',{name:'Xem nhân sự: Phòng kiểm thử A'}).count(),0);
   mode='data';
 });
 await check('mobile-directory-has-no-page-horizontal-overflow',async()=>{
   await page.setViewportSize({width:390,height:844});
   await page.reload();await page.getByRole('table',{name:'Phòng ban và nhóm'}).waitFor();
   const size=await page.evaluate(()=>({width:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth}));
   assert.ok(size.scroll<=size.width+1,JSON.stringify(size));
   await page.screenshot({path:path.join(output,'directory-mobile-light.png'),fullPage:true});
   await page.setViewportSize({width:1440,height:1000});
 });
 await check('sidebar-document-query-has-one-active-link',async()=>{
   await page.goto(base+'/vi/apps/documents/list?kind=Outgoing&view=mine');
   await page.locator('a[aria-current="page"][href*="documents/list"]').waitFor();
   assert.equal(await page.locator('a[aria-current="page"][href*="documents/list"]').count(),1);
   assert.ok((await page.locator('a[aria-current="page"][href*="documents/list"]').getAttribute('href')).includes('kind=Outgoing&view=mine'));
 });
 await check('successful-empty-document-list-never-shows-demo-documents',async()=>{
   await page.goto(base+'/vi/apps/documents/list?kind=Incoming&view=all');
   await page.getByRole('table').first().waitFor();
   assert.equal(await page.getByRole('row').filter({hasText:'CV-DEN-2026-0001'}).count(),0);
 });
 await page.screenshot({path:path.join(output,'sidebar-documents.png'),fullPage:true});
 fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({fixture:'Stub; not live EAP/auth acceptance',utc:new Date().toISOString(),base,results,pageErrors:errors},null,2));
 await context.close();await browser.close();
 console.log(JSON.stringify({passed:results.filter(r=>r.status==='Passed').length,failed:results.filter(r=>r.status==='Failed'),pageErrors:errors},null,2));
 process.exitCode=results.some(r=>r.status==='Failed')||errors.length?1:0;
})().catch(e=>{console.error(e);process.exit(1)});

