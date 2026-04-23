import { initPaginationState, paginationMethods } from './table/paginationMethods.js';
import { commandMethods } from './table/commandMethods.js';
import { initDrawerState, drawerMethods } from './table/drawerMethods.js';
import { messageMethods } from './table/messageMethods.js';

// Encapsulates common logic for List pages using Vue 3 + Element Plus
export class EleTable {
    constructor(options = {}) {
        this.config = options;

        // Split domains: pagination/data loading, command dispatch, drawer, message
        initPaginationState(this, Vue, options);
        initDrawerState(this, Vue);
    }

    // Dynamic options (for TreeSelect etc)
    async fetchOptions(key, url) {
        if (this.options.value[key]) return;
        try {
            const res = await axios.get(url);
            if (res.data.code === 0 || res.data.code === '0') {
                this.options.value[key] = res.data.data;
            }
        } catch (e) {
            console.error('Fetch options failed:', url, e);
        }
    }
}

Object.assign(
    EleTable.prototype,
    paginationMethods,
    commandMethods,
    drawerMethods,
    messageMethods
);
