const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require(process.env.DAS_PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.DAS_UI_BASE_URL || 'http://127.0.0.1:3211';
const output = path.resolve(process.env.DAS_UI_QA_DIR || '.artifacts/qa/catalog-ui');
fs.mkdirSync(output, { recursive: true });
const id = '10000000-0000-4000-8000-000200000001';
const row = { id, group: 'methods', code: 'FAX', name: 'Fax server', sortOrder: 1, isActive: true, version: 1 };
const results = [], pageErrors = [];
(async () => {
 const browser = await chromium.launch({ headless: true, executablePath: process.env.DAS_BROWSER_EXE });
 const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
 const page = await context.newPage(); page.setDefaultTimeout(7000);
 page.on('pageerror', e => pageErrors.push(e.message));
 let mode = 'data', current = { ...row }, writes = [], requests = [], resolveLate, seenLate;
 const cors = { 'access-control-allow-origin': '*', 'access-control-allow-methods': 'GET,POST,PUT,OPTIONS', 'access-control-allow-headers': '*' };
 await page.route('**/api/v2/**', async route => {
  const request = route.request(); const url = new URL(request.url());
  if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers: cors });
  requests.push(url);
  if (request.method() === 'POST' || request.method() === 'PUT') {
   const data = request.postDataJSON(); writes.push({ method: request.method(), path: url.pathname, data });
   if (mode === 'conflict') return route.fulfill({ status: 409, headers: cors, json: { success: false, message: 'Version conflict' } });
   if (mode === 'uncertain') return route.abort('failed');
   current = request.method() === 'POST' ? { ...row, id: '10000000-0000-4000-8000-000200000099', ...data, version: 1 } : { ...current, ...data, version: current.version + 1 };
   return route.fulfill({ status: 200, headers: cors, json: { success: true, data: current } });
  }
  if (url.pathname === '/api/v2/distribution-targets') return route.fulfill({ status: 200, headers: cors, json: { success: true, data: {
   items: [{ id, name: 'Drilling legacy', initial: 'DRI', mappingState: 'Pending', version: 1 }],
   pageNumber: Number(url.searchParams.get('pageNumber')), pageSize: Number(url.searchParams.get('pageSize')), totalCount: 21
  } } });
  if (url.pathname.endsWith('/' + id)) return route.fulfill({ status: 200, headers: cors, json: { success: true, data: current } });
  if (url.pathname !== '/api/v2/catalogs') return route.continue();
  const group = url.searchParams.get('groups');
  if (mode === 'race' && group === 'methods') { seenLate(); await new Promise(r => resolveLate = r); }
  let items = group === 'methods' ? (current.isActive ? [current] : []) : group === 'companies' ? [{ ...row, group, code: 'HL', name: 'Hoàng Long server' }] : group === 'sensitivity' ? [{ ...row, group, code: 'Normal', name: 'Normal' }] : [];
  if (mode === 'malformed') items = [{ ...row, name: {} }];
  if (mode === 'empty') items = [];
  try { await route.fulfill({ status: mode === 'error' ? 503 : 200, headers: cors, json: mode === 'error' ? { success: false } : { success: true, data: { [group]: items } } }); } catch (e) { if (mode !== 'race') throw e; }
 });
 async function check(name, fn) {
  try { await fn(); results.push({ name, status: 'Passed' }); }
  catch (e) { results.push({ name, status: 'Failed', message: e.message }); await page.screenshot({ path: path.join(output, name + '-failed.png'), fullPage: true, timeout: 30000 }); }
 }
 // Set the fixture profile on every document before React mounts.
 await context.addInitScript(() => {
  const params = new URLSearchParams(location.search);
  localStorage.setItem('das_user', JSON.stringify({ capabilities: params.get('manage') === '1' ? ['CatalogManage'] : [] }));
 });
 const load = async (manage = false) => { await page.goto(base + '/vi/apps/settings/catalogs' + (manage ? '?manage=1' : '')); await page.getByRole('tab', { name: 'Công ty', exact: true }).waitFor(); };
 const methods = async () => { await page.getByRole('tab', { name: 'Phương thức', exact: true }).click(); await page.getByRole('cell', { name: current.name, exact: true }).waitFor(); };
 const dialog = () => page.getByRole('dialog');
 await check('fixed-and-readonly-groups-have-no-edit-actions', async () => {
  await load(); await page.getByRole('cell', { name: 'Hoàng Long server', exact: true }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Thêm danh mục', exact: true }).count(), 0);
  await methods(); assert.equal(await page.getByRole('button', { name: 'Sửa: FAX', exact: true }).count(), 0);
  await page.getByRole('tab', { name: 'Độ mật', exact: true }).click(); await page.getByRole('cell', { name: 'Normal', exact: true }).first().waitFor();
 });
 await check('create-uses-server-identity-and-refreshes-list', async () => {
  await load(true); await methods();
  await page.getByRole('button', { name: 'Thêm danh mục', exact: true }).click();
  await dialog().getByLabel('Mã', { exact: true }).fill(' custom '); await dialog().getByLabel('Tên', { exact: true }).fill('Danh mục mới');
  await dialog().getByRole('button', { name: 'Lưu', exact: true }).click();
  await page.getByRole('cell', { name: 'Danh mục mới', exact: true }).waitFor();
  assert.deepEqual(writes.at(-1).data, { group: 'methods', code: 'CUSTOM', name: 'Danh mục mới' });
  assert.equal(await dialog().count(), 0); current = { ...row };
 });
 await check('conflict-blocks-save-until-explicit-latest-version-reload', async () => {
  await load(true); await methods(); await page.getByRole('button', { name: 'Sửa: FAX', exact: true }).click();
  await dialog().getByLabel('Tên', { exact: true }).fill('First edit'); mode = 'conflict';
  await dialog().getByRole('button', { name: 'Lưu', exact: true }).click(); await dialog().getByText(/Danh mục đã thay đổi/).waitFor();
  assert.equal(await dialog().getByRole('button', { name: 'Lưu', exact: true }).isDisabled(), true);
  assert.equal(writes.at(-1).data.version, 1);
  current = { ...row, name: 'Concurrent change', version: 3 }; mode = 'data';
  await dialog().getByRole('button', { name: 'Tải dữ liệu mới', exact: true }).click();
  await page.waitForFunction(() => document.querySelector('[role="dialog"] input[value="Concurrent change"]'));
  await dialog().getByLabel('Tên', { exact: true }).fill('Reviewed edit'); await dialog().getByRole('button', { name: 'Lưu', exact: true }).click();
  await page.getByRole('cell', { name: 'Reviewed edit', exact: true }).waitFor(); assert.equal(writes.at(-1).data.version, 3);
 });
 await check('deactivation-preserves-entry-and-uses-versioned-update', async () => {
  await page.getByRole('button', { name: 'Sửa: FAX', exact: true }).click(); await dialog().getByRole('switch', { name: 'Đang sử dụng' }).uncheck();
  await dialog().getByRole('button', { name: 'Lưu', exact: true }).click(); await page.getByText('Không có dữ liệu trong danh mục này.', { exact: true }).waitFor();
  assert.equal(writes.at(-1).method, 'PUT'); assert.equal(writes.at(-1).data.isActive, false); current = { ...row };
 });
 await check('ambiguous-network-save-is-not-automatically-repeated', async () => {
  await load(true); await methods(); await page.getByRole('button', { name: 'Sửa: FAX', exact: true }).click(); mode = 'uncertain'; const before = writes.length;
  await dialog().getByRole('button', { name: 'Lưu', exact: true }).click(); await dialog().getByText(/Chưa xác định kết quả lưu/).waitFor();
  assert.equal(await dialog().getByRole('button', { name: 'Lưu', exact: true }).isDisabled(), true); assert.equal(writes.length, before + 1);
  mode = 'data'; await dialog().getByRole('button', { name: 'Tải dữ liệu mới', exact: true }).click();
  await dialog().getByRole('button', { name: 'Lưu', exact: true }).waitFor(); await dialog().getByRole('button', { name: 'Đóng', exact: true }).click();
 });
 await check('http-and-malformed-data-show-errors-with-retry', async () => {
  mode = 'error'; await load(true); await page.getByRole('alert').filter({ hasText: 'Không thể tải danh mục' }).waitFor();
  assert.equal(await page.getByRole('table').count(), 0); mode = 'data'; await page.getByRole('button', { name: 'Thử lại', exact: true }).click(); await page.getByRole('table').waitFor();
  mode = 'malformed'; await methods().catch(() => {}); await page.getByRole('alert').filter({ hasText: 'Không thể tải danh mục' }).waitFor(); assert.equal(await page.getByRole('table').count(), 0); mode = 'data';
 });
 await check('recipient-pending-mapping-server-search-and-paging', async () => {
  await load(true); await page.getByRole('tab', { name: 'Nơi nhận', exact: true }).click(); await page.getByRole('cell', { name: 'Drilling legacy', exact: true }).waitFor();
  await page.getByText('Chờ xác nhận', { exact: true }).waitFor(); assert.equal(await page.getByRole('button', { name: /^Sửa:/ }).count(), 0);
  const response = page.waitForResponse(r => new URL(r.url()).searchParams.get('pageNumber') === '2');
  await page.getByRole('button', { name: 'Trang sau', exact: true }).click(); await response;
  await page.getByLabel('Tìm nơi nhận', { exact: true }).fill('DRI');
  const searched = page.waitForResponse(r => { const u = new URL(r.url()); return u.pathname === '/api/v2/distribution-targets' && u.searchParams.get('search') === 'DRI' && u.searchParams.get('pageNumber') === '1'; });
  await page.getByRole('button', { name: 'Tìm kiếm', exact: true }).click(); await searched;
 });
 await check('group-change-cancels-late-response', async () => {
  await load(true); mode = 'race'; const seen = new Promise(r => seenLate = r);
  await page.getByRole('tab', { name: 'Phương thức', exact: true }).click(); await seen;
  const cancelled = page.waitForEvent('requestfailed', { predicate: r => new URL(r.url()).searchParams.get('groups') === 'methods' });
  await page.getByRole('tab', { name: 'Công ty', exact: true }).click(); await cancelled;
  await page.getByRole('cell', { name: 'Hoàng Long server', exact: true }).waitFor(); resolveLate(); resolveLate = undefined;
  assert.equal(await page.getByRole('cell', { name: 'Fax server', exact: true }).count(), 0); mode = 'data';
 });
 await check('mobile-catalogs-have-local-table-scroll-only', async () => {
  await page.setViewportSize({ width: 390, height: 844 }); await load(true); await methods();
  const size = await page.evaluate(() => ({ width: document.documentElement.clientWidth, scroll: document.documentElement.scrollWidth }));
  assert.ok(size.scroll <= size.width + 1, JSON.stringify(size)); await page.screenshot({ path: path.join(output, 'catalog-mobile.png'), fullPage: true, timeout: 30000 });
  await page.setViewportSize({ width: 1440, height: 1000 }); await page.screenshot({ path: path.join(output, 'catalog-desktop.png'), fullPage: true, timeout: 30000 });
 });
 fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ fixture: 'Stub; not live EAP/catalog acceptance', utc: new Date().toISOString(), base, results, pageErrors }, null, 2));
 await browser.close(); console.log(JSON.stringify({ passed: results.filter(r => r.status === 'Passed').length, failed: results.filter(r => r.status === 'Failed'), pageErrors }, null, 2));
 process.exitCode = results.some(r => r.status === 'Failed') || pageErrors.length ? 1 : 0;
})().catch(e => { console.error(e); process.exit(1); });
