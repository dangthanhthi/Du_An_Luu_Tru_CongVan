# Task Management – EAP integration endpoint contract

Tài liệu này dành để gửi cho dev của Task Management. Đây là **contract tích
hợp**: domain, endpoint, header, request/response và RabbitMQ topology. File
không chứa password, client secret, private key, token hoặc lệnh chạy checker.

## 1. Domain và application đã đăng ký

| Mục đích | Giá trị |
| --- | --- |
| Identity Admin | `https://identity.lucasvu.io.vn` |
| Browser authorization | `https://auth.lucasvu.io.vn` |
| OIDC issuer và server API | `https://api.lucasvu.io.vn/` |
| Internal API base qua gateway | `https://api.lucasvu.io.vn` |
| Task Management application | `Task Management - Local Dev` |
| OIDC client ID | `app_SzutInnUQ0TkPSSsrz7ZZULd` |
| Local callback đã đăng ký | `http://localhost:3000/api/auth/callback/sso-oauth` |
| Local post-logout redirect | `http://localhost:3000/logout` |
| Internal API resource/audience | `urn:hlhv:api:identity` |
| RabbitMQ broker | `eap-staging-broker-01.netbird.selfhosted` qua NetBird |
| RabbitMQ vhost | `hlhv.production` |
| RabbitMQ protocol | AMQP plain trên port `5672` qua NetBird |

`OIDC_CLIENT_SECRET`, `S2S_AUTH_PRIVATE_KEY_PKCS8_PEM_BASE64` và
`RABBITMQ_PASSWORD` chỉ được giữ ở server/BFF/secret store. Không đưa các giá
trị này vào frontend hoặc commit vào repository.

## 1.1 Quy ước datatype trên wire

Các kiểu dưới đây là kiểu JSON gửi/nhận qua HTTP; dấu `?` nghĩa là field có
thể nhận `null` hoặc được bỏ qua khi API cho phép:

| Ký hiệu | Kiểu JSON và quy ước |
| --- | --- |
| `string` | Chuỗi UTF-8 |
| `uuid` | `string` theo RFC 4122, ví dụ `00000000-0000-0000-0000-000000000000` |
| `date-time-utc` | `string` ISO-8601/RFC 3339 có timezone UTC, ví dụ `2026-10-06T00:00:00Z` |
| `int32` | JSON number nguyên 32-bit |
| `int64` | JSON number nguyên 64-bit |
| `boolean` | JSON `true` hoặc `false` |
| `enum<string>` | Chuỗi thuộc danh sách giá trị được ghi ở endpoint |
| `array<T>` | JSON array mà mỗi phần tử có kiểu `T` |
| `object` | JSON object |
| `T?` | Giá trị `T` nullable; gửi `null` hoặc bỏ field nếu endpoint ghi là optional |

Các enum của organization-context được serialize dạng chuỗi: `AllActive`,
`PrimaryOnly`, `All`, `ExactlyOne`, `PrimaryOu`, `PrimaryDepartment`,
`ParentDepartment`, `ManagementRoot` và `FixedOuCode`.

`int64` hiện được serialize trên wire là JSON number, không phải chuỗi. Client
JavaScript/TypeScript cần giữ trong vùng an toàn của `number` hoặc dùng cách
đọc số nguyên 64-bit phù hợp; không tự đổi sang string khi gửi request.

## 2. Browser SSO (OIDC Authorization Code + PKCE)

Frontend chỉ khởi tạo redirect. Code exchange và userinfo phải chạy ở server
BFF. Không tự xử lý password trong frontend.

### 2.1 Discovery

```http
GET https://api.lucasvu.io.vn/.well-known/openid-configuration
Accept: application/json
```

Discovery trả về issuer, authorization endpoint, token endpoint, userinfo
endpoint và JWKS endpoint. Các URL bên dưới là giá trị của deployment hiện tại.

### 2.2 Authorization endpoint

