import os

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp"

# Let's search all .css and .tsx files for text-slate-800 or color definitions
for root, dirs, files in os.walk(base_dir):
    if "node_modules" in root or ".next" in root:
        continue
    for file in files:
        if file.endswith((".css", ".tsx")):
            file_path = os.path.join(root, file)
            with open(file_path, "r", encoding="utf-8") as f:
                content = f.read()
            if "text-slate-800" in content:
                print(f"Found text-slate-800 in: {os.path.relpath(file_path, base_dir)}")
            if "text-slate-200" in content:
                print(f"Found text-slate-200 in: {os.path.relpath(file_path, base_dir)}")
