import sqlite3
import os

db_path = "backend/services/partner-service/local-partner.db"
sql_file = "scripts/migrated_partners_sqlite.sql"

if not os.path.exists(sql_file):
    print(f"SQL file not found: {sql_file}")
    exit(1)

print(f"Connecting to {db_path}...")
conn = sqlite3.connect(db_path)
cursor = conn.cursor()

with open(sql_file, 'r', encoding='utf-8') as f:
    sql_script = f.read()

try:
    cursor.executescript(sql_script)
    conn.commit()
    print("Successfully seeded partner database!")
except Exception as e:
    print(f"Error seeding database: {e}")
finally:
    conn.close()
