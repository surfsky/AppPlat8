import{o as e}from"./chunk-CMxvf4Kt.js";import{t}from"./preload-helper-CwQZjUKM.js";import{Et as n,Ft as r,Ot as i,Rt as a,an as o,ct as s,ft as c,ht as l,in as u,mt as d,o as f,pt as p,un as m}from"./_plugin-vue_export-helper-Bbe_HLAV.js";import{t as h}from"./jszip.min-DWCzc6bk.js";var g=e(h(),1),_={width:794,height:1123},v=new Set([`file:`,`about:`,`data:`]),y=.24,b=3,x=.15,S=`0.3.26`,C=20555,w=`http://schemas.openxmlformats.org/wordprocessingml/2006/main`,T=`http://schemas.openxmlformats.org/officeDocument/2006/relationships`,E=`http://schemas.openxmlformats.org/package/2006/relationships`,D=`urn:schemas-microsoft-com:vml`,O=`word/document.xml`,k=`word/_rels/document.xml.rels`,A=`docx-page-background`,ee={bmp:`image/bmp`,gif:`image/gif`,jpeg:`image/jpeg`,jpg:`image/jpeg`,png:`image/png`,svg:`image/svg+xml`,tif:`image/tiff`,tiff:`image/tiff`,webp:`image/webp`},te=e=>{let t=typeof e.renderAsync==`function`?e:e.default;if(!t||typeof t.renderAsync!=`function`)throw TypeError(`@file-viewer/docx did not expose a compatible renderAsync function.`);return t},j=(()=>{let e={module:null,async load(){return this.module||=t(()=>import(`./docx-preview-BV83ExEj.js`),[],import.meta.url),this.module}};return async()=>te(await e.load())})(),M=e=>e instanceof Error?/(?:undefined|null).*children|children.*(?:undefined|null)/i.test(e.message)&&/renderHeaderFooter/i.test(e.stack||``):!1,N=async(e,t,n,r)=>{try{return await e(t,n,void 0,r),!1}catch(i){if(!M(i))throw i;return n.innerHTML=``,await e(t,n,void 0,{...r,renderHeaders:!1,renderFooters:!1}),!0}},P=(e,t)=>{if((e.byteLength>=4?new DataView(e).getUint16(0,!1):0)!==C)throw Error(s(t?.options)(`word.error.invalidDocx`))},F=e=>e.ownerDocument.defaultView,ne=e=>new((F(e))?.DOMParser??globalThis.DOMParser),re=e=>{let t=[e.ownerDocument.URL,F(e)?.location?.href,globalThis.location?.href].filter(Boolean);for(let e of t)try{return new URL(e).protocol}catch{}return``},I=(e,t,n)=>{let r=Array.from(e.getElementsByTagNameNS(t,n));return r.length?r:Array.from(e.getElementsByTagName(`*`)).filter(e=>e.localName===n)},L=(e,t)=>{let n=t.parseFromString(e,`application/xml`);return I(n,`http://www.mozilla.org/newlayout/xml/parsererror.xml`,`parsererror`).length?null:n},R=(e,t)=>{let n=t.startsWith(`/`)?[]:e.split(`/`).slice(0,-1);return t.replace(/^\/+/,``).split(`/`).forEach(e=>{if(!(!e||e===`.`)){if(e===`..`){n.pop();return}n.push(e)}}),n.join(`/`)},z=e=>ee[e.split(`.`).pop()?.toLowerCase()||``],B=async(e,t=()=>new DOMParser)=>{try{let n=await g.default.loadAsync(e),r=n.file(O),i=n.file(k);if(!r||!i)return;let a=t(),o=L(await r.async(`string`),a),s=L(await i.async(`string`),a);if(!o||!s)return;let c=I(o,w,`background`)[0],l=c&&I(c,D,`fill`)[0],u=l?.getAttributeNS(T,`id`)||l?.getAttribute(`r:id`);if(!u)return;let d=I(s,E,`Relationship`).find(e=>e.getAttribute(`Id`)===u),f=d?.getAttribute(`Target`);if(!f||d?.getAttribute(`TargetMode`)===`External`)return;let p=R(O,f),m=z(p),h=n.file(p)||n.file(decodeURIComponent(p));return!m||!h?void 0:`data:${m};base64,${await h.async(`base64`)}`}catch{return}},V=(e,t)=>{if(!t)return 0;let n=0;return e.querySelectorAll(`section.docx`).forEach(r=>{let i=Array.from(r.children).find(e=>e.classList.contains(A)),a=i||e.ownerDocument.createElement(`div`);a.className=A,a.setAttribute(`aria-hidden`,`true`),a.style.backgroundImage=`url("${t}")`,i||r.prepend(a),n+=1}),n},H=(e,t)=>t?.worker===!1?!1:t?.worker===!0?!0:!!((F(e)?.Worker??globalThis.Worker)&&t?.workerUrl&&!v.has(re(e))),U=e=>{let t=F(e)?.matchMedia??globalThis.matchMedia;return typeof t==`function`&&t(`(prefers-color-scheme: dark)`).matches},W=(e,t,n)=>{if(n?.darkMode!==void 0)return n.darkMode;let r=f(t?.options?.theme);return r===`dark`?!0:r===`light`?!1:U(e)},G=(e,t)=>!e||t?e:`${e}${e.includes(`?`)?`&`:`?`}file-viewer-docx=${S}`,K=(e,t)=>{if(t===`allow`)return 0;let n=0;return e.querySelectorAll(`a[href]`).forEach(e=>{let t=e.getAttribute(`href`);!t||t.startsWith(`#`)||(e.hasAttribute(`data-docx-external-href`)||e.setAttribute(`data-docx-external-href`,t),e.removeAttribute(`href`),e.setAttribute(`aria-disabled`,`true`),n+=1)}),n},q=(e,t,n)=>{let r=t?.options?.docx,i=m(e.ownerDocument),a=H(e,r),s=r?.visualPagination===!0,c=W(e,t,r),l=e=>{(e.phase===`render`||e.phase===`layout`||e.phase===`done`)&&n()},d=r?.externalLinkPolicy??`block`,f={useWorker:a,breakPages:s,ignoreLastRenderedPageBreak:r?.ignoreLastRenderedPageBreak??!s,externalLinkPolicy:d,darkMode:c,progress:t=>{(t.phase===`render`||t.phase===`layout`||t.phase===`done`)&&K(e,d),l(t)}};return a&&(f.workerUrl=G(o(r,i),!!r?.workerUrl),f.workerJsZipUrl=G(u(r,i),!!r?.workerJsZipUrl)),r?.workerTimeout!==void 0&&(f.workerTimeout=r.workerTimeout),r?.renderPageBatchSize===void 0?r?.progressive===!1&&(f.renderPageBatchSize=2**53-1):f.renderPageBatchSize=r.renderPageBatchSize,r?.renderYieldEveryMs!==void 0&&(f.renderYieldEveryMs=r.renderYieldEveryMs),r?.strictWordCompatibility!==void 0&&(f.strictWordCompatibility=r.strictWordCompatibility),r?.paginationTolerance!==void 0&&(f.paginationTolerance=r.paginationTolerance),r?.maxDynamicPaginationPasses!==void 0&&(f.maxDynamicPaginationPasses=r.maxDynamicPaginationPasses),r?.awaitLayout!==void 0&&(f.awaitLayout=r.awaitLayout),r?.preserveComplexFieldResults!==void 0&&(f.preserveComplexFieldResults=r.preserveComplexFieldResults),r?.updatePageReferences!==void 0&&(f.updatePageReferences=r.updatePageReferences),r?.hideWebHiddenContent!==void 0&&(f.hideWebHiddenContent=r.hideWebHiddenContent),f},J=(e,t)=>{let n=F(t)?.HTMLElement;return n?e instanceof n:e instanceof HTMLElement},Y=`
.docx-fit-viewer {
  box-sizing: border-box;
  height: 100%;
  overflow: auto;
  background: var(--file-viewer-render-surface-background, #ececec);
  color-scheme: light;
}
.docx-fit-viewer[data-docx-dark-mode='true'] {
  background: var(--file-viewer-render-surface-background, #242424);
  color-scheme: dark;
}
.docx-fit-viewer .docx-wrapper {
  box-sizing: border-box;
  min-width: 0 !important;
  width: 100% !important;
  padding: 24px 14px 40px !important;
  background: var(--file-viewer-render-surface-background, #e7e9ec) !important;
}
.docx-fit-viewer[data-docx-dark-mode='true'] .docx-wrapper {
  background: var(--file-viewer-render-surface-background, #242424) !important;
}
.docx-fit-viewer .docx-page-frame {
  position: relative;
  width: 100%;
  min-width: 0;
  margin: 0 auto 24px;
  overflow: visible;
}
.docx-fit-viewer .docx-flow-frame {
  position: relative;
  width: 100%;
  min-width: 0;
  margin: 0 auto 28px;
  overflow: visible;
}
.docx-fit-viewer .docx-page-frame > section.docx,
.docx-fit-viewer .docx-flow-frame > section.docx {
  position: absolute;
  top: 0;
  left: 50%;
  margin: 0 !important;
  background: #ffffff !important;
  box-shadow: 0 2px 14px rgba(25, 35, 48, 0.18);
  box-sizing: border-box;
  overflow: hidden;
  transform-origin: top center;
}
.docx-fit-viewer .docx-page-background {
  position: absolute;
  inset: 0;
  z-index: 0;
  pointer-events: none;
  background-position: center;
  background-repeat: no-repeat;
  background-size: 100% 100%;
}
.docx-fit-viewer[data-docx-dark-mode='true'] .docx-page-frame > section.docx,
.docx-fit-viewer[data-docx-dark-mode='true'] .docx-flow-frame > section.docx {
  background: rgb(51, 51, 51) !important;
  box-shadow: 0 0 10px rgba(0, 0, 0, 0.8);
  outline: 1px solid rgba(255, 255, 255, 0.15);
  outline-offset: -1px;
}
.docx-fit-viewer .docx-flow-frame > section.docx {
  height: auto !important;
  min-height: var(--docx-page-height, auto) !important;
  overflow: visible !important;
}
.docx-fit-viewer .docx-page-frame > section.docx > article,
.docx-fit-viewer .docx-flow-frame > section.docx > article {
  position: relative;
  z-index: 1;
}
`;function ie(e){let t=e.ownerDocument.createElement(`style`);return t.textContent=Y,e.prepend(t),t}function ae(e,t){let n=e.querySelector(`.docx-wrapper`);return n?Array.from(n.children).flatMap(n=>{if(!J(n,e)||!n.matches(`section.docx`))return[];let r=e.ownerDocument.createElement(`div`);return r.className=t?`docx-page-frame`:`docx-flow-frame`,n.before(r),r.appendChild(n),[r]}):[]}function oe(e,t){e.classList.add(`docx-fit-viewer`);let o=ie(e),s=t?.options?.docx?.visualPagination===!0,c=ae(e,s),u=F(e),d=u?.ResizeObserver,f=0,p=1,m=1,h=1,g=i(),v=e=>Math.min(b,Math.max(y,Number(e.toFixed(2)))),S=()=>{let t=m,n=h,r=!1;c.forEach(i=>{let a=i.firstElementChild;if(!J(a,e))return;a.style.transform=`translateX(-50%)`;let o=a.offsetWidth,c=s?a.offsetHeight:Math.max(a.scrollHeight,a.offsetHeight);if(!o||!c)return;let l=Math.max(e.clientWidth-28,120),u=Math.min(1,Math.max(y,l/o)),d=v(u*p);r||=(t=d,n=u,!0),a.style.transform=`translateX(-50%) scale(${d})`,i.style.width=`${Math.ceil(Math.max(o*d,e.clientWidth-28,120))}px`,i.style.maxWidth=`none`,i.style.height=`${Math.ceil(c*d)}px`}),r&&(m=t,h=n,g.emit())},C=()=>{if(!u){S();return}u.cancelAnimationFrame(f),f=u.requestAnimationFrame(()=>{S()})},w=()=>({scale:m,label:`${Math.round(m*100)}%`,canZoomIn:m<b,canZoomOut:m>y,canReset:p!==1,minScale:y,maxScale:b}),T=e=>(p=Math.min(6,Math.max(.2,Number(e.toFixed(2)))),u?.cancelAnimationFrame(f),S(),w()),E=e=>T(e/Math.max(h,.01)),D=()=>{for(let e of c){let t=X(e);if(!t)continue;let n=l(t,_);return{width:t.offsetWidth||n.width||_.width,height:Z(e)?_.height:t.offsetHeight||n.height||_.height}}return null},O=t=>{let r=D();if(!r)return{applied:!1,mode:t.mode,resize:t.resize,source:t.source,reason:`unmeasurable`,provider:`zoom`};let i=n({mode:t.mode===`auto`?`width`:t.mode,viewportWidth:Math.max(1,t.viewportWidth||e.clientWidth||0),viewportHeight:Math.max(1,t.viewportHeight||e.clientHeight||0),contentWidth:r.width,contentHeight:r.height,currentScale:m,minScale:t.minScale??y,maxScale:t.maxScale??b});if(!i)return{applied:!1,mode:t.mode,resize:t.resize,source:t.source,reason:`unmeasurable`,provider:`zoom`};let a=E(i);return{applied:!0,mode:t.mode,resize:t.resize,scale:a.scale,source:t.source,provider:`zoom`}};e.dataset.viewerZoomProvider=`docx`,r(e,{zoomIn:()=>T((m+x)/Math.max(h,.01)),zoomOut:()=>T((m-x)/Math.max(h,.01)),resetZoom:()=>T(1),setZoom:E,fit:O,getState:w,subscribe:g.subscribe});let k=d?new d(C):null;return k?.observe(e),c.forEach(e=>{let t=X(e);t&&k?.observe(t)}),S(),()=>{u?.cancelAnimationFrame(f),k?.disconnect(),a(e),o.remove(),e.classList.remove(`docx-fit-viewer`)}}function X(e){let t=e.firstElementChild,n=e.ownerDocument.defaultView?.HTMLElement;return n&&t instanceof n?t:null}function Z(e){return!!e?.classList.contains(`docx-flow-frame`)}function Q(e){let t=e?X(e):null;if(!t)return _;let n=l(t,_);return Z(e)?{width:n.width,height:Math.max(t.scrollHeight||0,t.offsetHeight||0,_.height)}:n}function $(e,t){let n=Z(e),r=d(t.width),i=d(t.height);c(e,t,{heightMode:n?`min`:`fixed`}),e.style.margin=`0 auto 18px`;let a=X(e);a&&(a.style.position=`relative`,a.style.top=`auto`,a.style.left=`auto`,a.style.width=r,a.style.maxWidth=`none`,a.style.minHeight=n?`0`:i,a.style.height=n?`auto`:i,a.style.margin=`0 auto`,a.style.transform=`none`,a.style.transformOrigin=`top left`,a.style.overflow=n?`visible`:`hidden`,a.style.boxShadow=`none`)}function se(e){let t=e.querySelector(`.docx-page-frame, .docx-flow-frame`),n=Q(t||void 0);return p({selector:t?.classList.contains(`docx-flow-frame`)?`.viewer-export-content .docx-flow-frame`:`.viewer-export-content .docx-page-frame`,width:n.width,height:t?.classList.contains(`docx-flow-frame`)?_.height:n.height,heightMode:t?.classList.contains(`docx-flow-frame`)?`min`:`fixed`})}function ce(e){let t=Array.from(e.querySelectorAll(`.docx-page-frame, .docx-flow-frame`)),n=e.cloneNode(!0),r=e.ownerDocument.createElement(`div`);r.className=`docx-print-document`;let i=Array.from(n.querySelectorAll(`style`)).filter(e=>!e.textContent?.includes(`.docx-fit-viewer`)).map(e=>e.outerHTML).join(``);return n.querySelectorAll(`.docx-page-frame, .docx-flow-frame`).forEach((e,n)=>{e.dataset.viewerPrintPageIndex=String(n),$(e,Q(t[n])),r.appendChild(e.cloneNode(!0))}),r.childElementCount?`${i}${r.outerHTML}`:n.innerHTML}async function le(e,t,n){P(e,n),t.innerHTML=``;let r=!1,i=()=>{r||(r=!0,n?.onProgressiveRender?.())},a=q(t,n,i),[{defaultOptions:o,renderAsync:s},c]=await Promise.all([j(),B(e,()=>ne(t))]);t.dataset.docxWorker=a.useWorker?`self`:`false`,t.dataset.docxDarkMode=a.darkMode?`true`:`false`;let l=await N(s,e,t,{...o,...a});K(t,a.externalLinkPolicy),t.dataset.docxHeaderFooterFallback=l?`true`:`false`,t.dataset.docxPageBackground=V(t,c)>0?`true`:`false`,i();let u=oe(t,n);return n?.registerExportAdapter?.({includeDocumentStyles:!1,getPrintMaskPages:()=>Array.from(t.querySelectorAll(`.docx-page-frame, .docx-flow-frame`)),beforeSnapshot:()=>{let e=F(t);e&&e.dispatchEvent(new e.Event(`resize`))},printStyle:()=>se(t),toHtml:()=>ce(t)}),n?.registerThumbnailAdapter?.({getTarget:()=>t.querySelector(`.docx-page-frame, .docx-flow-frame`)||t}),{$el:t,unmount(){n?.registerExportAdapter?.(null),n?.registerThumbnailAdapter?.(null),u(),delete t.dataset.docxWorker,delete t.dataset.docxDarkMode,delete t.dataset.docxHeaderFooterFallback,delete t.dataset.docxPageBackground,t.innerHTML=``}}}export{le as default};