```http
GET https://auth.lucasvu.io.vn/connect/authorize
```

Query bắt buộc/khuyến nghị:

| Key | Datatype | Giá trị/quy tắc |
| --- | --- | --- |
| `client_id` | `string` | `app_SzutInnUQ0TkPSSsrz7ZZULd` |
| `redirect_uri` | `string` (URI) | URI đã đăng ký chính xác, local là `http://localhost:3000/api/auth/callback/sso-oauth` |
| `response_type` | `enum<string>` | `code` |
| `scope` | `string` | Space-delimited scopes: `openid profile email offline_access` |
| `state` | `string` | Chuỗi random do app tạo, dùng chống CSRF |
| `code_challenge` | `string` (base64url) | Base64url(SHA-256(`code_verifier`)) |
| `code_challenge_method` | `enum<string>` | `S256` |
| `nonce` | `string` | Chuỗi random để kiểm tra ID token |
| `prompt` | `enum<string>`? | Bỏ qua hoặc dùng `select_account`; chỉ dùng `login` khi cần đăng nhập lại |

Sau khi đăng nhập và cấp quyền, Identity redirect về callback với:

```text
?code=<one-time-code>&state=<original-state>&iss=https%3A%2F%2Fapi.lucasvu.io.vn%2F
```

App phải kiểm tra `state`, kiểm tra callback đúng origin/path đã đăng ký và không
ghi `code` vào log.

### 2.3 Token endpoint

Chỉ gọi từ BFF/backend:

```http
POST https://api.lucasvu.io.vn/connect/token
Content-Type: application/x-www-form-urlencoded
```

Request cho authorization code:

```text
grant_type=authorization_code
client_id=app_SzutInnUQ0TkPSSsrz7ZZULd
client_secret=<server-only-secret>
code=<callback-code>
redirect_uri=http://localhost:3000/api/auth/callback/sso-oauth
code_verifier=<original-verifier>
```

| Field | Datatype | Bắt buộc |
| --- | --- | --- |
| `grant_type` | `enum<string>` | Có, giá trị `authorization_code` |
| `client_id` | `string` | Có |
| `client_secret` | `string` | Có ở server/BFF, không gửi cho browser |
| `code` | `string` | Có, one-time authorization code |
| `redirect_uri` | `string` (URI) | Có, phải trùng URI đã đăng ký |
| `code_verifier` | `string` (base64url) | Có khi dùng PKCE |

Response thành công là OAuth token response gồm `access_token`, `token_type`,
`expires_in`, ID token nếu client yêu cầu OIDC, và có thể có `refresh_token`.
Không trả access/refresh token ra browser log hoặc URL.

### 2.4 Userinfo và JWKS

```http
GET https://api.lucasvu.io.vn/connect/userinfo
Authorization: Bearer <access_token>
Accept: application/json
```

Userinfo dùng để lấy thông tin user OIDC, tối thiểu thường gồm `sub`, `name`,
`email` và `preferred_username` khi claim được cấp cho client.

```http
GET https://api.lucasvu.io.vn/.well-known/jwks.json
Accept: application/json
```

BFF dùng JWKS để kiểm tra chữ ký ID token khi thư viện OIDC yêu cầu. Không tự
hard-code signing key.

### 2.5 Refresh và logout

Refresh token cũng dùng cùng token endpoint, chỉ ở server:

```text
grant_type=refresh_token
client_id=app_SzutInnUQ0TkPSSsrz7ZZULd
client_secret=<server-only-secret>
refresh_token=<server-only-refresh-token>
```

| Field | Datatype | Bắt buộc |
| --- | --- | --- |
| `grant_type` | `enum<string>` | Có, giá trị `refresh_token` |
| `client_id` | `string` | Có |
| `client_secret` | `string` | Có ở server/BFF |
| `refresh_token` | `string` | Có, chỉ lưu ở server |

Logout là browser redirect:

