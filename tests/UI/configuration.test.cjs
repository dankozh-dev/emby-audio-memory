const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = path.resolve(__dirname, '../../src/Emby.AudioMemory');
function setup(failLoad = false) {
    let Controller, saved, calls = 0;
    const elements = new Map();
    const makeElement = () => ({value:'', checked:false, disabled:false, hidden:false, textContent:'', children:[], style:{}, addEventListener(){}, appendChild(child){this.children.push(child);}, querySelector:element});
    const element = id => {
        if (!elements.has(id)) elements.set(id, makeElement());
        return elements.get(id);
    };
    const api = {
        getPluginConfiguration: async () => { if(failLoad) throw Error('load'); return {Enable:true, DebugLogging:false, AllowedUserIds:['test-user'], UnknownSetting:42}; },
        getUrl: x => x,
        getJSON: async route => route === 'Users' ? [{Id:'test-user',Name:'Анна'},{Id:'second',Name:'Борис'}] : {Preferences:[], Error:null},
        updatePluginConfiguration: async (id, config) => {calls++; saved = config;}
    };
    vm.runInNewContext(fs.readFileSync(path.join(source, 'Configuration.js'),'utf8'), {
        ApiClient:api, document:{createElement:()=>makeElement()},
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
    assert.equal(s.controller.userChoices[0].checkbox.checked,true);
    assert.equal(s.element('#audioMemoryUsers').children[0].children[1].textContent,'Анна');
    assert.equal(s.controller.allUsers.checked,false);
    assert.equal(s.element('#audioMemoryEnable').checked,true);
    s.controller.userChoices[0].checkbox.checked=false;
    s.controller.userChoices[1].checkbox.checked=true;
    s.element('#audioMemoryEnable').checked=false;
    await s.controller.save();
    assert.deepEqual(Array.from(s.saved.AllowedUserIds),['second']);
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
    const s=setup();const get=s.api.getJSON;s.api.getJSON=async route=>{if(route==='Users')return get(route);throw Error('status');};
    await s.controller.onResume({}); assert.equal(s.controller.button.disabled,false);
    assert.match(s.element('#audioMemoryStatus').textContent,/Не удалось загрузить/);
});

test('all users mode preserves the chosen subset for switching back', async () => {
    const s=setup();await s.controller.onResume({});
    s.controller.allUsers.checked=true;s.controller.updateUserMode();
    assert.equal(s.element('#audioMemorySelectedUsers').hidden,true);
    await s.controller.save();assert.equal(s.saved.EnableForAllUsers,true);
    assert.deepEqual(Array.from(s.saved.AllowedUserIds),['test-user']);
    s.controller.allUsers.checked=false;s.controller.updateUserMode();await s.controller.save();
    assert.equal(s.saved.EnableForAllUsers,false);assert.equal(s.element('#audioMemorySelectedUsers').hidden,false);
    assert.deepEqual(Array.from(s.saved.AllowedUserIds),['test-user']);
});
test('user list failure prevents saving and expanding user scope', async () => {
    const s=setup();s.api.getJSON=async()=>{throw Error('users');};
    await s.controller.onResume({});await s.controller.save();
    assert.equal(s.calls,0);assert.equal(s.controller.button.disabled,true);
});
test('names are text, GUID formatting preserves selections, and missing users stay removable', async () => {
    const s=setup();
    s.api.getJSON=async route=>route==='Users'?[{Id:'TESTUSER',Name:'<img src=x>'}]:{};
    await s.controller.onResume({});
    assert.equal(s.controller.userChoices[0].checkbox.checked,true);
    assert.equal(s.element('#audioMemoryUsers').children[0].children[1].textContent,'<img src=x>');
    s.api.getJSON=async route=>route==='Users'?[]:{};await s.controller.onResume({});
    assert.equal(s.controller.userChoices[0].checkbox.checked,true);
    s.controller.userChoices[0].checkbox.checked=false;await s.controller.save();
    assert.equal(s.saved.AllowedUserIds.length,0);assert.equal(s.saved.EnableForAllUsers,false);
});
