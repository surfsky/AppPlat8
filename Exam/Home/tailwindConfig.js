/**
 * 主题色彩配置 - 使用CSS变量支持运行时切换
 */

// 定义所有主题
const themes = {
    blue: {
        name: '蓝色',
        primary: {
            50: '#f0f6ff',
            100: '#e6f0ff',
            200: '#cde1ff',
            300: '#b3d9ff',
            400: '#80b8ff',
            500: '#4d9eff',
            600: '#0d6efd',
            700: '#0b5ed7',
            800: '#0a58ca',
            900: '#084298',
        }
    },
    purple: {
        name: '紫色',
        primary: {
            50: '#f5f3ff',
            100: '#ede9fe',
            200: '#ddd6fe',
            300: '#c4b5fd',
            400: '#a78bfa',
            500: '#8b5cf6',
            600: '#7c3aed',
            700: '#6d28d9',
            800: '#5b21b6',
            900: '#4c1d95',
        }
    },
    dark: {
        name: '暗色',
        primary: {
            50: '#f9fafb',
            100: '#f3f4f6',
            200: '#e5e7eb',
            300: '#d1d5db',
            400: '#9ca3af',
            500: '#6b7280',
            600: '#4b5563',
            700: '#374151',
            800: '#1f2937',
            900: '#111827',
        }
    }
};


/**
 * 在页面上应用CSS变量
 * @param {string} themeName - 主题名称: 'blue', 'purple', 'dark'
 */
function applyTheme(themeName) {
    const theme = themes[themeName];
    const root = document.documentElement;
    Object.entries(theme.primary).forEach(([key, value]) => {
        root.style.setProperty(`--primary-${key}`, value);
    });

    // 保存当前主题到localStorage
    localStorage.setItem('appTheme', themeName);
    console.log('Theme applied:', themeName);
}

/**
 * 切换主题
 */
function setBlueTheme() {applyTheme('blue');}
function setPurpleTheme() {applyTheme('purple');}
function setDarkTheme() {applyTheme('dark');}

/**
 * 初始化：应用保存的主题或默认主题
 */
function initTheme() {
    const savedTheme = localStorage.getItem('appTheme') || 'blue';
    applyTheme(savedTheme);
}

// 页面加载时初始化主题
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initTheme);
} else {
    initTheme();
}


/**
 * 设置Tailwind CSS配置，使用CSS变量
 * 这样可以在运行时修改主题，只需改变CSS变量的值
 */
tailwind.config = {
    theme: {
        extend: {
            colors: {
                primary: {
                    50: 'var(--primary-50)',
                    100: 'var(--primary-100)',
                    200: 'var(--primary-200)',
                    300: 'var(--primary-300)',
                    400: 'var(--primary-400)',
                    500: 'var(--primary-500)',
                    600: 'var(--primary-600)',
                    700: 'var(--primary-700)',
                    800: 'var(--primary-800)',
                    900: 'var(--primary-900)',
                },
                // 定义次色（比如项目次色是绿色 #10b981，对应 tailwind 的 emerald-500）
                secondary: {
                    50: '#ecfdf5',
                    100: '#d1fae5',
                    500: '#10b981', // 次色核心值
                    700: '#047857',
                },
                // 可选：定义警告色、危险色等
                danger: '#ef4444',
                warning: '#f59e0b',
            }
        }
    }
}


