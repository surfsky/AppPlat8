class SliderVerifier extends HTMLElement {
    static get observedAttributes() {
        return ['verify-url', 'token-selector'];
    }
 
    constructor() {
        super();
        this._shadow = this.attachShadow({ mode: 'open' });
        this._verified = false;
        this._dragging = false;
        this._verifying = false;
        this._x = 0;
        this._startX = 0;
        this._startClientY = 0;
        this._wrapHeight = 0;
        this._maxX = 0;
        this._startTime = 0;
        this._points = [];
        this._pointerId = null;
 
        this._onPointerDown = this._onPointerDown.bind(this);
        this._onPointerMove = this._onPointerMove.bind(this);
        this._onPointerUp = this._onPointerUp.bind(this);
    }
 
    connectedCallback() {
        this._render();
        this._bind();
        this._applyState();
    }
 
    disconnectedCallback() {
        this._unbind();
    }
 
    attributeChangedCallback() {
        this._applyState();
    }
 
    get verified() {
        return this._verified;
    }
 
    reset() {
        this._verified = false;
        this._verifying = false;
        this._dragging = false;
        this._x = 0;
        this._startX = 0;
        this._startTime = 0;
        this._points = [];
        this._applyState();
        this._dispatchVerifiedChange(false);
    }
 
    _render() {
        this._shadow.innerHTML = `
<style>
    :host { display:block; width:100%; }
    .wrap{
        position:relative;
        width:100%;
        height:42px;
        background:#c0c4cc;
        border-radius:4px;
        border:1px solid #dcdfe6;
        overflow:hidden;
        box-sizing:border-box;
        user-select:none;
        touch-action:pan-y;
    }
    .bg{
        position:absolute;
        inset:0 auto 0 0;
        height:100%;
        width:0px;
        background:#67c23a;
        border-radius:4px 0 0 4px;
        transition:background-color .2s ease;
    }
    .bg.fail{ background:#f56c6c; }
    .txt{
        position:absolute;
        inset:0;
        display:flex;
        align-items:center;
        justify-content:center;
        font-size:14px;
        color:#303133;
        pointer-events:none;
        white-space:nowrap;
        overflow:hidden;
        text-overflow:ellipsis;
        padding:0 12px;
        box-sizing:border-box;
    }
    .txt.ok{
        color:#fff;
        font-weight:700;
    }
    .btn{
        position:absolute;
        top:0;
        left:0;
        width:42px;
        height:40px;
        margin:1px 0 0 1px;
        background:#fff;
        border-radius:4px;
        box-shadow:0 2px 6px rgba(0,0,0,.10);
        display:flex;
        align-items:center;
        justify-content:center;
        cursor:grab;
        color:#606266;
        transition:background .15s ease, color .15s ease;
    }
    .btn:active{ cursor:grabbing; background:#f2f6fc; }
    .btn.ok{ cursor:default; color:#67c23a; }
    .icon{
        width:16px;
        height:16px;
        display:block;
    }
</style>
<div class="wrap" part="wrap">
    <div class="bg" part="bg"></div>
    <div class="txt" part="text"></div>
    <div class="btn" part="btn" role="button" aria-label="slider">
        <svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 18l6-6-6-6"></path>
        </svg>
    </div>
</div>`;
    }
 
    _bind() {
        const btn = this._getBtn();
        if (btn) btn.addEventListener('pointerdown', this._onPointerDown);
    }
 
    _unbind() {
        const btn = this._getBtn();
        if (btn) btn.removeEventListener('pointerdown', this._onPointerDown);
        document.removeEventListener('pointermove', this._onPointerMove);
        document.removeEventListener('pointerup', this._onPointerUp);
        document.removeEventListener('pointercancel', this._onPointerUp);
    }
 
    _getWrap() { return this._shadow.querySelector('.wrap'); }
    _getBg() { return this._shadow.querySelector('.bg'); }
    _getBtn() { return this._shadow.querySelector('.btn'); }
    _getText() { return this._shadow.querySelector('.txt'); }
 
    _onPointerDown(e) {
        if (this._verified || this._verifying) return;
        const wrap = this._getWrap();
        if (!wrap) return;
 
        this._dragging = true;
        this._pointerId = e.pointerId;
        this._startTime = Date.now();
        const rect = wrap.getBoundingClientRect();
        const btnWidth = 42;
        this._wrapHeight = rect.height || 0;
        this._maxX = Math.max(0, (rect.width || 0) - btnWidth);
        this._startClientY = e.clientY;
        this._points = [{ x: -1, y: 0, t: 0 }];
 
        this._startX = e.clientX - rect.left - this._x;
 
        try {
            this.setPointerCapture(this._pointerId);
        } catch {}
 
        document.addEventListener('pointermove', this._onPointerMove);
        document.addEventListener('pointerup', this._onPointerUp);
        document.addEventListener('pointercancel', this._onPointerUp);
 
        this._applyState();
    }
 
    _onPointerMove(e) {
        if (!this._dragging || this._verified || this._verifying) return;
        if (this._pointerId != null && e.pointerId !== this._pointerId) return;
        const wrap = this._getWrap();
        if (!wrap) return;
        const rect = wrap.getBoundingClientRect();
        const btnWidth = 42;
        const maxX = Math.max(0, rect.width - btnWidth);
        let x = e.clientX - rect.left - this._startX;
        if (x < 0) x = 0;
        if (x > maxX) x = maxX;
        this._x = x;
        this._maxX = maxX;
        this._wrapHeight = rect.height || this._wrapHeight;
        const nx = this._toNormX(this._x, this._maxX);
        const ny = this._toNormY(e.clientY, this._startClientY, this._wrapHeight);
        this._points.push({ x: nx, y: ny, t: Date.now() - this._startTime });
        this._applyState();
        if (x >= maxX) {
            void this._verify();
        }
    }
 
    _onPointerUp(e) {
        if (this._pointerId != null && e.pointerId !== this._pointerId) return;
        this._pointerId = null;
        document.removeEventListener('pointermove', this._onPointerMove);
        document.removeEventListener('pointerup', this._onPointerUp);
        document.removeEventListener('pointercancel', this._onPointerUp);
 
        if (this._verified || this._verifying) {
            this._dragging = false;
            this._applyState();
            return;
        }
 
        this._dragging = false;
        this._x = 0;
        this._applyState();
    }
 
    async _verify() {
        if (this._verifying || this._verified) return;
        const url = (this.getAttribute('verify-url') || '').trim();
        if (!url) return;
 
        const wrap = this._getWrap();
        const rect = wrap ? wrap.getBoundingClientRect() : { width: 0 };
        const btnWidth = 42;
        const maxX = Math.max(0, rect.width - btnWidth);
        this._x = maxX;
        this._maxX = maxX;
        this._dragging = false;
        this._verifying = true;
        this._applyState();
 
        const duration = Date.now() - this._startTime;
        const payload = { duration, points: this._points || [] };
 
        let token = '';
        const tokenSelector = (this.getAttribute('token-selector') || '').trim() || 'input[name="__RequestVerificationToken"]';
        try {
            const tokenEl = document.querySelector(tokenSelector);
            token = tokenEl ? (tokenEl.value || '') : '';
        } catch {}
 
        try {
            const res = await fetch(url, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(payload)
            });
            const body = await res.json();
            if (body && (body.code === 0 || body.code === '0')) {
                this._verified = true;
                this._verifying = false;
                this._applyState();
                this._dispatchVerifiedChange(true);
                return;
            }
            this._verifying = false;
            this._fail(body && (body.msg || body.info || body.message) ? (body.msg || body.info || body.message) : '验证失败');
        } catch (e) {
            this._verifying = false;
            this._fail('验证失败');
        }
    }
 
    _fail(message) {
        const bg = this._getBg();
        if (bg) {
            bg.classList.add('fail');
            setTimeout(() => {
                bg.classList.remove('fail');
            }, 600);
        }
        this._x = 0;
        this._verified = false;
        this._points = [];
        this._applyState();
        this.dispatchEvent(new CustomEvent('verify-failed', { detail: { message }, bubbles: true, composed: true }));
        this._dispatchVerifiedChange(false);
    }
 
    _dispatchVerifiedChange(verified) {
        this.dispatchEvent(new CustomEvent('verified-change', { detail: { verified: !!verified }, bubbles: true, composed: true }));
    }
 
    _applyState() {
        const bg = this._getBg();
        const btn = this._getBtn();
        const txt = this._getText();
        if (bg) {
            bg.style.width = `${Math.max(0, this._x)}px`;
        }
        if (btn) {
            btn.style.left = `${Math.max(0, this._x)}px`;
            btn.classList.toggle('ok', !!this._verified);
        }
        if (txt) {
            if (this._verified) {
                txt.textContent = '验证通过';
                txt.classList.add('ok');
            } else if (this._verifying) {
                txt.textContent = '验证中...';
                txt.classList.remove('ok');
            } else {
                txt.textContent = '请按住滑块，拖动到最右边';
                txt.classList.remove('ok');
            }
        }
    }

    _toNormX(x, maxX) {
        if (!maxX || maxX <= 0) return -1;
        let v = (x / maxX) * 2 - 1;
        if (v < -1) v = -1;
        if (v > 1) v = 1;
        return Number(v.toFixed(4));
    }

    _toNormY(clientY, startClientY, wrapHeight) {
        const half = (wrapHeight || 0) / 2;
        if (!half || half <= 0) return 0;
        let v = (clientY - startClientY) / half;
        if (v < -1) v = -1;
        if (v > 1) v = 1;
        return Number(v.toFixed(4));
    }
}
 
if (!customElements.get('slider-verifier')) {
    customElements.define('slider-verifier', SliderVerifier);
}
