import os
import re

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\src"

replacements = [
    # Backgrounds
    (r"bg-white dark:bg-zinc-900", "bg-card"),
    (r"bg-white dark:bg-zinc-950", "bg-card"),
    (r"bg-slate-50 dark:bg-zinc-900", "bg-muted"),
    (r"bg-slate-50 dark:bg-slate-950", "bg-background"),
    (r"bg-white dark:bg-slate-950", "bg-background"),
    (r"bg-\[#FFFFFF\] dark:bg-\[#090D16\]", "bg-background"),
    (r"bg-white/60 dark:bg-slate-900/60", "bg-card/60"),
    (r"bg-[#FFFFFF]/80 dark:bg-slate-900/60", "bg-card"),
    (r"bg-\[#FFFFFF\]/80 dark:bg-slate-900/60", "bg-card"),
    (r"bg-\[#F1F5F9\]/60 dark:bg-slate-900/60", "bg-muted"),
    
    # Borders
    (r"border-slate-200 dark:border-zinc-800", "border-border"),
    (r"border-slate-100 dark:border-zinc-800", "border-border"),
    (r"border-slate-100 dark:border-slate-800", "border-border"),
    (r"border-slate-100 dark:border-slate-850", "border-border"),
    (r"border-slate-200/80 dark:border-slate-800", "border-border"),
    (r"border-slate-200/50 dark:border-slate-800", "border-border"),
    (r"border-slate-900 dark:border-slate-200", "border-border"),
    (r"border-slate-900 dark:border-slate-250", "border-border"),
    
    # Divides
    (r"divide-slate-100 dark:divide-zinc-800", "divide-border"),
    (r"divide-slate-200 dark:divide-zinc-800", "divide-border"),
    (r"divide-slate-100 dark:divide-slate-800", "divide-border"),
    (r"divide-border/60", "divide-border"),

    # Text Colors
    (r"text-slate-800 dark:text-slate-100", "text-foreground"),
    (r"text-slate-900 dark:text-slate-100", "text-foreground"),
    (r"text-slate-700 dark:text-slate-300", "text-foreground"),
    (r"text-slate-900 dark:text-slate-200", "text-foreground"),
    (r"text-slate-500 dark:text-slate-400", "text-muted-foreground"),
    (r"text-slate-650 mt-1 font-semibold", "text-muted-foreground mt-1"),
    (r"text-slate-450", "text-muted-foreground"),
    (r"text-slate-400", "text-muted-foreground"),
    (r"text-zinc-400", "text-muted-foreground"),
    (r"text-zinc-500", "text-muted-foreground"),
    (r"text-zinc-100", "text-foreground"),
    (r"text-slate-700", "text-foreground"),
    (r"text-slate-600 dark:text-slate-300", "text-muted-foreground"),
    (r"text-\[#0F172A\] dark:text-\[#F8FAFC\]", "text-foreground"),
    (r"text-\[#0F172A\] dark:text-\[#FFFFFF\]", "text-foreground"),
    
    # Interaction / Hover states
    (r"hover:bg-slate-50 dark:hover:bg-slate-800", "hover:bg-muted"),
    (r"hover:bg-slate-900 hover:text-white dark:hover:bg-slate-100 dark:hover:text-slate-950", "hover:bg-foreground hover:text-background"),
]

def update_file(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    orig = content
    for pattern, repl in replacements:
        content = re.sub(pattern, repl, content)
        
    if content != orig:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Updated: {os.path.relpath(file_path, base_dir)}")

# Walk all subdirectories
for root, dirs, files in os.walk(base_dir):
    for file in files:
        if file.endswith(".tsx"):
            update_file(os.path.join(root, file))

print("All file classes updated successfully.")
