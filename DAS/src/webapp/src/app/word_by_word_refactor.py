import os
import re

base_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\src"

word_map = {
    # Backgrounds
    'bg-white': 'bg-card',
    'bg-[#FFFFFF]': 'bg-background',
    'bg-slate-50': 'bg-muted',
    'bg-slate-100': 'bg-muted',
    'bg-slate-150': 'bg-muted',
    'bg-slate-50/50': 'bg-muted/50',
    'bg-[#F1F5F9]/60': 'bg-muted',
    'bg-[#FFFFFF]/80': 'bg-card',
    'bg-white/60': 'bg-card/60',
    
    # Borders
    'border-slate-200': 'border-border',
    'border-slate-150': 'border-border',
    'border-slate-100': 'border-border',
    'border-slate-200/80': 'border-border',
    'border-slate-200/50': 'border-border',
    'border-slate-900': 'border-border',
    'border-slate-950': 'border-border',
    
    # Divides
    'divide-slate-100': 'divide-border',
    'divide-slate-200': 'divide-border',
    
    # Text colors
    'text-slate-800': 'text-foreground',
    'text-slate-900': 'text-foreground',
    'text-slate-700': 'text-foreground',
    'text-zinc-100': 'text-foreground',
    'text-slate-500': 'text-muted-foreground',
    'text-slate-400': 'text-muted-foreground',
    'text-slate-450': 'text-muted-foreground',
    'text-slate-650': 'text-muted-foreground',
    'text-slate-600': 'text-muted-foreground',
    'text-zinc-400': 'text-muted-foreground',
    'text-zinc-500': 'text-muted-foreground',
    'text-foreground dark:text-slate-350': 'text-foreground',
    
    # Remove dark overrides that become redundant
    'dark:bg-zinc-900': '',
    'dark:bg-zinc-950': '',
    'dark:bg-zinc-800': '',
    'dark:bg-slate-950': '',
    'dark:bg-slate-900': '',
    'dark:bg-slate-900/60': '',
    'dark:border-zinc-800': '',
    'dark:border-slate-800': '',
    'dark:border-slate-850': '',
    'dark:border-slate-200': '',
    'dark:divide-zinc-800': '',
    'dark:text-slate-100': '',
    'dark:text-slate-200': '',
    'dark:text-slate-300': '',
    'dark:text-slate-400': '',
    'dark:text-slate-350': '',
    'dark:text-muted-foreground': '',
    'dark:text-zinc-100': '',
    'dark:text-zinc-400': '',
    'dark:hover:bg-zinc-800/30': '',
    'hover:bg-slate-50/50': 'hover:bg-muted/40',
    'hover:bg-slate-50': 'hover:bg-muted',
    'hover:bg-slate-100': 'hover:bg-muted',
    'dark:hover:bg-slate-800': '',
}

def clean_class_string(class_str, file_name):
    # Split classes by spaces
    classes = class_str.split()
    new_classes = []
    for c in classes:
        # Special case: layout.tsx bg-white should be bg-background
        if c == 'bg-white' and 'layout.tsx' in file_name:
            new_classes.append('bg-background')
        elif c in word_map:
            mapped = word_map[c]
            if mapped: # Only add if it's not mapped to empty string
                new_classes.append(mapped)
        else:
            new_classes.append(c)
    # Join and return
    return ' '.join(new_classes)

def process_file(file_path):
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()
    
    orig = content
    file_name = os.path.basename(file_path)

    # Regex to find className="..." or className={`...`}
    # We will find matches and clean their class content
    def repl_className(match):
        prefix = match.group(1) # className="
        class_content = match.group(2) # ...
        suffix = match.group(3) # "
        
        # Clean the class string
        cleaned = clean_class_string(class_content, file_name)
        return f'{prefix}{cleaned}{suffix}'

    # Match className="any text without quote"
    content = re.sub(r'(className=")([^"]+)(")', repl_className, content)
    
    # Match className='any text without quote'
    content = re.sub(r"(className=')([^']+)(')", repl_className, content)

    # Specific cleanups
    content = content.replace("bg-slate-100 text-slate-800 dark:bg-zinc-800 dark:text-slate-350", "bg-muted text-foreground")
    content = content.replace("bg-slate-150 dark:bg-zinc-800", "bg-muted")

    if content != orig:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print(f"Processed: {os.path.relpath(file_path, base_dir)}")

# Walk and clean all TSX files
for root, dirs, files in os.walk(base_dir):
    for file in files:
        if file.endswith(".tsx"):
            process_file(os.path.join(root, file))

print("Comprehensive word-by-word cleanup completed successfully.")
