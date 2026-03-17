const MenuTree = {
    name: 'menu-tree', // Required for recursive self-reference
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
            <!-- Group with Submenu (Recursive) -->
            <div v-if="item.children && item.children.length > 0" class="flex flex-col gap-0">
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
                
                <!-- Recursive Children Container -->
                <div class="nav-sub-items flex flex-col overflow-hidden" 
                     :class="{ 'collapsed': !item.isOpen }">
                    <!-- Recursive Call -->
                    <menu-tree 
                        class="ml-3 border-l border-white border-opacity-10"
                        :menu-data="item.children" 
                        :default-target="defaultTarget" 
                        :text-color="textColor"
                        :active-key="activeKey"
                        @navigate="handleNavigate"
                    ></menu-tree>
                </div>
            </div>
            
            <!-- Single Item (Leaf) -->
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
            if (props.defaultTarget && document.getElementById(props.defaultTarget)) {
                 if (item.url) {
                     const iframe = document.getElementById(props.defaultTarget);
                     iframe.src = item.url;
                 }
            }
            
            // Emit event for parent
            emit('navigate', item);
        };

        // Handle recursive navigate events
        const handleNavigate = (item) => {
            emit('navigate', item);
        }

        return {
            toggleGroup,
            handleItemClick,
            handleNavigate
        };
    }
};
