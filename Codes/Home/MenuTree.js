const MenuTree = {
    props: {
        menuData: {
            type: Array,
            required: true
        },
        defaultTarget: {
            type: String,
            default: ''
        },
        textColor: {
            type: String,
            default: '#cbd5e1' // slate-300
        },
        activeKey: {
            type: String,
            default: ''
        }
    },
    emits: ['navigate'],
    template: `
    <div class="flex flex-col gap-1">
        <template v-for="item in menuData" :key="item.key">
            <!-- Group with Submenu -->
            <div v-if="item.children" class="flex flex-col gap-0">
                <div class="nav-item-parent flex items-center justify-between h-10 px-2 py-2 rounded-md cursor-pointer hover:bg-white hover:bg-opacity-10"
                     :style="{ color: textColor }"
                     @click="toggleGroup(item)">
                    <div class="flex items-center gap-3">
                        <i :class="[item.icon, 'w-5 flex-shrink-0']"></i>
                        <span class="text-sm truncate">{{ item.text }}</span>
                    </div>
                    <i class="fas fa-chevron-right w-4 transition-transform duration-300" 
                       :style="{ transform: item.isOpen ? 'rotate(90deg)' : 'rotate(0deg)' }"></i>
                </div>
                <div class="nav-sub-items flex flex-col" :class="{ 'collapsed': !item.isOpen }">
                    <div v-for="sub in item.children" :key="sub.key" 
                         class="nav-item flex items-center gap-2 h-8 px-2 py-1 rounded-md cursor-pointer text-xs ml-3 hover:bg-white hover:bg-opacity-10"
                         :class="{ 'active': activeKey === sub.key }"
                         :style="{ color: textColor }"
                         @click="handleItemClick(sub)">
                        <i :class="[sub.icon || 'fas fa-arrow-right', 'w-3 flex-shrink-0']"></i>
                        <span class="truncate">{{ sub.text }}</span>
                    </div>
                </div>
            </div>
            <!-- Single Item -->
            <div v-else 
                 class="nav-item flex items-center gap-3 h-10 px-2 py-2 rounded-md cursor-pointer hover:bg-white hover:bg-opacity-10"
                 :class="{ 'active': activeKey === item.key }"
                 :style="{ color: textColor }"
                 @click="handleItemClick(item)">
                <i :class="[item.icon, 'w-5 flex-shrink-0']"></i>
                <span class="text-sm truncate">{{ item.text }}</span>
            </div>
        </template>
    </div>
    `,
    setup(props, { emit }) {
        const toggleGroup = (item) => {
            item.isOpen = !item.isOpen;
        };

        const handleItemClick = (item) => {
            // Determine target
            const target = item.target || props.defaultTarget;
            
            // Handle external/top navigation
            if (target === '_blank') {
                if (item.url) window.open(item.url, '_blank');
                return;
            }
            if (target === '_top') {
                if (item.url) window.top.location.href = item.url;
                return;
            }

            // Handle iframe navigation
            // If defaultTarget is provided and it's an ID, try to set src
            if (props.defaultTarget && document.getElementById(props.defaultTarget)) {
                 if (item.url) {
                     const iframe = document.getElementById(props.defaultTarget);
                     // Check if src is different to avoid reload? Or just set it.
                     // User intention seems to be navigation, so setting it is correct.
                     iframe.src = item.url;
                 }
            }
            
            // Emit event for parent to handle history/title updates
            emit('navigate', item);
        };

        return {
            toggleGroup,
            handleItemClick
        };
    }
};
