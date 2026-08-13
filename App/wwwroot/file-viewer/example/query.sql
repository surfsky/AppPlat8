with preview_files as (
  select 1 as id, 'contract.pdf' as filename, 184320 as size_bytes, 'document' as category
  union all
  select 2, 'invoice.ofd', 4278691, 'document'
  union all
  select 3, 'house.dwfx', 17340177, 'drawing'
  union all
  select 4, 'code.ts', 4096, 'source'
),
renderer_map as (
  select 'pdf' as extension, 'pdfjs-dist' as renderer
  union all select 'ofd', 'DLTech21/ofd.js'
  union all select 'dxf', '@flyfish-dev/cad-viewer'
  union all select 'dwg', '@flyfish-dev/cad-viewer'
  union all select 'dwf', '@flyfish-dev/cad-viewer'
  union all select 'dwfx', '@flyfish-dev/cad-viewer'
  union all select 'xps', '@flyfish-dev/cad-viewer'
  union all select 'ts', 'highlight.js'
)
select
  f.id,
  f.filename,
  lower(regexp_replace(f.filename, '^.*\.', '')) as extension,
  coalesce(m.renderer, 'fallback') as renderer,
  case when f.size_bytes > 1024 * 1024 then 'large' else 'normal' end as size_bucket
from preview_files f
left join renderer_map m
  on m.extension = lower(regexp_replace(f.filename, '^.*\.', ''))
where f.category in ('document', 'drawing', 'source')
order by f.id;
