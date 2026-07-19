import os
import time

def find_recent_files(directory, minutes=10):
    now = time.time()
    recent_files = []
    for root, dirs, files in os.walk(directory):
        if 'node_modules' in root or '.git' in root or '.next' in root:
            continue
        for file in files:
            filepath = os.path.join(root, file)
            try:
                mtime = os.path.getmtime(filepath)
                if now - mtime < minutes * 60:
                    recent_files.append((filepath, mtime))
            except Exception:
                pass
    return recent_files

print("Searching workspace...")
for f, t in find_recent_files(r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager"):
    # Convert path to pure ascii representation
    ascii_path = f.encode('ascii', errors='replace').decode('ascii')
    print("WS File: " + ascii_path)

print("\nSearching Gemini App Data...")
for f, t in find_recent_files(r"C:\Users\MSIIIIII\.gemini\antigravity", minutes=15):
    ascii_path = f.encode('ascii', errors='replace').decode('ascii')
    print("Gemini File: " + ascii_path)
