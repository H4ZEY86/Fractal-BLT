const chatContainer = document.getElementById('chatContainer');
const promptInput = document.getElementById('promptInput');
const sendBtn = document.getElementById('sendBtn');
const statusIndicator = document.querySelector('.status');

const API_URL = 'http://localhost:5000/v1/chat/completions';

function addMessage(role, content) {
    const div = document.createElement('div');
    div.className = `message ${role}`;
    
    const roleDiv = document.createElement('div');
    roleDiv.className = 'role';
    roleDiv.textContent = role === 'user' ? 'USER' : 'FRACTAL-BLT';
    
    const contentDiv = document.createElement('div');
    contentDiv.className = 'content';
    contentDiv.textContent = content;
    
    div.appendChild(roleDiv);
    div.appendChild(contentDiv);
    chatContainer.appendChild(div);
    chatContainer.scrollTop = chatContainer.scrollHeight;
    
    return contentDiv;
}

async function sendPrompt() {
    const text = promptInput.value.trim();
    if (!text) return;
    
    // Add user message
    addMessage('user', text);
    promptInput.value = '';
    sendBtn.disabled = true;
    promptInput.disabled = true;
    statusIndicator.textContent = '[ API: COMPUTING... ]';
    statusIndicator.style.color = 'var(--neon-magenta)';
    
    // Create assistant message container for streaming
    const replyContentDiv = addMessage('assistant', '');
    
    try {
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'text/event-stream'
            },
            body: JSON.stringify({
                model: 'local-model',
                messages: [{ role: 'user', content: text }],
                stream: true
            })
        });
        
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        
        const reader = response.body.getReader();
        const decoder = new TextDecoder('utf-8');
        
        while (true) {
            const { value, done } = await reader.read();
            if (done) break;
            
            const chunk = decoder.decode(value, { stream: true });
            const lines = chunk.split('\n');
            
            for (const line of lines) {
                if (line.startsWith('data: ')) {
                    const dataStr = line.substring(6).trim();
                    if (dataStr === '[DONE]') {
                        break;
                    }
                    if (!dataStr) continue;
                    
                    try {
                        const data = JSON.parse(dataStr);
                        const token = data.choices[0].delta.content;
                        if (token) {
                            replyContentDiv.textContent += token;
                            chatContainer.scrollTop = chatContainer.scrollHeight;
                        }
                    } catch (e) {
                        console.error('Error parsing SSE chunk:', e, dataStr);
                    }
                }
            }
        }
    } catch (error) {
        console.error('Fetch error:', error);
        replyContentDiv.textContent += `\n\n[ERROR: Failed to communicate with FractalServe. Ensure it is running on port 5000.]`;
        replyContentDiv.style.color = '#ef4444';
    } finally {
        sendBtn.disabled = false;
        promptInput.disabled = false;
        promptInput.focus();
        statusIndicator.textContent = '[ API: CONNECTED ]';
        statusIndicator.style.color = 'var(--neon-green)';
    }
}

// Event Listeners
sendBtn.addEventListener('click', sendPrompt);

promptInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        sendPrompt();
    }
});