```http
GET https://auth.lucasvu.io.vn/connect/logout?client_id=app_SzutInnUQ0TkPSSsrz7ZZULd&post_logout_redirect_uri=http%3A%2F%2Flocalhost%3A3000%2Flogout
```

`post_logout_redirect_uri` phải trùng URI đã đăng ký.

### 2.6 Endpoint password login nội bộ

```http
POST https://api.lucasvu.io.vn/identity/auth/sso-login
Content-Type: application/json
```

Endpoint này chỉ dành cho trusted BFF/test harness để tạo `EapSession` HttpOnly;
không gọi trực tiếp từ browser app thay cho OIDC:

```json
{
  "identity": "task-manager-dev",
  "password": "<server-side-or-interactive-secret>",
  "rememberMe": false,
  "returnUrl": "https://auth.lucasvu.io.vn/connect/authorize?..."
}
```

| Field | Datatype | Bắt buộc |
| --- | --- | --- |
| `identity` | `string` | Có, username |
| `password` | `string` | Có, chỉ truyền từ trusted BFF/test harness |
| `rememberMe` | `boolean` | Có |
| `returnUrl` | `string` (URI) | Có, URL được allowlist |

Identity hiện resolve trường `identity` theo **username**, không theo email.

## 3. Machine client gọi Internal API

### 3.1 Đổi machine credential lấy service token

```http
POST https://api.lucasvu.io.vn/connect/token
Content-Type: application/x-www-form-urlencoded
```

Client dùng `private_key_jwt` ES256:

```text
grant_type=client_credentials
client_id=<S2S_AUTH_CLIENT_ID>
client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer
client_assertion=<ES256-JWT-signed-with-S2S-private-key>
scope=identity.organization-context.picker.read identity.organization-context.routing.resolve
resource=urn:hlhv:api:identity
```

| Field | Datatype | Bắt buộc |
| --- | --- | --- |
| `grant_type` | `enum<string>` | Có, giá trị `client_credentials` |
| `client_id` | `string` | Có, machine client ID |
| `client_assertion_type` | `enum<string>` | Có, giá trị `urn:ietf:params:oauth:client-assertion-type:jwt-bearer` |
| `client_assertion` | `string` (JWT) | Có, JWT ES256 ký bằng private key ở server |
| `scope` | `string` | Có, space-delimited scopes |
| `resource` | `string` (URI) | Có, `urn:hlhv:api:identity` |

JWT assertion có các claim chính:

```json
{
  "iss": "<S2S_AUTH_CLIENT_ID>",
  "sub": "<S2S_AUTH_CLIENT_ID>",
  "aud": "https://api.lucasvu.io.vn/",
  "iat": 0,
  "exp": 0,
  "jti": "<unique-id>"
}
```

Header dùng `alg=ES256`, `typ=client-authentication+jwt`, `kid=<S2S_AUTH_KEY_ID>`.
Service token trả về phải được giữ ở backend và gửi dạng:

```http
Authorization: Bearer <service-access-token>
Content-Type: application/json
Accept: application/json
```

| JWT claim/header | Datatype | Quy tắc |
| --- | --- | --- |
| `iss`, `sub`, `jti`, `kid` | `string` | `iss`/`sub` là client ID; `jti` phải unique; `kid` là key ID đã đăng ký |
| `aud` | `string` (URI) | `https://api.lucasvu.io.vn/` |
| `iat`, `exp` | `int64` | Unix time seconds; `exp` phải lớn hơn `iat` |
| `alg` | `enum<string>` | `ES256` |
| `typ` | `enum<string>` | `client-authentication+jwt` |

### 3.2 Response envelope chung

Các endpoint organization-context trả response theo dạng:

```json
{
  "success": true,
  "message": null,
  "result": {
    "contractVersion": "1",
    "organizationRevision": 123,
    "resolvedAtUtc": "2026-10-06T00:00:00Z",
    "requestId": "<opaque-request-id>",
    "data": {}
  },
  "errors": null
}
```

