# API Reference

Fractal-BLT exposes a minimal, high-performance OpenAI-compatible REST API via Kestrel. 

## Endpoints

### `POST /v1/chat/completions`

Generates token completions (or raw latent patches) for a given prompt, streamed directly from GPU memory via Server-Sent Events (SSE).

#### Request

**Content-Type**: `application/json`

| Field      | Type      | Required | Description |
|------------|-----------|----------|-------------|
| `model`    | string    | Yes      | The identifier of the MoE model (e.g., `"fractal-moe-64x"`). |
| `messages` | array     | Yes      | An array of message objects. |
| `stream`   | boolean   | No       | Must be set to `true`. Non-streaming responses are currently unsupported by the unmanaged pipeline. |

**Message Object:**

| Field     | Type   | Required | Description |
|-----------|--------|----------|-------------|
| `role`    | string | Yes      | The role of the author (e.g., `"user"`, `"assistant"`). |
| `content` | string | Yes      | The UTF-8 string content of the message. |

**Example Request:**
```json
{
  "model": "fractal-moe-64x",
  "messages": [
    {
      "role": "user",
      "content": "Compute"
    }
  ],
  "stream": true
}
```

#### Response

Responses are streamed using the `text/event-stream` format. Each chunk represents a zero-copy memory pipe flush from the GPU.

**Chunk Object (Stream):**

| Field     | Type   | Description |
|-----------|--------|-------------|
| `choices` | array  | A list of choice objects containing the generated delta. |

**Choice Object:**

| Field   | Type   | Description |
|---------|--------|-------------|
| `delta` | object | Contains the `content` field with the generated token or logit array. |

**Example Stream Sequence:**
```text
data: {"choices": [{"delta": {"content": " [Expert 42 -> lm_head] GPU Logits: 0.6926"}}]}

data: {"choices": [{"delta": {"content": " [Expert 13 -> lm_head] GPU Logits: 1.0248"}}]}

data: {"choices": [{"delta": {"content": " [Expert 63 -> lm_head] GPU Logits: 1.5537"}}]}

data: [DONE]
```

#### Error Codes

If the pipeline encounters a hardware or memory fault, it returns standard HTTP error codes.

| Status | Code                  | Description |
|--------|-----------------------|-------------|
| `400`  | `Bad Request`         | Malformed JSON or `stream: false` (unsupported). |
| `500`  | `CUDA_ERROR`          | Physical fault in the driver (`cuInit`, `cuLaunchKernel`). |
| `503`  | `Service Unavailable` | Tensor read failed from NVMe or `safetensors` corrupted. |
