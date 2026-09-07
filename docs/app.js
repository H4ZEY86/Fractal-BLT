// Routing Logic
function handleRouting() {
    const hash = window.location.hash || '#home';
    const views = document.querySelectorAll('.view-section');
    const links = document.querySelectorAll('nav a');

    views.forEach(view => view.classList.remove('active'));
    links.forEach(link => link.classList.remove('active'));

    const activeView = document.getElementById(`view-${hash.substring(1)}`);
    if (activeView) {
        activeView.classList.add('active');
        document.querySelector(`nav a[href="${hash}"]`).classList.add('active');
    } else {
        document.getElementById('view-home').classList.add('active');
        document.querySelector('nav a[href="#home"]').classList.add('active');
    }
}

window.addEventListener('hashchange', handleRouting);
document.addEventListener('DOMContentLoaded', handleRouting);

// Terminal Simulator
const termOutput = document.getElementById('term-output');
const termInput = document.getElementById('term-input');

const responses = {
    'help': "Commands:\n  help    - Display commands\n  bench   - Execute 10MB telemetry gauntlet\n  route   - Inspect GNN matrix calculations\n  python  - Attempt to run this in Python\n  status  - Print system health\n  clear   - Clear terminal",
    'bench': "[+] Allocating 0 bytes on managed heap...\n[+] Initializing 10MB byte stream from NVMe...\n[+] Executing Shannon Entropy Patcher (327,680 patches generated).\n[+] cuLaunchKernel SGEMV PTX across RTX 5070 Ti...\n[SUCCESS] Gauntlet completed.\n    -> Throughput: 45.05 MB/s\n    -> GC Collections: 0\n    -> Heap Size: 10.06 MB",
    'route': "[+] Constructing 8x8 RBF similarity matrix on stack...\n[+] Convolving 1-hop GCN message-passing weights...\n[+] Executing Linear Projection...\n[SUCCESS] Expert ID #42 selected (Weight: 0.9412).",
    'status': "Host OS: Omarchy Linux / Windows 11 NativeAOT\nRuntime: .NET 10.0 (Release)\nGC State: DORMANT. 0 Gen0/Gen1/Gen2 collections.\nMemory Footprint: 10.06 MB.",
    'python': "<span style='color:#ff3333'>[FATAL] Executing Python...</span>\n<span style='color:#ff3333'>Loading torch... (Waiting 4.2 seconds)</span>\n<span style='color:#ff3333'>Allocating 3.5GB to VRAM...</span>\n<span style='color:#ff3333'>RuntimeError: CUDA out of memory.</span>\n<span style='color:#ff3333'>GIL deadlock detected.</span>\n<span style='color:var(--neon-cyan)'>[!] Why are you doing this to yourself? Use Fractal-BLT.</span>",
    'clear': ""
};

async function typeText(text, element) {
    const lines = text.split('\n');
    for (let i = 0; i < lines.length; i++) {
        element.innerHTML += lines[i] + "<br>";
        element.scrollTop = element.scrollHeight;
        await new Promise(r => setTimeout(r, 40));
    }
}

if(termInput) {
    termInput.addEventListener('keydown', async function(e) {
        if (e.key === 'Enter') {
            const cmd = termInput.value.trim().toLowerCase();
            termInput.value = '';
            
            if (cmd === 'clear') {
                termOutput.innerHTML = '<br>> ';
                return;
            }
            
            termOutput.innerHTML += `<br><span style="color:var(--text-bright)">> ${cmd}</span><br>`;
            
            const response = responses[cmd] || `Command not recognized: '${cmd}'. Type 'help' for options.`;
            
            if (cmd === 'python' || cmd === 'bench' || cmd === 'route') {
                termInput.disabled = true;
                await typeText(response, termOutput);
                termOutput.innerHTML += "<br>> ";
                termInput.disabled = false;
                termInput.focus();
            } else {
                termOutput.innerHTML += response.replace(/\n/g, '<br>') + "<br><br>> ";
            }
            termOutput.scrollTop = termOutput.scrollHeight;
        }
    });
}