| Field | Datatype | Ghi chú |
| --- | --- | --- |
| `success` | `boolean` | `true` khi request thành công |
| `message` | `string`? | Thông báo tổng quát, thường `null` khi thành công |
| `result` | `object`? | Có khi thành công |
| `result.contractVersion` | `string` | Hiện tại là `1` |
| `result.organizationRevision` | `int64` | Revision snapshot |
| `result.resolvedAtUtc` | `date-time-utc` | Thời điểm snapshot |
| `result.requestId` | `string` | Correlation/request ID opaque |
| `result.data` | `object` | Payload theo endpoint |
| `errors` | `array<object>`? | Có khi lỗi |

Không dùng `requestId` làm business identifier. Nếu revision thay đổi trong
quá trình xử lý, API có thể trả `409 REVISION_CHANGED`; request nên được tạo lại
với thời điểm/selector mới.

### 3.3 Routing – resolve một user

```http
POST https://api.lucasvu.io.vn/internal/auth/v1/organization-context/routing/resolve
```

Scope: `identity.organization-context.routing.resolve`

```json
{
  "requestedFor": "00000000-0000-0000-0000-000000000000",
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "selectors": [
    {
      "key": "primary-department",
      "scope": "PrimaryDepartment",
      "role": null,
      "ouCode": null,
      "cardinality": "All",
      "isRequired": true
    }
  ],
  "assignmentSelection": "PrimaryOnly"
}
```

| Field | Datatype | Bắt buộc | Giá trị/quy tắc |
| --- | --- | --- | --- |
| `requestedFor` | `uuid` | Có | User cần resolve |
| `resolveAtUtc` | `date-time-utc` | Có | Thời điểm snapshot cần resolve |
| `selectors` | `array<object>` | Có | Tối đa theo policy; mỗi phần tử là selector |
| `selectors[].key` | `string` | Có | Key do app đặt để map kết quả |
| `selectors[].scope` | `enum<string>` | Có | `PrimaryOu`, `PrimaryDepartment`, `ParentDepartment`, `ManagementRoot`, `FixedOuCode` |
| `selectors[].role` | `enum<string>`? | Không | Role cần lọc, nếu scope yêu cầu |
| `selectors[].ouCode` | `string`? | Không | Bắt buộc theo nghiệp vụ khi `scope=FixedOuCode` |
| `selectors[].cardinality` | `enum<string>` | Có | `All` hoặc `ExactlyOne` |
| `selectors[].isRequired` | `boolean` | Có | `true` nếu selector không resolve được thì request thất bại |
| `assignmentSelection` | `enum<string>` | Có | `AllActive` hoặc `PrimaryOnly` |

`scope`: `PrimaryOu`, `PrimaryDepartment`, `ParentDepartment`, `ManagementRoot`
hoặc `FixedOuCode`. `cardinality`: `All` hoặc `ExactlyOne`.

`data` trả về `requestedFor`, `resolvedAtUtc`, `primaryOrganizationUnit`,
`requestedForRole`, `primaryBranch`, và kết quả từng selector trong
`selectors`; mỗi selector có `outcome`, `organizationUnit` và `candidates`.

Các field chính trong `data` có kiểu: `requestedFor: uuid`,
`resolvedAtUtc: date-time-utc`, `requestedForRole: enum<string>`,
`primaryOrganizationUnit: object`, `primaryBranch: array<object>` và
`selectors: array<object>`. Mỗi `organizationUnit` có `departmentId: int64`,
`code/name: string`, `isGroup: boolean`, `parentDepartmentId: int64?` và
`sourceVersion: int64`.

### 3.4 Routing – resolve một nhóm user

```http
POST https://api.lucasvu.io.vn/internal/auth/v1/organization-context/routing/resolve-group
```

Scope: `identity.organization-context.routing.resolve`

```json
{
  "participantIds": ["00000000-0000-0000-0000-000000000000"],
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "rootOuCode": "MAN",
  "additionalOuCodes": [],
  "assignmentSelection": "AllActive"
}
```

