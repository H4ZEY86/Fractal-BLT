# Fractal-BLT API Reference

The Fractal-BLT server implements a subset of the standard OpenAI API specification. This allows it to function as a drop-in, zero-allocation replacement backend for many existing frontends (like LocalUI, SillyTavern, or standard OpenAI SDKs).

---

## 1. Chat Completions

Generates a model response for the given chat conversation.

**Endpoint:**  
`POST /v1/chat/completions`

**Headers:**  
- `Content-Type: application/json`

### Request Body

| Field | Type | Required | Description |
|---|---|---|---|
| `model` | string | Optional | The ID of the model to use. Defaults to the environment variable `FRACTAL_MODEL` if not specified. |
| `messages` | array | **Yes** | A list of messages comprising the conversation so far. |
| `stream` | boolean | Optional | If `true`, partial message deltas will be sent via Server-Sent Events (SSE). |

#### Example `messages` Object
```json
[
  {
    "role": "system",
    "content": "You are a helpful assistant."
  },
  {
    "role": "user",
    "content": "Initialize sequence: test tensor read stream."
  }
]
```

### Response (Streaming: `true`)

When `stream` is set to `true`, the server uses Server-Sent Events (SSE) to push zero-copy logits directly from the GPU VRAM as they are generated. 

Each token is emitted as a `data:` payload containing a JSON chunk. The stream is terminated by a `data: [DONE]` payload.

**Example Stream:**
```text
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","model":"fractal-moe","choices":[{"index":0,"delta":{"content":"[Expert"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","model":"fractal-moe","choices":[{"index":0,"delta":{"content":" 63"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","model":"fractal-moe","choices":[{"index":0,"delta":{"content":" ->"},"finish_reason":null}]}

data: [DONE]
```

---

## Error Handling

Fractal-BLT APIs are designed to fail fast. If the direct memory DMA pipeline or the PTX compiler encounters an error, the server will return a standard HTTP error code.

| Status Code | Description | Resolution |
|---|---|---|
| `400 Bad Request` | Malformed JSON payload. | Ensure your request matches the OpenAI specification. |
| `404 Not Found` | The specified route does not exist. | Ensure you are hitting `/v1/chat/completions`. |
| `500 Internal Server Error` | The NVMe drive could not be mapped, or the CUDA driver threw a `CudaException` (e.g. PTX JIT failure). | Check server console logs for exact `CUresult` codes or path configuration issues. |
