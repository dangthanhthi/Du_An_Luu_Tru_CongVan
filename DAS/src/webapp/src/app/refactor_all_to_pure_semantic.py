import os
import re

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\src"

# 1. Overwrite globals.css with Tailwind CSS v4 class-based dark mode config
globals_css = """@import "tailwindcss";

@theme {
  --dark-mode: selector(.dark);
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-border: var(--border);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
}

:root {
  --background: #ffffff;
  --foreground: #09090b;
  --card: #ffffff;
  --card-foreground: #09090b;
  --border: #d0d7de; /* GitHub light border */
  --primary: #24292f; /* GitHub light primary */
  --primary-foreground: #ffffff;
  --muted: #f6f8fa; /* GitHub light muted bg */
  --muted-foreground: #57606a; /* GitHub light muted text */
  --accent: #1f883d; /* GitHub light green */
  --accent-foreground: #ffffff;
}

.dark {
  --background: #0d1117; /* GitHub dark bg */
  --foreground: #c9d1d9; /* GitHub dark text */
  --card: #161b22; /* GitHub dark card bg */
  --card-foreground: #c9d1d9;
  --border: #30363d; /* GitHub dark border */
  --primary: #f0f6fc;
  --primary-foreground: #0d1117;
  --muted: #21262d; /* GitHub dark muted bg */
  --muted-foreground: #8b949e; /* GitHub dark muted text */
  --accent: #238636; /* GitHub dark green */
  --accent-foreground: #ffffff;
}

body {
  background-color: var(--background);
  color: var(--foreground);
  font-family: var(--font-sans), sans-serif;
  transition: background-color 0.25s, color 0.25s;
}

/* Custom scrollbar */
::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}
::-webkit-scrollbar-track {
  background: transparent;
}
::-webkit-scrollbar-thumb {
  background: var(--border);
  border-radius: 3px;
}
::-webkit-scrollbar-thumb:hover {
  background: var(--muted-foreground);
}
"""

with open(os.path.join(base_dir, "app", "globals.css"), "w", encoding="utf-8") as f:
    f.write(globals_css)
print("Updated: app/globals.css with proper @theme dark-mode selector config")

# 2. Define a function to perform comprehensive class string updates in all TSX files
def clean_file_classes(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    orig = content

    # Replace specific text-slate combinations
    content = content.replace("text-slate-800 dark:text-slate-200", "text-foreground")
    content = content.replace("bg-slate-100 dark:bg-zinc-800 text-foreground", "bg-muted text-foreground")
    content = content.replace("bg-slate-100 dark:bg-zinc-800", "bg-muted")

    # Replace hardcoded layout backgrounds
    content = content.replace("bg-white dark:bg-slate-950", "bg-background")
    content = content.replace("bg-slate-50 dark:bg-slate-950", "bg-background")
    content = content.replace("bg-[#FFFFFF] dark:bg-[#090D16]", "bg-background")
    
    # Replace active sidebar item block style
    content = content.replace(
        "bg-foreground text-background dark:bg-foreground dark:text-background border-l-4 border-accent",
        "bg-muted text-foreground border-l-4 border-accent"
    )

    # Replace hardcoded backgrounds
    content = re.sub(r"bg-white dark:bg-zinc-900", "bg-card", content)
    content = re.sub(r"bg-white dark:bg-zinc-950", "bg-card", content)
    content = re.sub(r"bg-slate-50 dark:bg-zinc-900", "bg-muted", content)
    content = re.sub(r"bg-slate-50 dark:bg-zinc-950", "bg-muted", content)
    content = re.sub(r"bg-[#FFFFFF]/80 dark:bg-slate-900/60", "bg-card", content)
    content = re.sub(r"bg-\[#FFFFFF\]/80 dark:bg-slate-900/60", "bg-card", content)
    content = re.sub(r"bg-white/60 dark:bg-slate-900/60", "bg-card/60", content)
    
    # Form control backgrounds
    content = re.sub(r"bg-slate-50 dark:bg-zinc-950", "bg-muted", content)
    content = re.sub(r"bg-slate-50/50 dark:bg-zinc-900", "bg-muted/50", content)
    
    # Hover states
    content = re.sub(r"hover:bg-slate-50/50 dark:hover:bg-zinc-800/30", "hover:bg-muted/40", content)
    content = re.sub(r"hover:bg-slate-50 dark:hover:bg-slate-800", "hover:bg-muted", content)
    content = re.sub(r"hover:bg-slate-100 dark:hover:bg-slate-800", "hover:bg-muted", content)
    
    # Borders
    content = re.sub(r"border-slate-200 dark:border-zinc-800", "border-border", content)
    content = re.sub(r"border-slate-100 dark:border-zinc-800", "border-border", content)
    content = re.sub(r"border-slate-100 dark:border-slate-800", "border-border", content)
    content = re.sub(r"border-slate-100 dark:border-slate-850", "border-border", content)
    content = re.sub(r"border-slate-200/80 dark:border-slate-800", "border-border", content)
    content = re.sub(r"border-slate-200/50 dark:border-slate-800", "border-border", content)
    
    # Text colors
    content = re.sub(r"text-slate-800 dark:text-slate-100", "text-foreground", content)
    content = re.sub(r"text-slate-900 dark:text-slate-100", "text-foreground", content)
    content = re.sub(r"text-slate-700 dark:text-slate-300", "text-foreground", content)
    content = re.sub(r"text-slate-900 dark:text-slate-200", "text-foreground", content)
    content = re.sub(r"text-zinc-100", "text-foreground", content)
    content = re.sub(r"text-slate-500 dark:text-slate-400", "text-muted-foreground", content)
    content = re.sub(r"text-slate-550", "text-muted-foreground", content)
    content = re.sub(r"text-slate-500", "text-muted-foreground", content)
    content = re.sub(r"text-slate-400", "text-muted-foreground", content)
    content = re.sub(r"text-zinc-400", "text-muted-foreground", content)
    content = re.sub(r"text-zinc-500", "text-muted-foreground", content)
    content = re.sub(r"text-slate-600 dark:text-muted-foreground", "text-muted-foreground", content)
    content = re.sub(r"text-slate-650", "text-muted-foreground", content)
    content = re.sub(r"text-slate-600", "text-muted-foreground", content)
    content = re.sub(r"text-foreground dark:text-slate-350", "text-foreground", content)
    
    # Hardcoded text and bg tags
    content = re.sub(r"bg-slate-100 text-slate-800 dark:bg-zinc-800 dark:text-slate-350", "bg-muted text-foreground", content)
    content = re.sub(r"bg-slate-150 dark:bg-zinc-800", "bg-muted", content)
    
    # CTA Buttons (Monochrome light / Dark button colors)
    content = re.sub(
        r"bg-slate-900 hover:bg-slate-800 text-white dark:bg-white dark:text-zinc-950 dark:hover:bg-slate-100",
        "bg-primary text-primary-foreground hover:opacity-90",
        content
    )

    if content != orig:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Cleaned: {os.path.relpath(file_path, base_dir)}")

# Walk and clean all TSX files
for root, dirs, files in os.walk(base_dir):
    for file in files:
        if file.endswith(".tsx"):
            clean_file_classes(os.path.join(root, file))

print("Comprehensive cleanup completed successfully.")
