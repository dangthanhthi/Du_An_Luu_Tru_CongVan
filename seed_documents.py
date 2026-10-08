import urllib.request
import urllib.parse
import json
import jwt
import datetime
import uuid

# 1. TẠO FAKE JWT TOKEN ĐỂ BYPASS BẢO MẬT
secret = "DAS_SECRET_KEY_FOR_LOCAL_DEV_AT_LEAST_32_BYTES_LONG"
payload = {
    "sub": "00000000-0000-0000-0000-000000000001",
    "email": "admin@example.com",
    "name": "Admin Tester",
    "role": "admin",
    "das_capability": "DocumentRegisterIncoming DocumentRegisterDepartment",
    "exp": datetime.datetime.utcnow() + datetime.timedelta(hours=1)
}
token = jwt.encode(payload, secret, algorithm="HS256")
headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json"
}

# 2. DỮ LIỆU MẪU (3 Incoming, 2 Outgoing)
docs = [
    {
        "kind": "Incoming",
        "referenceNumber": "CV-2026/012-HL",
        "title": "[Demo] Báo cáo tiến độ dự án tuần 3",
        "direction": "incoming",
        "issuedDate": "2026-10-08T00:00:00Z"
    },
    {
        "kind": "Incoming",
        "referenceNumber": "CV-2026/015-HV",
        "title": "[Demo] Quyết định phê duyệt ngân sách",
        "direction": "incoming",
        "issuedDate": "2026-10-05T00:00:00Z"
    },
    {
        "kind": "Outgoing",
        "title": "[Demo] Công văn trả lời báo cáo tiến độ",
        "direction": "outgoing",
        "issuedDate": "2026-10-08T00:00:00Z"
    }
]

print("Seeding sample documents...")
success_count = 0

for doc in docs:
    req = urllib.request.Request(
        "http://localhost:5002/api/v2/documents",
        data=json.dumps(doc).encode('utf-8'),
        headers=headers,
        method="POST"
    )
    try:
        with urllib.request.urlopen(req) as response:
            if response.status in [200, 201]:
                success_count += 1
                print(f"Success: {doc['title']}")
    except Exception as e:
        print(f"Failed: {doc['title']} - {e}")

print(f"\nDone! Seeded {success_count}/{len(docs)} documents.")
