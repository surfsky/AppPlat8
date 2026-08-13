#set page(paper: "a4", margin: (x: 18mm, y: 16mm))
#set text(size: 10pt)
#set heading(numbering: "1.")

#align(center)[
  #text(size: 22pt, weight: "bold")[File Viewer Typst Report]
  #linebreak()
  #text(fill: rgb("#64748b"))[Native Typst preview rendered in the browser]
]

#v(10pt)

#grid(
  columns: (1fr, 1fr, 1fr),
  gutter: 10pt,
  box(fill: rgb("#f0fdf4"), stroke: rgb("#bbf7d0"), inset: 10pt, radius: 5pt)[
    #text(weight: "bold", fill: rgb("#166534"))[Async Engine]
    #linebreak()
    WASM compiler and renderer are loaded only for `.typ` documents.
  ],
  box(fill: rgb("#eff6ff"), stroke: rgb("#bfdbfe"), inset: 10pt, radius: 5pt)[
    #text(weight: "bold", fill: rgb("#1d4ed8"))[Paged Output]
    #linebreak()
    Page size metadata is preserved for preview, print, and HTML export.
  ],
  box(fill: rgb("#fff7ed"), stroke: rgb("#fed7aa"), inset: 10pt, radius: 5pt)[
    #text(weight: "bold", fill: rgb("#9a3412"))[Safe SVG]
    #linebreak()
    Generated SVG keeps scripts disabled in the viewer integration.
  ],
)

= Why Typst

Typst is a modern markup-based typesetting system. It is expressive like a
document language, but fast enough for interactive browser previews when paired
with a WASM renderer.

== Supported preview path

#table(
  columns: (1.1fr, 1.5fr, 1.4fr, 1.3fr),
  inset: 7pt,
  stroke: 0.5pt + rgb("#d8dee8"),
  fill: (x, y) => if y == 0 { rgb("#e8f3ff") } else if calc.rem(y, 2) == 0 { rgb("#f8fafc") },
  [Format], [Renderer], [Loading], [Operations],
  [`.typ`], [`typst.ts` WASM], [Async vendor chunk], [Preview / print / HTML],
  [`.typst`], [Compatibility alias], [Same render path], [Preview / print / HTML],
  [Packages], [Typst registry], [Network-bound when used], [Best with pinned templates],
)

== Formula and code

The renderer keeps mathematical layout and text positioning in SVG:

$ integral_0^1 x^2 dif x = 1/3 quad and quad E = m c^2 $

```ts
import FileViewer from '@file-viewer/vue3'

const options = {
  toolbar: { print: true, exportHtml: true },
  watermark: { enabled: true, text: 'Internal Preview' }
}
```

#pagebreak()

= Browser integration checklist

#enum(
  [Select `report.typ` from the grouped demo file picker.],
  [The Typst renderer compiles the source to SVG in a dedicated async chunk.],
  [Each page is displayed on a white document surface over a neutral canvas.],
  [Printing and rendered HTML export use the document page dimensions.],
)

#v(8pt)

#quote(block: true)[
  The demo intentionally includes headings, tables, math, code, and multiple
  pages so smoke tests can catch empty output, clipped pages, and broken print
  adapters.
]

#rect(width: 100%, height: 58pt, fill: rgb("#ecfdf5"), stroke: rgb("#86efac"), radius: 6pt)[
  #align(center + horizon)[#text(weight: "bold", fill: rgb("#166534"))[
    File Viewer renders Typst documents without server-side conversion.
  ]]
]
