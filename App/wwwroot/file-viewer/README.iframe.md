# Flyfish File Viewer official zero-dependency demo artifact

This archive is the official static demo build for zero-dependency iframe integration and private deployment.
Extract every file in this archive to the same static directory. Do not move `assets/`,
`vendor/`, `wasm/`, or `example/` away from the HTML entries.

## Entries

| Entry | Purpose |
| --- | --- |
| `iframe.html` | Recommended chrome-free iframe entry for customer systems. |
| `index.html` | Full official demo with sample selector and toolbar; keeps the same URL and Blob handoff protocol for existing integrations. |
| `compare.html` | Two-pane document comparison demo. |
| `iframe-example.html` | Parent-page example for URL and Blob postMessage integration. |

## URL iframe

```html
<iframe
  src="/file-viewer/iframe.html?url=/files/demo.docx"
  style="width:100%;height:720px;border:0"
  allow="fullscreen"
></iframe>
```

Use a same-origin URL, signed intranet URL, or browser-accessible absolute URL in `url`.
The viewer, worker, WASM, font, and vendor assets resolve from this static directory and do
not require a public CDN at runtime.

## Blob postMessage iframe

```html
<iframe id="viewer" src="/file-viewer/iframe.html?from=https%3A%2F%2Fapp.example.com&name=contract.docx"></iframe>
<script>
  const frame = document.querySelector('#viewer')
  const file = await fetch('/api/files/contract.docx').then(response => response.blob())
  frame.contentWindow.postMessage(file, 'https://static.example.com')
</script>
```

`from` must match the parent page origin. The iframe accepts a `Blob`, wraps it as a
`File` with `name`, and renders it without exposing the full demo shell.

The original full demo entry uses the same protocol. If an existing integration already points
at `index.html?from=<parent-origin>&name=<filename>`, it can continue to post the same `Blob`.

## Recommended headers

- `Content-Type: application/wasm` for `*.wasm`
- `Cache-Control: public, max-age=31536000, immutable` for hashed `assets/`, `vendor/`, and `wasm/`
- `X-Content-Type-Options: nosniff`
- Allow iframe embedding from your application origin with your gateway or CDN policy.
