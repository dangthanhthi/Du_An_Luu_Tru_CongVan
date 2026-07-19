import os
import json

log_dir = r"C:\Users\MSIIIIII\.gemini\antigravity\brain\a562e668-185f-47c7-98f6-609047e48cbe\.system_generated\logs"
transcript_path = os.path.join(log_dir, "transcript_full.jsonl")

# Let's search for "app/(main)/partners/page.tsx" or "partners/page.tsx"
if os.path.exists(transcript_path):
    print("Found transcript_full.jsonl")
    with open(transcript_path, "r", encoding="utf-8") as f:
        for line in f:
            if "partners/page.tsx" in line:
                try:
                    obj = json.loads(line)
                    # Print step index and type
                    print(f"Step: {obj.get('step_index')}, Type: {obj.get('type')}")
                    # If it's a code block or contains file content, print a snippet
                    content = obj.get("content", "")
                    if "export default function Partners" in content:
                        print("Found partner page code snippet!")
                        # Write code to a temp file to review
                        with open("temp_partner_code.txt", "w", encoding="utf-8") as out:
                            out.write(content)
                except Exception as e:
                    pass
else:
    print("Transcript not found at", transcript_path)
