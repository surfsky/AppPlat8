# Support Notes

## Incident Summary

An enterprise customer reported slow first-screen loading when every renderer was bundled into the application shell. The recommended setup now separates the component package from renderer presets.

## Recommended Setup

- Use `@file-viewer/vue3`, `@file-viewer/react`, `@file-viewer/web`, or another ecosystem component as the UI layer.
- Install `@file-viewer/preset-lite`, `@file-viewer/preset-office`, `@file-viewer/preset-engineering`, or `@file-viewer/preset-all`.
- Enable `fileViewerRenderers({ copyAssets: true })` in Vite.

## Follow-up

Search, zoom, lifecycle hooks, watermark options, download, print, and HTML export are still configured through the same shared options object.

