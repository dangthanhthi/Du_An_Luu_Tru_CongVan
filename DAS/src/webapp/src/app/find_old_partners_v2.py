import os
import json

log_dir = r"C:\Users\MSIIIIII\.gemini\antigravity\brain\a562e668-185f-47c7-98f6-609047e48cbe\.system_generated\logs"
transcript_path = os.path.join(log_dir, "transcript_full.jsonl")

# Let's search step 330 or 367 in transcript
if os.path.exists(transcript_path):
    with open(transcript_path, "r", encoding="utf-8") as f:
        for line in f:
            obj = json.loads(line)
            step = obj.get("step_index")
            if step in [330, 367, 371]:
                content = obj.get("content", "")
                # Let's check if the file app/(main)/partners/page.tsx was written or mentioned
                if "app/(main)/partners/page.tsx" in content or "partners/page.tsx" in content:
                    print(f"--- Code at Step {step} ---")
                    # Let's search for the code block
                    start_idx = content.find("export default function Partners")
                    if start_idx != -1:
                        # Find the end of the file/block
                        end_idx = content.find("```", start_idx)
                        if end_idx != -1:
                            print(content[start_idx:end_idx])
                        else:
                            print(content[start_idx:start_idx+1000])
                    else:
                        print("Function definition not found, printing partial content:")
                        # Look for writes
                        print(content[:800])
else:
    print("Transcript not found")
