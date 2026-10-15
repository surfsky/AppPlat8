/**
 * 按钮任务方法：为 EleButton 的同步/异步服务端操作提供统一的"锁定 + loading + 自动恢复"能力。
 * 用于方案 A（后端同步阻塞）场景：点击后立即在按钮内部显示 Element Plus 原生 loading 图标，
 * 并禁止重复点击，服务端返回后自动恢复按钮状态。
 */
export const buttonMethods = {
    /**
     * 执行按钮异步任务：期间锁定按钮并显示内嵌 loading，任务结束后自动恢复。
     * 模板内典型用法（EleButton AutoLoading="true" 会自动生成该表达式）：
     *   v-on:click="$eleManager.runButtonTask($event, function(){ return postHandler('FixData'); })"
     * @param {Event} event    触发事件，用于定位按钮 DOM 元素
     * @param {Function} taskFn 实际执行的异步任务（返回 Promise）
     * @returns {Promise<any>} 任务结果
     */
    async runButtonTask(event, taskFn) {
        const run = (typeof taskFn === 'function') ? taskFn : () => null;
        const btn = this._resolveButtonElement(event);

        // 定位不到按钮时直接执行任务，不影响原有行为
        if (!btn) return run();

        // 已在处理中：忽略本次重复点击
        if (btn.__eleButtonBusy) return;
        btn.__eleButtonBusy = true;

        // 保存原始状态以便恢复
        const prevDisabled = btn.disabled;
        const prevInner = btn.innerHTML;

        // 模拟 Element Plus 原生 loading 按钮：
        //   1) 加 is-loading 类（让按钮内非图标文字变淡）
        //   2) 禁用按钮
        //   3) 在内容前插入 el-icon-loading spinner（Element Plus 自带旋转动画）
        btn.classList.add('is-loading');
        btn.disabled = true;
        const spinner = document.createElement('span');
        spinner.className = 'el-icon is-loading';
        spinner.setAttribute('aria-hidden', 'true');
        spinner.innerHTML = '<svg viewBox="0 0 1024 1024" class="circular" style="width:1em;height:1em;vertical-align:-0.15em;fill:currentColor"><path d="M512 64a32 32 0 0 1 32 32v192a32 32 0 0 1-64 0V96a32 32 0 0 1 32-32zm0 640a32 32 0 0 1 32 32v192a32 32 0 1 1-64 0V736a32 32 0 0 1 32-32zm448-192a32 32 0 0 1-32 32H736a32 32 0 1 1 0-64h192a32 32 0 0 1 32 32zM288 512a32 32 0 0 1-32 32H64a32 32 0 1 1 0-64h192a32 32 0 0 1 32 32zm210.75-320a32 32 0 0 1 22.63 54.63l-135.76 135.76a32 32 0 1 1-45.25-45.25l135.76-135.76A32 32 0 0 1 498.75 192zM660 764a32 32 0 0 1 0 45.25L524.24 945.03a32 32 0 0 1-45.25-45.25L614.75 764A32 32 0 0 1 660 764zM320 284.75a32 32 0 0 1 45.25 0l135.76 135.76a32 32 0 1 1-45.25 45.25L320 330a32 32 0 0 1 0-45.25zM819.25 720a32 32 0 0 1 0-45.25l135.76-135.76a32 32 0 1 1 45.25 45.25L864.5 720a32 32 0 0 1-45.25 0z"></path></svg>';
        btn.insertBefore(spinner, btn.firstChild);

        try {
            return await run();
        } finally {
            // 恢复按钮原始状态
            btn.classList.remove('is-loading');
            btn.disabled = prevDisabled;
            if (spinner.parentNode === btn) btn.removeChild(spinner);
            btn.__eleButtonBusy = false;
        }
    },

    /** 从事件对象解析出按钮 DOM 元素（兼容 el-button 内部子元素被点击的情况） */
    _resolveButtonElement(event) {
        const src = event && (event.currentTarget || event.target);
        if (!src) return null;
        if (typeof src.closest !== 'function') return (src.nodeType === 1) ? src : null;
        return src.closest('button') || src.closest('.el-button') || src;
    }
};
