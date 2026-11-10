const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = path.resolve(__dirname, '../../src/Emby.AudioMemory');
function setup(failLoad = false) {
    let Controller, saved, calls = 0;
    const elements = new Map();
    const element = id => {
        if (!elements.has(id)) elements.set(id, {value:'', checked:false, disabled:false, textContent:'', addEventListener(){}, querySelector:element});
        return elements.get(id);
    };
    const api = {
        getPluginConfiguration: async () => { if(failLoad) throw Error('load'); return {Enable:true, DebugLogging:false, AllowedUserIds:['test-user'], UnknownSetting:42}; },
        getUrl: x => x,
        getJSON: async () => ({Preferences:[], Error:null}),
        updatePluginConfiguration: async (id, config) => {calls++; saved = config;}
    };
    vm.runInNewContext(fs.readFileSync(path.join(source, 'Configuration.js'),'utf8'), {
        ApiClient:api,
        define(deps, factory){ Controller = factory(class {constructor(view){this.view=view;} onResume(){} }); }
    });
    return {controller:new Controller({querySelector:element},{}),element,api,get saved(){return saved;},get calls(){return calls;}};
}
test('page uses the Emby view/controller contract without inline scripts', () => {
    const html = fs.readFileSync(path.join(source,'Configuration.html'),'utf8');
    assert.match(html, /class="view /);
    assert.match(html, /data-controller="configurationpage\?name=AudioMemoryJs"/);
    assert.doesNotMatch(html, /<script/i);
    assert.match(fs.readFileSync(path.join(source,'Plugin.cs'),'utf8'), /Name = "AudioMemoryJs", EmbeddedResourcePath = "Emby.AudioMemory.Configuration.js"/);
    assert.match(fs.readFileSync(path.join(source,'Emby.AudioMemory.csproj'),'utf8'), /EmbeddedResource Include="Configuration.js"/);
});
test('loads on resume and saves fields while preserving unrelated configuration', async () => {
    const s=setup(); await s.controller.onResume({});
    assert.equal(s.element('#audioMemoryUsers').value,'test-user');
    assert.equal(s.element('#audioMemoryEnable').checked,true);
    s.element('#audioMemoryUsers').value=' first, , second ';
    s.element('#audioMemoryEnable').checked=false;
    await s.controller.save();
    assert.deepEqual(Array.from(s.saved.AllowedUserIds),['first','second']);
    assert.equal(s.saved.Enable,false); assert.equal(s.saved.UnknownSetting,42);
    await s.controller.onResume({}); assert.equal(s.element('#audioMemoryEnable').checked,true);
});
test('failed loading cannot overwrite settings with an empty form', async () => {
    const s=setup(true); await s.controller.onResume({}); await s.controller.save();
    assert.equal(s.calls,0); assert.equal(s.controller.button.disabled,true);
    assert.match(s.controller.message.textContent,/Не удалось загрузить/);
});
test('save error is visible and allows a retry', async () => {
    const s=setup(); await s.controller.onResume({});
    s.api.updatePluginConfiguration=async()=>{throw Error('save');};
    await s.controller.save();
    assert.match(s.controller.message.textContent,/Не удалось сохранить/);
    assert.equal(s.controller.button.disabled,false);
});
test('status error leaves successfully loaded settings usable', async () => {
    const s=setup();s.api.getJSON=async()=>{throw Error('status');};
    await s.controller.onResume({}); assert.equal(s.controller.button.disabled,false);
    assert.match(s.element('#audioMemoryStatus').textContent,/Не удалось загрузить/);
});
