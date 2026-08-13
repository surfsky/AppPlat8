# File Viewer Markdown Sample

This Markdown file is designed to exercise the lightweight document renderer with common content used in product documentation.

## Capability Checklist

- Headings and paragraphs remain readable in light and dark shells.
- Tables keep borders, alignment, and inline code contrast.
- Fenced code blocks are highlighted only when the code renderer is loaded.
- Links, quotes, lists, and task items share the same search and export pipeline.

| Area | Expected behavior | Renderer |
| --- | --- | --- |
| Documentation | Prose, links, tables, quotes | Markdown |
| Source snippets | Fenced code blocks | highlight.js on demand |
| Operations | Search, zoom, print, export | Core APIs |

> The demo uses local files only, so it is suitable for intranet and offline deployment checks.

```ts
import { mountViewer } from '@file-viewer/web'

mountViewer('#viewer', {
  src: '/files/contract.pdf',
  locale: 'en-US',
  theme: 'light'
})
```

## Release Notes

1. Install the component package for your framework.
2. Add one preset package for the required file range.
3. Let `@file-viewer/vite-plugin` discover the installed preset and copy assets.