// Architecture Interaction
const archData = {
    'nvme': '<h3>NVMe Storage (safetensors)</h3><p>Raw weight tensors residing on Gen4/Gen5 solid-state drives. Bypasses the OS page cache entirely using unbuffered direct I/O (FILE_FLAG_NO_BUFFERING).</p>',
    'blt': '<h3>BltEncoder (Shannon Entropy)</h3><p>Replaces tokenizers. Scans UTF-8 byte stream using a sliding <code>stackalloc int[256]</code> table. Computes <code>H = -sum(p * log2(p))</code> natively. When entropy peaks (>4.0), a semantic boundary is drawn.</p>',
    'gnn': '<h3>GnnRouter (1-Hop GCN)</h3><p>Allocates an 8x8 RBF similarity matrix on the thread stack. Convolves patch lengths and entropy peaks to pass messages between local patches. Dot products against <code>ExpertRegistry</code> to route to top-K experts.</p>',
    'dma': '<h3>PCIe Pinned DMA (FractalBridge)</h3><p>Uses CUDA Driver API (<code>cuMemHostRegister</code>) to pin unmanaged host buffers. Executes <code>cuMemcpyHtoDAsync</code> to copy weights directly to the RTX 5070 Ti, overlapping compute and I/O.</p>',
    'ptx': '<h3>Raw PTX Kernel (SGEMV)</h3><p>Embedded raw PTX assembly string in the C# binary. JIT compiled at boot via <code>cuModuleLoadData</code>. Dispatched natively via <code>cuLaunchKernel</code>. Zero dependencies on cuBLAS or external DLLs.</p>'
};

document.querySelectorAll('.arch-node').forEach(node => {
    node.addEventListener('click', function() {
        document.querySelectorAll('.arch-node').forEach(n => n.classList.remove('active'));
        this.classList.add('active');
        const target = this.getAttribute('data-target');
        document.getElementById('arch-details-content').innerHTML = archData[target];
    });
});

// Benchmark Animation
const btnBench = document.getElementById('btn-run-bench');
if(btnBench) {
    btnBench.addEventListener('click', () => {
        const bltBar = document.getElementById('bar-blt');
        const pyBar = document.getElementById('bar-py');
        const bltVal = document.getElementById('val-blt');
        const pyVal = document.getElementById('val-py');
        
        // Reset
        bltBar.style.height = '0%';
        pyBar.style.height = '0%';
        bltVal.innerText = '0 MB/s';
        pyVal.innerText = '0 MB/s';
        btnBench.disabled = true;

        setTimeout(() => {
            bltBar.style.height = '100%'; // Max height
            pyBar.style.height = '5%';    // Tiny height
            
            let count = 0;
            let iv = setInterval(() => {
                count += 2;
                if(count >= 45) {
                    clearInterval(iv);
                    bltVal.innerText = '45.05 MB/s';
                    pyVal.innerText = '0.2 MB/s (GIL)';
                    btnBench.disabled = false;
                } else {
                    bltVal.innerText = count + ' MB/s';
                }
            }, 30);
        }, 500);
    });
}

// API SSE Simulator
const btnApi = document.getElementById('btn-api');
const apiOutput = document.getElementById('api-output');
if(btnApi) {
    btnApi.addEventListener('click', async () => {
        btnApi.disabled = true;
        apiOutput.innerHTML = "Initializing connection to /v1/chat/completions...\n";
        await new Promise(r => setTimeout(r, 800));
        apiOutput.innerHTML += "Connected. Streaming Server-Sent Events (SSE):\n\n";
        
        const chunks = [
            "data: {\"choices\": [{\"delta\": {\"content\": \" [Expert 42 -> lm_head] GPU Logits: 0.6926\"}}]}\n\n",
            "data: {\"choices\": [{\"delta\": {\"content\": \" [Expert 13 -> lm_head] GPU Logits: 1.0248\"}}]}\n\n",
            "data: {\"choices\": [{\"delta\": {\"content\": \" [Expert 63 -> lm_head] GPU Logits: 1.5537\"}}]}\n\n",
            "data: {\"choices\": [{\"delta\": {\"content\": \" [Expert 7 -> lm_head] GPU Logits: 0.0323\"}}]}\n\n",
            "data: [DONE]\n"
        ];

        for(let chunk of chunks) {
            await new Promise(r => setTimeout(r, 400));
            apiOutput.innerHTML += `<span style="color:var(--neon-green)">${chunk}</span>`;
            apiOutput.scrollTop = apiOutput.scrollHeight;
        }
        btnApi.disabled = false;
    });
}
