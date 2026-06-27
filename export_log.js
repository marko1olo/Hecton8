const fs = require('fs');
const readline = require('readline');

const inputFile = 'C:\\Users\\Admin\\.gemini\\antigravity\\brain\\9412af70-ebf5-491e-80e6-e0b2fcde1017\\.system_generated\\logs\\transcript.jsonl';
const outputFile = 'C:\\Users\\Admin\\.gemini\\antigravity\\brain\\9412af70-ebf5-491e-80e6-e0b2fcde1017\\FULL_DIALOGUE_LOG.md';

const readInterface = readline.createInterface({
    input: fs.createReadStream(inputFile, { encoding: 'utf8' }),
    console: false
});

let outputText = '# ПОЛНЫЙ ЛОГ ДИАЛОГА\n\n';

readInterface.on('line', function(line) {
    if (!line.trim()) return;
    try {
        const obj = JSON.parse(line);
        if (obj.type === 'USER_INPUT' && obj.source === 'USER_EXPLICIT') {
            let msg = obj.content || '';
            msg = msg.replace(/<USER_REQUEST>/g, '')
                     .replace(/<\/USER_REQUEST>/g, '')
                     .replace(/<ADDITIONAL_METADATA>[\s\S]*?<\/ADDITIONAL_METADATA>/g, '')
                     .replace(/<EPHEMERAL_MESSAGE>[\s\S]*?<\/EPHEMERAL_MESSAGE>/g, '');
            
            // Remove ephemeral reminders sometimes added inside USER_INPUT
            msg = msg.replace(/The following is an <EPHEMERAL_MESSAGE>[\s\S]*?<\/EPHEMERAL_MESSAGE>/g, '');
            
            outputText += '---\n**ПОЛЬЗОВАТЕЛЬ:**\n' + msg.trim() + '\n\n';
        } else if (obj.type === 'PLANNER_RESPONSE' && obj.source === 'MODEL') {
            let content = (obj.content || '').trim();
            if (content) {
                outputText += '---\n**АГЕНТ:**\n' + content + '\n\n';
            }
        }
    } catch (e) {
        // ignore JSON parse errors for incomplete lines
    }
});

readInterface.on('close', function() {
    fs.writeFileSync(outputFile, outputText, { encoding: 'utf8' });
    console.log('Log exported successfully!');
});