| Field | Datatype | Bắt buộc | Giá trị/quy tắc |
| --- | --- | --- | --- |
| `participantIds` | `array<uuid>` | Có | Danh sách user cần resolve |
| `resolveAtUtc` | `date-time-utc` | Có | Thời điểm snapshot cần resolve |
| `rootOuCode` | `string` | Không | Mặc định `MAN` |
| `additionalOuCodes` | `array<string>`? | Không | OU code bổ sung; có thể gửi `[]` hoặc bỏ field |
| `assignmentSelection` | `enum<string>` | Không | Mặc định `AllActive`; có thể dùng `PrimaryOnly` |

`data` trả `status` (`Resolved` hoặc `RouteFailed`), `rootOuCode`, danh sách
`participants` và `staffing`. Mỗi participant có `userId`, `errorCode`,
`errorMessage`, `assignments`.

`status`, `rootOuCode`, `errorCode`, `errorMessage` là `string` (các error field
có thể `null`); `participants` và `staffing` là `array<object>`; các `userId`
và `assignmentId` là `uuid`; các department/assignment version là `int64`;
`isPrimary` là `boolean`.

### 3.5 Picker – danh sách organization units

```http
POST https://api.lucasvu.io.vn/internal/auth/v1/organization-context/picker/nodes
```

Scope: `identity.organization-context.picker.read`

```json
{
  "parentDepartmentId": null,
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "page": 1,
  "pageSize": 100,
  "search": null,
  "assignmentSelection": "AllActive",
  "continuationToken": null
}
```

| Field | Datatype | Bắt buộc | Giá trị/quy tắc |
| --- | --- | --- | --- |
| `parentDepartmentId` | `int64`? | Có | `null` để lấy root; số nguyên ID của department cha |
| `resolveAtUtc` | `date-time-utc` | Có | Thời điểm snapshot cần resolve |
| `page` | `int32` | Không | Mặc định `1`, bắt đầu từ `1` |
| `pageSize` | `int32` | Không | Mặc định `100`, tối đa theo policy |
| `search` | `string`? | Không | Từ khóa tìm kiếm, tối đa theo policy |
| `assignmentSelection` | `enum<string>` | Không | Mặc định `AllActive`; có thể dùng `PrimaryOnly` |
| `continuationToken` | `string`? | Không | Token opaque từ response trước; không tự parse/sửa |

`data` gồm `page`, `pageSize`, `totalCount`, `items`, `continuationToken`.
Mỗi item có `departmentId`, `code`, `name`, `isGroup`, `parentDepartmentId`,
`hasChildren`, `directActiveUserCount`, `sourceVersion`.

`page`/`pageSize` là `int32`, `totalCount`/`departmentId`/
`parentDepartmentId`/`directActiveUserCount`/`sourceVersion` là `int64`,
`code`/`name` là `string`, `isGroup`/`hasChildren` là `boolean`, còn
`continuationToken` là `string?`.

### 3.6 Picker – user theo organization unit

```http
POST https://api.lucasvu.io.vn/internal/auth/v1/organization-context/picker/users
```

Scope: `identity.organization-context.picker.read`

```json
{
  "departmentId": 1001,
  "includeDescendants": true,
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "page": 1,
  "pageSize": 50,
  "search": null,
  "assignmentSelection": "PrimaryOnly",
  "continuationToken": null
}
```

| Field | Datatype | Bắt buộc | Giá trị/quy tắc |
| --- | --- | --- | --- |
| `departmentId` | `int64` | Có | ID organization unit |
| `includeDescendants` | `boolean` | Có | Có lấy cả user ở subtree hay không |
| `resolveAtUtc` | `date-time-utc` | Có | Thời điểm snapshot cần resolve |
| `page` | `int32` | Không | Mặc định `1`, bắt đầu từ `1` |
| `pageSize` | `int32` | Không | Mặc định `50`, tối đa theo policy |
| `search` | `string`? | Không | Từ khóa tìm kiếm |
| `assignmentSelection` | `enum<string>` | Không | Mặc định `AllActive`; có thể dùng `PrimaryOnly` |
| `continuationToken` | `string`? | Không | Token opaque từ response trước |

