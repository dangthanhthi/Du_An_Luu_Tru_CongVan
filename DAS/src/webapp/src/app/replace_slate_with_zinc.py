import os

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\src"

def process_file(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    orig = content
    
    # Replace any text containing slate with zinc
    # e.g., slate-50 -> zinc-50, slate-900 -> zinc-900, etc.
    # We do a simple case-sensitive replacement of "slate" with "zinc"
    content = content.replace("slate", "zinc")
    
    # Special overrides if needed
    # For example, if there is slate-750 or something
    content = content.replace("zinc-750", "zinc-700")
    content = content.replace("zinc-650", "zinc-600")
    content = content.replace("zinc-350", "zinc-400")

    if content != orig:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Replaced slate in: {os.path.relpath(file_path, base_dir)}")

# Walk and replace in all TSX and CSS files
for root, dirs, files in os.walk(base_dir):
    for file in files:
        if file.endswith(".tsx") or file.endswith(".css"):
            process_file(os.path.join(root, file))

print("Slate-to-Zinc replacement completed successfully.")
