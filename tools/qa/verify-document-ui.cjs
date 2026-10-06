const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {chromium} = require(process.env.DAS_PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.DAS_UI_BASE_URL || 'http://127.0.0.1:3211';
const output = path.resolve(process.env.DAS_UI_QA_DIR || '.artifacts/qa/document-ui');
fs.mkdirSync(output,{recursive:true});
const row = (kind='Incoming',subject='Server subject') => ({id:'11111111-1111-4111-8111-111111111111',kind,registrationNumber:'20-12-0130/HL/C&P',subject,status:'Distributed',issuedDate:null,referenceNumber:'PARTNER-42',senderPartnerName:'Server partner',companyCode:'HL',isComplete:false,allowedActions:[],documentNumber:'20-12-0130/HL/C&P',title:subject,direction:kind.toLowerCase(),partnerName:'Server partner',summary:'{"subject":"OCR impostor","issuedDate":"2030-01-01"}'});
const results=[],errors=[];
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.DAS_BROWSER_EXE});
 const context=await browser.newContext({viewport:{width:1440,height:1000}});
 const page=await context.newPage();page.setDefaultTimeout(6000);
 page.on('pageerror',e=>errors.push(e.message));
 let mode='data',requests=[],release,seen;
 const cors={'access-control-allow-origin':'*','access-control-allow-methods':'GET,OPTIONS','access-control-allow-headers':'*'};
 await page.route('**/api/**',async route=>{
  if(route.request().method()==='OPTIONS')return route.fulfill({status:204,headers:cors});
  const url=new URL(route.request().url());
  if(!/\/documents(?:\/|$)/.test(url.pathname))return route.continue();
  requests.push(url);
  if(mode==='error')return route.fulfill({status:503,headers:cors,json:{success:false,message:'Fixture unavailable'}});
  if(url.pathname.endsWith('/documents')){
   const kind=url.searchParams.get('kind')||'Incoming';
   if(mode==='race'&&kind==='Outgoing'){seen();await new Promise(r=>release=r);}
   const item=row(kind,kind==='Outgoing'?'Late outgoing subject':'Current server subject');
   if(mode==='malformed')item.issuedDate={unexpected:'object'};
   if(mode==='editable')item.allowedActions=['Edit'];
   if(mode==='missing-detail')item.id='22222222-2222-4222-8222-222222222222';
   const items=mode==='empty'?[]:[item];
   try{await route.fulfill({status:200,headers:cors,json:{success:true,data:{items,totalCount:mode==='paging'?21:items.length,pageNumber:Number(url.searchParams.get('pageNumber')||1),pageSize:Number(url.searchParams.get('pageSize')||20)}}});}catch(e){if(mode!=='race')throw e;}
  }else return route.fulfill({status:mode==='missing-detail'?404:200,headers:cors,json:mode==='missing-detail'?{success:false,message:'Không tìm thấy công văn.'}:{success:true,data:row()}});
 });
 async function check(name,action){try{await action();results.push({name,status:'Passed'});}catch(e){results.push({name,status:'Failed',message:e.message});await page.screenshot({path:path.join(output,name+'-failed.png'),fullPage:true,timeout:30000});}finally{if(release){release();release=undefined;}}}
 const list=async query=>{requests=[];await page.goto(base+'/vi/apps/documents/list?'+query);};
 await check('empty-response-removes-demo',async()=>{mode='empty';await list('kind=Incoming&view=all');await page.getByRole('table').waitFor();assert.equal(await page.getByRole('row').filter({hasText:'CV-DEN-2026-0001'}).count(),0);});
 await check('kind-view-use-v2-server-query',async()=>{mode='data';await list('kind=Internal&view=department');await page.getByRole('table').waitFor();assert.ok(requests.some(u=>u.pathname==='/api/v2/documents'&&u.searchParams.get('kind')==='Internal'&&u.searchParams.get('view')==='department'));});
 await check('historical-number-and-server-metadata',async()=>{await list('kind=Incoming&view=all');await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).waitFor();await page.getByText('Current server subject',{exact:true}).waitFor();assert.equal(await page.getByText('OCR impostor',{exact:true}).count(),0);assert.equal(await page.getByText('2030-01-01',{exact:true}).count(),0);await page.screenshot({path:path.join(output,'document-list-server.png'),fullPage:true,timeout:30000});});
 await check('missing-capabilities-hide-register-and-mail',async()=>{assert.equal(await page.getByRole('link',{name:/Thêm Công Văn|Quét Từ Email/i}).count(),0);assert.equal(await page.getByRole('button',{name:/Thêm Công Văn|Quét Từ Email/i}).count(),0);});
 await check('http-error-is-visible-and-retry-recovers',async()=>{mode='error';await list('kind=Incoming&view=all');await page.getByRole('alert').waitFor();assert.equal(await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).count(),0);mode='data';await page.getByRole('button',{name:'Thử lại',exact:true}).click();await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).waitFor();});
 await check('malformed-metadata-shows-error-without-render-crash',async()=>{mode='malformed';await list('kind=Incoming&view=all');await page.getByRole('alert').waitFor();assert.equal(await page.getByRole('table').count(),0);mode='data';});
 await check('capabilities-and-allowed-actions-enable-only-granted-links',async()=>{
  await page.evaluate(()=>localStorage.setItem('das_user',JSON.stringify({capabilities:['DocumentRegisterDepartment','MailboxManage']})));
  mode='editable';await list('kind=Outgoing&view=mine');await page.getByRole('table').waitFor();
  await page.getByRole('link',{name:'Thêm Công Văn',exact:true}).waitFor();assert.equal(await page.getByRole('link',{name:'Thêm Công Văn',exact:true}).getAttribute('href'),'/vi/apps/documents/add?kind=Outgoing');await page.getByRole('link',{name:'Quét Từ Email',exact:true}).waitFor();
  await page.locator('a[href="/vi/apps/documents/edit/11111111-1111-4111-8111-111111111111"]').waitFor();
  mode='data';await list('kind=Incoming&view=all');await page.getByRole('table').waitFor();
  assert.equal(await page.getByRole('link',{name:'Thêm Công Văn',exact:true}).count(),0);assert.equal(await page.locator('a[href*="/apps/documents/edit/"]').count(),0);
  await page.evaluate(()=>localStorage.removeItem('das_user'));
 });
 await check('invalid-scope-never-falls-back-to-all',async()=>{await list('kind=Unexpected&view=all');await page.getByRole('alert').waitFor();assert.equal(requests.length,0);});
 await check('paging-requests-server-page',async()=>{mode='paging';await list('kind=Incoming&view=mine');await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).waitFor();const response=page.waitForResponse(r=>{const u=new URL(r.url());return u.pathname==='/api/v2/documents'&&u.searchParams.get('pageNumber')==='2';});await page.getByRole('button',{name:'Trang tiếp theo',exact:true}).click();await response;assert.ok(requests.some(u=>u.searchParams.get('pageNumber')==='2'&&u.searchParams.get('view')==='mine'));mode='data';});
 await check('scope-change-ignores-late-response',async()=>{
  mode='data';await list('kind=Incoming&view=all');await page.getByRole('table').waitFor();
  mode='race';const observed=new Promise(r=>seen=r);await page.getByRole('tab',{name:/Công Văn Đi/i}).click();
  await Promise.race([observed,new Promise((_,reject)=>setTimeout(()=>reject(new Error('No outgoing request')),6000))]);
  const cancelled=page.waitForEvent('requestfailed',{predicate:r=>{const u=new URL(r.url());return u.pathname==='/api/v2/documents'&&u.searchParams.get('kind')==='Outgoing';}});await page.getByRole('tab',{name:/Công Văn Nội Bộ/i}).click();await cancelled;await page.getByText('Current server subject',{exact:true}).waitFor();release();release=undefined;
  assert.equal(await page.getByText('Late outgoing subject',{exact:true}).count(),0);mode='data';
 });
 await check('detail-error-clears-data-and-retries',async()=>{mode='error';await page.goto(base+'/vi/apps/documents/11111111-1111-4111-8111-111111111111');await page.getByRole('alert').waitFor();await page.getByRole('button',{name:'Thử lại',exact:true}).waitFor();mode='data';await page.getByRole('button',{name:'Thử lại',exact:true}).click();await page.getByText('Server subject',{exact:true}).first().waitFor();assert.equal(await page.getByText('OCR impostor',{exact:true}).count(),0);});
 await check('moving-from-loaded-detail-to-missing-id-shows-no-old-subject',async()=>{
  mode='data';await list('kind=Incoming&view=all');await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).click();await page.getByText('Server subject',{exact:true}).first().waitFor();
  await page.goBack();mode='missing-detail';await page.reload();await page.getByRole('link',{name:'20-12-0130/HL/C&P',exact:true}).click();await page.getByRole('alert').waitFor();
  assert.equal(await page.getByText('Server subject',{exact:true}).count(),0);mode='data';
 });
 await check('mobile-list-has-no-page-overflow',async()=>{await page.setViewportSize({width:390,height:844});await list('kind=Incoming&view=all');await page.getByRole('table').waitFor();const size=await page.evaluate(()=>({width:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth}));assert.ok(size.scroll<=size.width+1,JSON.stringify(size));await page.screenshot({path:path.join(output,'document-list-mobile.png'),fullPage:true,timeout:30000});});
 fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({fixture:'Stub; not live EAP/auth/documents acceptance',utc:new Date().toISOString(),base,results,pageErrors:errors},null,2));
 await browser.close();console.log(JSON.stringify({passed:results.filter(r=>r.status==='Passed').length,failed:results.filter(r=>r.status==='Failed'),pageErrors:errors},null,2));process.exitCode=results.some(r=>r.status==='Failed')||errors.length?1:0;
})().catch(e=>{console.error(e);process.exit(1)});