`data.items` gồm `userId`, `displayName`, `userName`, `email` và `memberships`.
Mỗi membership có `departmentId`, `code`, `name`, `isGroup`, `role`, `isPrimary`,
`assignmentId`, `assignmentVersion`.

`data.page`/`data.pageSize` là `int32`, `data.totalCount` là `int64`,
`data.continuationToken` là `string?`; `userId`/`assignmentId` là `uuid`,
`displayName`/`userName`/`email`/`code`/`name`/`role` là `string`,
`departmentId`/`assignmentVersion` là `int64`, còn `isGroup`/`isPrimary` là
`boolean`.

### 3.7 Picker – preview subtree

```http
POST https://api.lucasvu.io.vn/internal/auth/v1/organization-context/picker/subtree-preview
```

Scope: `identity.organization-context.picker.read`

```json
{
  "departmentId": 1001,
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "search": null,
  "assignmentSelection": "PrimaryOnly"
}
```

| Field | Datatype | Bắt buộc | Giá trị/quy tắc |
| --- | --- | --- | --- |
| `departmentId` | `int64` | Có | Root organization unit của subtree |
| `resolveAtUtc` | `date-time-utc` | Có | Thời điểm snapshot cần resolve |
| `search` | `string`? | Không | Từ khóa lọc user |
| `assignmentSelection` | `enum<string>` | Không | Mặc định `AllActive`; có thể dùng `PrimaryOnly` |

`data` trả `departmentId` và `uniqueActiveUserCount`.

`departmentId` và `uniqueActiveUserCount` đều là `int64`.

### 3.8 Directory và display read-only endpoints

Các endpoint dưới đây dùng audience `urn:hlhv:api:identity`. Ba endpoint
directory dùng scope `identity.organization-context.directory.read`; endpoint
display dùng scope riêng `identity.organization-context.display.read`.

| Method | Endpoint | Request fields (datatype) |
| --- | --- | --- |
| POST | `/internal/auth/v1/organization-context/directory/nodes` | `parentOrganizationUnitId: int64?`, `resolveAtUtc: date-time-utc`, `page: int32?`, `pageSize: int32?`, `search: string?`, `continuationToken: string?` · scope `identity.organization-context.directory.read` |
| POST | `/internal/auth/v1/organization-context/directory/detail` | `organizationUnitId: int64`, `resolveAtUtc: date-time-utc` · scope `identity.organization-context.directory.read` |
| POST | `/internal/auth/v1/organization-context/directory/members` | `organizationUnitId: int64`, `resolveAtUtc: date-time-utc`, `page: int32?`, `pageSize: int32?`, `search: string?`, `continuationToken: string?` · scope `identity.organization-context.directory.read` |
| POST | `/internal/auth/v1/organization-context/display/primary-departments/batch` | `userIds: array<uuid>` · scope `identity.organization-context.display.read` |

### 3.9 Directory request examples

`directory/nodes`:

```json
{
  "parentOrganizationUnitId": null,
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "page": 1,
  "pageSize": 100,
  "search": null,
  "continuationToken": null
}
```

`directory/detail`:

```json
{
  "organizationUnitId": 1001,
  "resolveAtUtc": "2026-10-06T00:00:00Z"
}
```

`directory/members`:

```json
{
  "organizationUnitId": 1001,
  "resolveAtUtc": "2026-10-06T00:00:00Z",
  "page": 1,
  "pageSize": 50,
  "search": null,
  "continuationToken": null
}
```

`display/primary-departments/batch`:

```json
{
  "userIds": ["00000000-0000-0000-0000-000000000000"]
}
```

