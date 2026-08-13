const formats = ['pdf', 'docx', 'xlsx', 'dwg', 'xmind']

export function describeInstalledPreset(presetName) {
  const normalized = String(presetName || 'lite').replace(/^preset-/, '')
  return {
    packageName: `@file-viewer/preset-${normalized}`,
    vitePlugin: '@file-viewer/vite-plugin',
    autoRenderers: true,
    formats
  }
}

export function attachViewer(element, src) {
  element.setAttribute('src', src)
  element.setAttribute('locale', 'en-US')
  element.setAttribute('theme', 'light')
}

