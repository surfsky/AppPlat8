# File Viewer Markdown Demo

This sample is intentionally longer than a placeholder README. It checks whether
the Markdown renderer keeps a clean reading surface in both light and dark system
themes while still preserving tables, block quotes, task lists, links, and code
fences.

## Release Checklist

- [x] Route `.md` and `.markdown` files to the Markdown renderer.
- [x] Keep Markdown styling isolated from the demo shell theme.
- [x] Render code fences with readable contrast.
- [x] Export the rendered document as HTML without executing user scripts.
- [ ] Add your own business samples before production rollout.

> A preview component should feel quiet and dependable. The surrounding app can
> adapt to the user's theme, but document content must remain legible and
> predictable.

## Supported Reading Surfaces

| Format family | Example file | Rendering strategy | Print button |
| --- | --- | --- | --- |
| Markdown | `markdown.md` | Marked + GitHub Markdown CSS | Available |
| Source code | `code.ts` | highlight.js, language loaded on demand | Available |
| Word | `word.docx` | Dedicated page export adapter | Available |
| PDF | `pdf.pdf` | Dedicated page export adapter | Available |
| Typst | `report.typ` | WASM compile + paged SVG export adapter | Available |
| Excel | `excel.xlsx` | Virtual table for large sheets | Hidden by design |

## Integration Notes

When a file URL does not expose an extension, wrap the downloaded `Blob` as a
`File` with a real name before passing it to the viewer:

```ts
async function openAuthenticatedFile(url: string) {
  const response = await fetch(url, { credentials: 'include' })
  const blob = await response.blob()
  return new File([blob], 'contract.pdf', { type: blob.type })
}
```

The operation hooks can be used to guard sensitive actions such as printing or
downloading:

```ts
const options = {
  toolbar: { download: true, print: true, exportHtml: true },
  beforeOperation(context) {
    if (context.operation === 'print') {
      return window.confirm(`Print ${context.filename}?`)
    }
    return true
  }
}
```

## Operational Guidance

1. Use URL preview for public or CORS-enabled files.
2. Use `File` preview when your application handles authentication itself.
3. Keep the viewer container height explicit, especially inside admin panels.
4. Let the built-in capability state decide whether print/export buttons are
   shown for the current file.

### Dark Theme Smoke Area

The text in this section should remain readable when the browser is in dark
mode. Inline code such as `operation-availability-change` should not fade into
the paper, and table borders should stay visible without looking heavy.

```json
{
  "viewer": "flyfish",
  "theme": "isolated document surface",
  "formats": ["pdf", "docx", "xlsx", "ofd", "typst", "geojson", "midi", "sqlite", "markdown", "code"],
  "buttons": {
    "download": true,
    "print": "dynamic",
    "exportHtml": true
  }
}
```
