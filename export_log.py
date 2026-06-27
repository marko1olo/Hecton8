import json
import re

output = []
with open('C:\\Users\\Admin\\.gemini\\antigravity\\brain\\9412af70-ebf5-491e-80e6-e0b2fcde1017\\.system_generated\\logs\\transcript.jsonl', 'r', encoding='utf-8') as f:
    for line in f:
        if not line.strip(): continue
        try:
            obj = json.loads(line)
        except:
            continue
            
        if obj.get('type') == 'USER_INPUT' and obj.get('source') == 'USER_EXPLICIT':
            msg = obj.get('content', '')
            msg = re.sub(r'<USER_REQUEST>', '', msg)
            msg = re.sub(r'</USER_REQUEST>', '', msg)
            msg = re.sub(r'(?s)<ADDITIONAL_METADATA>.*?</ADDITIONAL_METADATA>', '', msg)
            msg = re.sub(r'(?s)<EPHEMERAL_MESSAGE>.*?</EPHEMERAL_MESSAGE>', '', msg)
            
            # Additional cleanup: remove the system reminder block added recently
            msg = re.sub(r'(?s)The following is an <EPHEMERAL_MESSAGE>.*?</EPHEMERAL_MESSAGE>', '', msg)
            
            output.append('---')
            output.append('**USER:**')
            output.append(msg.strip())
            output.append('')
        elif obj.get('type') == 'PLANNER_RESPONSE' and obj.get('source') == 'MODEL':
            content = obj.get('content', '').strip()
            if content:
                output.append('---')
                output.append('**AGENT:**')
                output.append(content)
                output.append('')

with open('C:\\Users\\Admin\\.gemini\\antigravity\\brain\\9412af70-ebf5-491e-80e6-e0b2fcde1017\\FULL_DIALOGUE_LOG.md', 'w', encoding='utf-8') as f:
    f.write('\n'.join(output))
