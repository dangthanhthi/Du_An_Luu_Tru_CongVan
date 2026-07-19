import os
import shutil

src_dir = r"C:\Users\MSIIIIII\.gemini\antigravity\brain\a562e668-185f-47c7-98f6-609047e48cbe"
dest_dir = r"c:\Users\MSIIIIII\Desktop\Dự án taskmanager\DAS\src\webapp\public"

os.makedirs(dest_dir, exist_ok=True)

# List of files in src
for f in os.listdir(src_dir):
    if f.startswith("media__") and f.endswith(".png"):
        src_path = os.path.join(src_dir, f)
        size = os.path.getsize(src_path)
        # We can copy them with their original name first
        dest_path = os.path.join(dest_dir, f)
        shutil.copy2(src_path, dest_path)
        print(f"Copied: {f} (Size: {size} bytes)")
        
        # Let's also copy to a clean name for simplicity
        # The crystal background is usually the first uploaded in the message
        # Let's see the timestamp or alphabetical order.
        # Let's make copies of both with descriptive names
        if "1784437289865" in f:
            shutil.copy2(src_path, os.path.join(dest_dir, "bg-crystals.png"))
            print("Mapped 1784437289865 to bg-crystals.png")
        if "1784437309457" in f:
            shutil.copy2(src_path, os.path.join(dest_dir, "landing-screenshot.png"))
            print("Mapped 1784437309457 to landing-screenshot.png")
