// 配置 Tailwind 主题（仅保留基础颜色）
tailwind.config = {
    darkMode: 'class',
    theme: {
    extend: {
        colors: {
        primary: 'var(--color-primary)',
        secondary: 'var(--color-secondary)',
        bg: 'var(--color-bg)',
        text: 'var(--color-text)',
        card: 'var(--color-card)',
        },
    },
    }
}

/**
 * 主题管理器类
 * 负责处理主题切换、存储和初始化
 */
class ThemeManager {
    constructor() {
    // 1. 定义所有主题的颜色变量
    this.themeConfigs = {
        // 基础模式（亮色/暗色）
        light: {
        '--color-primary': '#3b82f6',
        '--color-secondary': '#8b5cf6',
        '--color-bg': '#ffffff',
        '--color-text': '#1f2937',
        '--color-card': '#f3f4f6',
        '--color-switch-off': '#e5e7eb'
        },
        dark: {
        '--color-primary': '#60a5fa',
        '--color-secondary': '#a78bfa',
        '--color-bg': '#1f2937',
        '--color-text': '#f9fafb',
        '--color-card': '#374151',
        '--color-switch-off': '#4b5563'
        },
        // 颜色主题（覆盖主色和次色）
        blue: {
        '--color-primary': '#3b82f6',
        '--color-secondary': '#8b5cf6'
        },
        purple: {
        '--color-primary': '#8b5cf6',
        '--color-secondary': '#ec4899'
        },
        green: {
        '--color-primary': '#10b981',
        '--color-secondary': '#f59e0b'
        }
    };

    // 2. 本地存储key
    this.storageKeys = {
        darkMode: 'darkMode',
        colorTheme: 'colorTheme'
    };

    // 初始化主题（仅设置变量，不处理控件）
    this.initTheme();
    }

    // 私有方法：设置CSS变量
    #setCssVariables(variables) {
    const root = document.documentElement;
    Object.entries(variables).forEach(([key, value]) => {
        root.style.setProperty(key, value);
    });
    }

    // 新增私有方法：设置body样式
    #setBodyStyles() {
    const body = document.body;
    // 从CSS变量中获取当前的背景色和文本色
    const bgColor = getComputedStyle(document.documentElement).getPropertyValue('--color-bg');
    const textColor = getComputedStyle(document.documentElement).getPropertyValue('--color-text');
    
    // 动态设置body的背景色和文本色
    body.style.backgroundColor = bgColor;
    body.style.color = textColor;
    }

    // 核心方法1：设置颜色主题
    setTheme(themeName) {
    // 验证主题名称是否合法
    if (!['blue', 'purple', 'green'].includes(themeName)) {
        console.warn(`无效的主题名称: ${themeName}，默认使用blue`);
        themeName = 'blue';
    }

    // 1. 获取当前暗色模式状态
    const isDark = this.getDarkMode();
    
    // 2. 先设置基础模式变量（亮色/暗色）
    this.#setCssVariables(this.themeConfigs[isDark ? 'dark' : 'light']);
    
    // 3. 覆盖颜色主题变量
    this.#setCssVariables(this.themeConfigs[themeName]);
    
    // 4. 同步设置body样式（新增）
    this.#setBodyStyles();
    
    // 5. 保存到本地存储
    localStorage.setItem(this.storageKeys.colorTheme, themeName);
    }

    // 核心方法2：获取当前颜色主题名称
    getThemeName() {
    const themeName = localStorage.getItem(this.storageKeys.colorTheme);
    // 验证有效性，无效则返回默认值
    return ['blue', 'purple', 'green'].includes(themeName) ? themeName : 'blue';
    }

    // 核心方法3：设置暗色模式（修复bug：立即更新变量，不依赖setTheme）
    setDarkMode(isDark) {
    // 1. 更新HTML类名
    document.documentElement.classList.remove('light', 'dark');
    document.documentElement.classList.add(isDark ? 'dark' : 'light');
    
    // 2. 直接设置当前模式的基础变量（修复不立即生效的bug）
    this.#setCssVariables(this.themeConfigs[isDark ? 'dark' : 'light']);
    
    // 3. 再应用颜色主题（保持主题一致性）
    this.#setCssVariables(this.themeConfigs[this.getThemeName()]);
    
    // 4. 同步设置body样式（新增）
    this.#setBodyStyles();
    
    // 5. 保存到本地存储
    localStorage.setItem(this.storageKeys.darkMode, isDark);
    }

    // 核心方法4：获取当前暗色模式状态
    getDarkMode() {
    return localStorage.getItem(this.storageKeys.darkMode) === 'true';
    }

    // 初始化主题（仅设置变量，不处理控件）
    initTheme() {
    this.setDarkMode(this.getDarkMode());
    this.setTheme(this.getThemeName());
    }
}

// 初始化主题管理器
window.themeManager = new ThemeManager();