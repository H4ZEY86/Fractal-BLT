#!/usr/bin/env python3
"""
Example Python script to consume the Fractal-BLT SSE endpoint.

Requires:
    pip install requests sseclient-py
"""

import json
import requests
import sseclient
import sys

API_URL = "http://localhost:5000/v1/chat/completions"

def stream_fractal_blt():
    payload = {
        "model": "fractal-moe-64x",
        "messages": [{"role": "user", "content": "Compute"}],
        "stream": True
    }
    
    headers = {
        "Accept": "text/event-stream",
        "Content-Type": "application/json"
    }

    print(f"[+] Connecting to Fractal-BLT NativeAOT Runtime at {API_URL}...")
    
    try:
        response = requests.post(API_URL, json=payload, headers=headers, stream=True)
        response.raise_for_status()
        
        client = sseclient.SSEClient(response)
        
        print("[+] SSE Stream Established. Listening for GPU logits:\n")
        
        for event in client.events():
            if event.data == "[DONE]":
                print("\n\n[+] Stream Complete.")
                break
                
            try:
                data = json.loads(event.data)
                content = data["choices"][0]["delta"]["content"]
                
                # Print incrementally to stdout
                sys.stdout.write(content)
                sys.stdout.flush()
                
            except (json.JSONDecodeError, KeyError) as e:
                print(f"\n[!] Malformed chunk: {event.data}")
                
    except requests.exceptions.RequestException as e:
        print(f"\n[!] Connection failed: {e}")
        print("    Ensure FractalServe is running (`dotnet run --project FractalServe`).")

if __name__ == "__main__":
    stream_fractal_blt()