`directory/nodes` và `directory/members` dùng các field tương ứng trong bảng
trên; `page` bắt đầu từ `1`, `pageSize` là `int32`, còn
`continuationToken` là chuỗi opaque do response trước trả về. Endpoint display
nhận tối đa số user theo policy của deployment và không nhận `resolveAtUtc`.

Datatype response directory/display:

| Response field | Datatype |
| --- | --- |
| `data.page`, `data.pageSize` | `int32` |
| `data.totalCount`, `data.items[].organizationUnitId`, `data.items[].parentOrganizationUnitId`, `data.items[].directMemberCount`, `data.items[].sourceVersion` | `int64`/`int64?` theo field |
| `data.continuationToken` | `string?` |
| `data.items[].code`, `data.items[].name`, `data.items[].leaders[].displayName`, `data.items[].leaders[].role` | `string` |
| `data.items[].isGroup`, `data.items[].hasChildren`, `data.items[].leaders[].isPrimary` | `boolean` |
| `data.items[].leaders[].userId`, `data.items[].members[].userId`, `data.items[].members[].assignmentId` | `uuid` |
| `data.items[].members[].displayName`, `data.items[].members[].userName`, `data.items[].members[].role` | `string` |
| `data.items[].members[].isPrimary` | `boolean` |
| `data.items[].primaryDepartment.departmentId`, `data.items[].primaryDepartment.sourceVersion` | `int64` |
| `data.items[].primaryDepartment.code`, `data.items[].primaryDepartment.name` | `string` |
| `data.items[].status`, `data.items[].primaryDepartment`/`assignment` | `enum<string>` / `object?` |
| `data.items[].assignment.assignmentId` | `uuid` |
| `data.items[].assignment.version` | `int64` |
| `data.items[].assignment.effectiveFromUtc`, `data.items[].assignment.effectiveToUtc`, `data.items[].nextChangeAtUtc` | `date-time-utc?` |

Các endpoint directory/display trả cùng envelope
`contractVersion`/`organizationRevision` ở trên. Không gửi
`identity/internal/v1/...` từ app; đó là route nội bộ sau gateway transform.

## 4. RabbitMQ user-sync

### 4.1 Topology

App tự suy ra tên queue từ client ID:

```text
queue       = user-sync.<client_id>
exchange    = eap.user-sync
routing key = user-sync.<client_id>
vhost       = hlhv.production
broker      = eap-staging-broker-01.netbird.selfhosted:5672
```

Với Task Management hiện tại:

```text
user-sync.app_SzutInnUQ0TkPSSsrz7ZZULd
```

Chỉ dùng hostname NetBird khi máy đã kết nối VPN. Không dùng IP public hoặc mở
RabbitMQ ra Internet. Không khai báo `RABBITMQ_USER_SYNC_QUEUE`; queue phải được
derive từ `RABBITMQ_USER_SYNC_CLIENT_ID`.

### 4.2 User-sync event nhận từ Identity

Message là JSON persistent. Các metadata quan trọng nằm ở envelope/properties:

```json
{
  "eventId": "<event-uuid>",
  "eventType": "identity.assigned_users.v1",
  "eventVersion": 1,
  "occurredAtUtc": "2026-10-06T00:00:00Z",
  "producer": "identity",
  "applicationId": 5,
  "clientId": "app_SzutInnUQ0TkPSSsrz7ZZULd",
  "routingKey": "user-sync.app_SzutInnUQ0TkPSSsrz7ZZULd",
  "idempotencyKey": "<stable-idempotency-key>",
  "correlationId": "<sync-correlation-id>",
  "data": {
    "user": {
      "userId": "00000000-0000-0000-0000-000000000000",
      "userName": "task-manager-dev",
      "email": "task-manager-dev@lucasvu.io.vn",
      "firstName": null,
      "lastName": null,
      "description": null,
      "phoneNumber": null,
      "avatar": null,
      "status": "Active",
      "roles": [],
      "empId": null,
      "companyId": null,
      "syncedAt": "2026-10-06T00:00:00Z",
      "aggregateVersion": 1,
      "eventType": null
    }
  }
}
```

