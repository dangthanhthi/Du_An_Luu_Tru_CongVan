import os

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\src"

def process_file(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    orig = content
    
    # Change "tranzinc" back to "translate"
    content = content.replace("tranzinc", "translate")
    content = content.replace("Tranzinc", "Translate")

    if content != orig:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Fixed tranzinc in: {os.path.relpath(file_path, base_dir)}")

# Walk and fix in all TSX and CSS files
for root, dirs, files in os.walk(base_dir):
    for file in files:
        if file.endswith(".tsx") or file.endswith(".css"):
            process_file(os.path.join(root, file))

print("Tranzinc-to-Translate fix completed successfully.")
