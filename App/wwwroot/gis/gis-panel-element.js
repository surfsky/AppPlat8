class GisPanelElement extends HTMLElement {
    static get observedAttributes() {
        return ['title', 'info', 'content', 'width', 'height', 'closable', 'close-label'];
    }

    connectedCallback() {
        if (!this.__docPointerHandler) {
            this.__docPointerHandler = (evt) => {
                if (this.contains(evt.target)) return;
                this.closeInfoTooltip();
            };
            document.addEventListener('pointerdown', this.__docPointerHandler);
        }

        if (!this.__panelObserver) {
            this.__panelObserver = new MutationObserver(() => {
                if (this.captureLightDomContent()) {
                    this.render();
                }
            });
            this.__panelObserver.observe(this, { childList: true });
        }

        if (!this.__panelInitDone) {
            this.captureLightDomContent();
            this.__panelInitDone = true;
        }

        this.render();
    }

    disconnectedCallback() {
        if (this.__docPointerHandler) {
            document.removeEventListener('pointerdown', this.__docPointerHandler);
            this.__docPointerHandler = null;
        }
        if (this.__panelObserver) {
            this.__panelObserver.disconnect();
            this.__panelObserver = null;
        }
    }

    attributeChangedCallback() {
        if (this.__panelInitDone) {
            this.render();
        }
    }

    captureLightDomContent() {
        const candidates = Array.from(this.childNodes).filter(node => {
            if (node.nodeType === Node.TEXT_NODE) {
                return !!node.textContent && node.textContent.trim().length > 0;
            }
            if (node.nodeType !== Node.ELEMENT_NODE) {
                return false;
            }
            return !node.classList.contains('gis-panel-root');
        });

        if (candidates.length === 0) {
            return false;
        }

        const bodyParts = [];
        let actionsHtml = this.__actionsHtml || '';

        candidates.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE && node.hasAttribute('data-panel-actions')) {
                actionsHtml = node.innerHTML || '';
                node.remove();
                return;
            }

            if (node.nodeType === Node.TEXT_NODE) {
                bodyParts.push(this.escapeHtml(node.textContent || ''));
                node.remove();
                return;
            }

            bodyParts.push(node.outerHTML || '');
            node.remove();
        });

        this.__actionsHtml = actionsHtml;
        this.__bodyHtml = bodyParts.join('').trim();
        return true;
    }

    render() {
        const title = this.getAttribute('title') || '';
        const info = this.getAttribute('info') || '';
        const content = this.getAttribute('content') || '';
        const width = this.getAttribute('width') || '900px';
        const height = this.getAttribute('height') || '530px';
        const closable = this.hasAttribute('closable') && this.getAttribute('closable') !== 'false';
        const closeLabel = this.getAttribute('close-label') || '关闭';

        this.style.setProperty('--gis-panel-width', width);
        this.style.setProperty('--gis-panel-height', height);

        const escapedTitle = this.escapeHtml(title);
        const escapedInfo = this.escapeHtml(info);
        const escapedContent = this.escapeHtml(content);

        let rightInner = '';
        if (this.__actionsHtml) rightInner += this.__actionsHtml;
        if (!this.__actionsHtml && escapedInfo) {
            rightInner += `<button class="gis-panel-info" title="${escapedInfo}" aria-label="${escapedInfo}"><i class="fa-solid fa-circle-info"></i></button>`;
        }
        if (closable) {
            rightInner += `<button class="gis-panel-close" type="button" title="${this.escapeHtml(closeLabel)}" aria-label="${this.escapeHtml(closeLabel)}" data-panel-close-btn><i class="fa-solid fa-xmark"></i></button>`;
        }
        const rightPart = rightInner ? `<div class="gis-panel-actions">${rightInner}</div>` : '';

        const bodyPart = this.__bodyHtml
            ? this.__bodyHtml
            : `<div class="gis-panel-content-text">${escapedContent}</div>`;

        const tooltipPart = escapedInfo
            ? `<div class="gis-panel-tooltip" data-panel-tooltip>${escapedInfo}</div>`
            : '';

        this.innerHTML = `
            <div class="gis-panel-root">
                <img class="gis-panel-corner tl" src="/gis/tl.svg" alt="" onerror="this.style.display='none'" />
                <img class="gis-panel-corner tr" src="/gis/tr.svg" alt="" onerror="this.style.display='none'" />
                <img class="gis-panel-corner bl" src="/gis/bl.svg" alt="" onerror="this.style.display='none'" />
                <img class="gis-panel-corner br" src="/gis/br.svg" alt="" onerror="this.style.display='none'" />
                <div class="gis-panel-header">
                    <h3 class="gis-panel-title">${escapedTitle}</h3>
                    ${rightPart}
                </div>
                ${tooltipPart}
                <div class="gis-panel-body">${bodyPart}</div>
            </div>
        `;

        const closeBtn = this.querySelector('[data-panel-close-btn]');
        if (closeBtn) {
            closeBtn.addEventListener('click', () => this.requestClose());
        }

        const infoBtn = this.querySelector('.gis-panel-info');
        if (infoBtn) {
            infoBtn.addEventListener('click', (evt) => {
                evt.stopPropagation();
                this.toggleInfoTooltip();
            });
        }
    }

    toggleInfoTooltip() {
        const tooltip = this.querySelector('[data-panel-tooltip]');
        if (!tooltip) return;
        const all = document.querySelectorAll('.gis-panel-tooltip.show');
        all.forEach(node => {
            if (node !== tooltip) node.classList.remove('show');
        });
        tooltip.classList.toggle('show');
    }

    closeInfoTooltip() {
        const tooltip = this.querySelector('[data-panel-tooltip]');
        if (!tooltip) return;
        tooltip.classList.remove('show');
    }

    requestClose() {
        this.dispatchEvent(new CustomEvent('panel-close', { bubbles: true, composed: true }));
    }

    escapeHtml(text) {
        return String(text || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/\"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }
}

if (!customElements.get('gis-panel')) {
    customElements.define('gis-panel', GisPanelElement);
}