`eventId`, `idempotencyKey`, `correlationId`, `userId`, `applicationId` và
`clientId` phải được lưu/kiểm tra để xử lý idempotent. `eventType=UserDeleted`
cho biết downstream phải xóa hoặc vô hiệu hóa projection user theo policy của
ứng dụng.

| Event field | Datatype |
| --- | --- |
| `eventId` | `uuid` |
| `idempotencyKey`, `correlationId`, `clientId`, `routingKey`, `producer`, `eventType` | `string` |
| `eventVersion` | `int32` |
| `applicationId` | `int64` |
| `data.user.aggregateVersion`, `data.user.companyId` | `int64`? |
| `occurredAtUtc` | `date-time-utc` |
| `data.user.syncedAt` | `date-time-utc`? |
| `data.user.userId` | `uuid` |
| `data.user.userName`, `email`, `firstName`, `lastName`, `description`, `phoneNumber`, `avatar`, `empId`, `companyId`, `eventType` | `string`? |
| `data.user.status` | `enum<string>` |
| `data.user.roles` | `array<string>` |

### 4.3 ACK/NACK về Identity

Publish vào exchange `eap.sync-results` với routing key
`sync-results.identity`:

```json
{
  "eventId": "<original-event-uuid>",
  "idempotencyKey": "<original-idempotency-key>",
  "userId": "00000000-0000-0000-0000-000000000000",
  "applicationId": 5,
  "clientId": "app_SzutInnUQ0TkPSSsrz7ZZULd",
  "status": "Succeeded",
  "processedAt": "2026-10-06T00:00:00Z"
}
```

`status` dùng `Succeeded` hoặc `Failed`. Khi failed, thêm `errorCode` và
`errorMessage` đã sanitize; không gửi password, token hoặc payload nhạy cảm.
Chỉ ACK delivery gốc sau khi publish reply đã được broker confirm. Duplicate
delivery cùng logical event phải được xử lý idempotent.

| ACK field | Datatype | Bắt buộc |
| --- | --- | --- |
| `eventId` | `uuid`? | Nên có, lấy từ event gốc |
| `idempotencyKey` | `string`? | Nên có, lấy từ event gốc |
| `userId` | `uuid` | Có trong user-sync event |
| `applicationId` | `int64`? | Có nếu reply theo application |
| `clientId` | `string`? | Có nếu reply theo client |
| `status` | `enum<string>` | Có, `Succeeded`, `Synced` hoặc `Failed` |
| `processedAt` | `date-time-utc`? | Nên có khi đã xử lý xong |
| `errorCode` | `string`? | Có khi `status=Failed` |
| `errorMessage` | `string`? | Có khi `status=Failed`; phải sanitize |

## 5. HTTP lỗi cần xử lý

| Status | Ý nghĩa xử lý |
| --- | --- |
| `400` | Request/selector không hợp lệ; sửa body trước khi retry |
| `401` | Token thiếu, hết hạn hoặc sai issuer; lấy token mới |
| `403` | Sai audience hoặc thiếu scope/application access |
| `409` | `REVISION_CHANGED`; tạo lại request với revision/thời điểm mới |
| `413` | Vượt response/hierarchy limit; thu hẹp selector/page |
| `422` | User/assignment/organization không hợp lệ; không retry mù |
| `429` | Rate limit; backoff theo policy của app |
| `5xx` | Upstream tạm thời lỗi; retry có giới hạn và idempotency key |

## 6. Không nằm trong contract này

Notify service và storage service chưa được thêm vào bộ credential/endpoint của
Task Management local. Khi có application access và contract riêng, bổ sung
thành tài liệu/service riêng; không tự suy đoán endpoint storage từ RabbitMQ hay
OIDC config.
