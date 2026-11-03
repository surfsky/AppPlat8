const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function createWorkDesk(org = false) {
    const markup = fs.readFileSync(path.join(__dirname, org ? '../Pages/Me/WorkDeskOrg.cshtml' : '../Pages/Me/WorkDesk.cshtml'), 'utf8');
    const initialKey = org ? '__WDO_INITIAL__' : '__WD_INITIAL__';
    const start = markup.indexOf('(function () {\n    var init = (window && window.' + initialKey + ')');
    assert.notEqual(start, -1);
    const end = markup.indexOf('</script>', start);
    const script = markup.slice(start, end);
    const pending = [];
    let exposed;
    let mounted;
    const card = { title: '测试', url: '/test', count: 0, iconCls: 'icon' };
    const initial = { quickEntries: [], objectCards: [card], superviseCards: [card], riskCards: [card], hazardCards: [card], userId: '5', orgId: '9' };
    const vue = {
        reactive: value => value,
        toRefs: value => Object.fromEntries(Object.keys(value).map(key => [key, { get value() { return value[key]; } }])),
        computed: fn => ({ get value() { return fn(); } }),
        onMounted: callback => { mounted = callback; },
        createApp: config => ({
            use() {}, component() {},
            mount() { exposed = config.setup(); mounted(); }
        })
    };
    const axios = {
        get(url, options) {
            if (options.params.handler === 'MyTaskObjects' || options.params.handler === 'OrgTaskObjects') {
                return Promise.resolve({ data: { code: 0, data: [] } });
            }
            return new Promise((resolve, reject) => pending.push({ params: options.params, resolve, reject }));
        }
    };
    const document = { readyState: 'complete', getElementById: () => ({}) };
    const window = { [initialKey]: initial, ElementPlusLocaleZhCn: null, ElementPlusIconsVue: null };
    vm.runInNewContext(script, {
        window, document, Vue: vue, ElementPlus: {}, axios, URLSearchParams,
        location: org
            ? { pathname: '/Me/WorkDeskOrg', search: '?orgName=%E7%A7%91%E5%AE%A4' }
            : { pathname: '/Me/WorkDesk', search: '?userId=5&orgId=9' },
        console, setTimeout
    });
    return { exposed, pending };
}

test('先显示卡片，按组加载统计并保留查询范围', async () => {
    const { exposed, pending } = createWorkDesk();
    assert.equal(pending.length, 4);
    assert.equal(exposed.objectCards.value[0].url, '/test');
    assert.equal(exposed.statsStatus.value.objects, 'loading');
    const objects = pending.find(item => item.params.get('group') === 'objects');
    assert.equal(objects.params.get('userId'), '5');
    assert.equal(objects.params.get('orgId'), '9');
    assert.equal(objects.params.get('handler'), 'Stats');
    objects.resolve({ data: { code: 0, data: [{ title: '测试', url: '/test', count: 42 }] } });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(exposed.statsStatus.value.objects, 'ready');
    assert.equal(exposed.objectCards.value[0].count, 42);
    assert.equal(exposed.statsStatus.value.risk, 'loading');
});

test('统计失败可单独重试，不显示为零', async () => {
    const { exposed, pending } = createWorkDesk();
    pending.find(item => item.params.get('group') === 'hazards').reject(new Error('测试失败'));
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(exposed.statsStatus.value.hazards, 'error');
    const retry = exposed.loadStats('hazards');
    assert.equal(exposed.statsStatus.value.hazards, 'loading');
    pending.at(-1).resolve({ data: { code: 0, data: [{ title: '隐患', url: '/hazards', count: 0 }] } });
    await retry;
    assert.equal(exposed.statsStatus.value.hazards, 'ready');
    assert.equal(exposed.hazardCards.value[0].count, 0);
});

test('科室工作台先展示卡片，并使用首屏解析的组织加载统计', async () => {
    const { exposed, pending } = createWorkDesk(true);
    assert.equal(pending.length, 4);
    assert.equal(exposed.objectGroups.value[0].cards[0].url, '/test');
    assert.equal(exposed.statsStatus.value.objects, 'loading');
    const objects = pending.find(item => item.params.get('group') === 'objects');
    assert.equal(objects.params.get('orgId'), '9');
    assert.equal(objects.params.get('orgName'), '科室');
    assert.equal(objects.params.get('handler'), 'Stats');
    objects.resolve({ data: { code: 0, data: [{ title: '科室对象', url: '/test', count: 42 }] } });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(exposed.statsStatus.value.objects, 'ready');
    assert.equal(exposed.objectGroups.value[0].cards[0].count, 42);
    assert.equal(exposed.statsStatus.value.risk, 'loading');
});

test('科室统计失败可单独重试', async () => {
    const { exposed, pending } = createWorkDesk(true);
    pending.find(item => item.params.get('group') === 'hazards').reject(new Error('测试失败'));
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(exposed.statsStatus.value.hazards, 'error');
    const retry = exposed.loadStats('hazards');
    assert.equal(exposed.statsStatus.value.hazards, 'loading');
    pending.at(-1).resolve({ data: { code: 0, data: [{ title: '科室隐患', url: '/hazards', count: 0 }] } });
    await retry;
    assert.equal(exposed.statsStatus.value.hazards, 'ready');
    assert.equal(exposed.hazardCards.value[0].count, 0);
});