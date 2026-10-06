const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {chromium}=require(process.env.DAS_PLAYWRIGHT_MODULE||'playwright');
const out=path.resolve('.artifacts/qa/subsequent-20261005/browser');fs.mkdirSync(out,{recursive:true});
const base=process.env.DAS_UI_BASE_URL||'http://127.0.0.1:3212',id='d68a56e8-89ca-4ec5-bc6c-0e7f1d2a8ae7';
const results=[],pageErrors=[],queries=[];let offline=false,exportAllowed=true,staffOffline=false,notificationsRead=false;
const cors={'access-control-allow-origin':'*','access-control-allow-methods':'GET,PUT,OPTIONS','access-control-allow-headers':'*'};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.DAS_BROWSER_EXE});const context=await browser.newContext({viewport:{width:1440,height:1000}});const page=await context.newPage();page.setDefaultTimeout(9000);page.on('pageerror',e=>pageErrors.push(e.message));
 await page.route('**/api/**',async route=>{
  const url=new URL(route.request().url()),p=url.pathname;if(route.request().method()==='OPTIONS')return route.fulfill({status:204,headers:cors});
  const ok=data=>route.fulfill({status:200,headers:cors,json:{success:true,data}});
  if(p==='/api/notifications/unread-count')return ok({unreadCount:notificationsRead?0:1});
  if(p==='/api/notifications/my')return ok({items:[{id,recipientUserId:id,title:'Persisted notification',message:'Server notification body',actionUrl:null,relatedDocumentId:null,notificationType:'Info',isRead:notificationsRead,readAt:null,createdAt:'2026-10-05T01:00:00Z'}],totalCount:1,page:1,pageSize:20});
  if(p==='/api/notifications/'+id+'/read'){notificationsRead=true;return ok(null);}
  if(p.startsWith('/api/v2/reports/')&&offline||p==='/api/v2/my-staff'&&staffOffline)return route.fulfill({status:503,headers:cors,json:{success:false,code:'AUTHORITY_UNAVAILABLE'}});
  if(p==='/api/v2/reports/incomplete/export'){queries.push({export:true,...Object.fromEntries(url.searchParams)});return route.fulfill({status:200,headers:{...cors,'content-type':'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'},body:Buffer.from([80,75,3,4])});}
  if(p==='/api/v2/reports/incomplete'){
   const q=Object.fromEntries(url.searchParams);queries.push(q);const recent=q.includeRecent==='true';const total=recent?2:1;
   return ok({items:[{documentId:id,kind:'Internal',departmentId:id,departmentName:'ADM',hasAttachment:false,registrationNumber:'26-09-0001/HL/ADM',registeredDate:'2026-09-01',issueDate:null,originator:id,status:'InProgress',recipientList:[],version:1}],total,pageNumber:Number(q.pageNumber),pageSize:Number(q.pageSize),groups:[{departmentId:id,departmentName:'ADM',count:total}],evaluatedAt:'2026-10-05T01:00:00Z',canExport:exportAllowed});
  }
  if(p==='/api/v2/my-staff')return ok({staff:[{userId:id,name:'Scoped member',departmentId:id,departmentName:'IT'}],total:1,pageNumber:1,pageSize:20,tasks:null,taskState:'TMS_UNAVAILABLE'});
  return ok([]);
 });
 const check=async(name,fn)=>{try{await fn();results.push({name,status:'Passed'});}catch(e){results.push({name,status:'Failed',error:String(e)});}};
 const report=async()=>{await page.goto(base+'/vi/apps/reports/incomplete');await page.getByText('26-09-0001/HL/ADM',{exact:true}).waitFor();};
 await check('report-shows-authoritative-fields-without-subject',async()=>{await report();assert.equal(await page.getByRole('columnheader',{name:'Số công văn',exact:true}).count(),1);assert.equal(await page.getByRole('link',{name:'26-09-0001/HL/ADM'}).getAttribute('href'),'/vi/apps/documents/'+id);assert.equal(queries[0].includeRecent,'false');await page.screenshot({path:path.join(out,'report-desktop.png'),fullPage:true});});
 await check('dashboard-counts-use-same-report-scope',async()=>{await page.goto(base+'/vi/dashboards/overview');await page.getByText('ADM: 1',{exact:true}).waitFor();await page.getByRole('link',{name:'Xem báo cáo chi tiết',exact:true}).waitFor();await page.screenshot({path:path.join(out,'overview-desktop.png'),fullPage:true});await report();});
 await check('include-recent-requeries-server',async()=>{await page.getByLabel('Bao gồm công văn trong 7 ngày gần đây').check();await page.getByText(/2 hồ sơ/).waitFor();assert.equal(queries.at(-1).includeRecent,'true');});
 await check('export-retains-filters-and-omits-page',async()=>{const event=page.waitForEvent('download');await page.getByRole('button',{name:'Tải Excel',exact:true}).click();const download=await event;assert.equal(download.suggestedFilename(),'DAS-incomplete.xlsx');const q=queries.at(-1);assert.equal(q.export,true);assert.equal(q.includeRecent,'true');assert.equal(q.pageNumber,undefined);});
 await check('report-only-user-has-no-export-button',async()=>{exportAllowed=false;await page.getByRole('button',{name:'Tải lại',exact:true}).click();await page.getByText('26-09-0001/HL/ADM',{exact:true}).waitFor();assert.equal(await page.getByRole('button',{name:'Tải Excel',exact:true}).count(),0);});
 await check('authority-failure-clears-old-report',async()=>{offline=true;await page.getByRole('button',{name:'Tải lại',exact:true}).click();await page.getByRole('alert').filter({hasText:'chờ kết nối'}).waitFor();assert.equal(await page.getByText('26-09-0001/HL/ADM',{exact:true}).count(),0);offline=false;});
 await check('scoped-staff-with-unavailable-tms',async()=>{await page.goto(base+'/vi/apps/tasks/my-staff');await page.getByText('Scoped member',{exact:true}).waitFor();await page.getByRole('alert').filter({hasText:'TMS chưa kết nối'}).waitFor();await page.screenshot({path:path.join(out,'my-staff-desktop.png'),fullPage:true});});
 await check('staff-failure-clears-old-members',async()=>{staffOffline=true;await page.getByRole('button',{name:'Tải lại',exact:true}).click();await page.getByRole('alert').filter({hasText:'Danh sách nhân sự'}).waitFor();assert.equal(await page.getByText('Scoped member',{exact:true}).count(),0);staffOffline=false;});
 await check('report-mobile-contained-table-overflow',async()=>{await page.setViewportSize({width:390,height:844});await report();const width=await page.evaluate(()=>({client:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth}));assert.ok(width.scroll<=width.client+1,JSON.stringify(width));await page.screenshot({path:path.join(out,'report-mobile.png'),fullPage:true});});
 await check('bell-uses-persisted-notification-without-template-samples',async()=>{await page.getByRole('button',{name:'Thông báo',exact:true}).click();await page.getByText('Persisted notification',{exact:true}).waitFor();assert.equal(await page.getByText('Congratulations Flora 🎉',{exact:true}).count(),0);});
 await check('mark-read-is-persisted-by-server',async()=>{await page.getByRole('button',{name:'Đánh dấu đã đọc',exact:true}).click();await page.getByRole('button',{name:'Tải lại thông báo',exact:true}).waitFor();assert.equal(notificationsRead,true);await page.waitForFunction(()=>!Array.from(document.querySelectorAll('button')).some(x=>x.textContent==='Đánh dấu đã đọc'));await page.screenshot({path:path.join(out,'notifications-mobile.png'),fullPage:true});});
 fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({fixture:'Stub browser/API boundaries; not live authority/TMS/SMTP acceptance',results,pageErrors},null,2));await browser.close();console.log(JSON.stringify({passed:results.filter(x=>x.status==='Passed').length,failed:results.filter(x=>x.status==='Failed'),pageErrors},null,2));process.exitCode=results.some(x=>x.status==='Failed')||pageErrors.length?1:0;
})().catch(e=>{console.error(e);process.exit(1)});
