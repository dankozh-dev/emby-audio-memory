define(['baseView', 'emby-button', 'emby-checkbox'], function (BaseView) {
    'use strict';
    var pluginId = 'b7a7922e-5271-4d47-87ec-698aa4718c46';
    return class AudioMemoryView extends BaseView {
        constructor(view, params) {
            super(view, params);
            this.ready = false;
            this.saving = false;
            this.form = view.querySelector('#audioMemoryForm');
            this.button = this.form.querySelector('button[type="submit"]');
            this.message = view.querySelector('#audioMemoryMessage');
            this.userChoices = [];
            this.allUsers = view.querySelector('#audioMemoryAllUsers');
            this.allUsers.addEventListener('change', () => this.updateUserMode());
            this.form.addEventListener('submit', (event) => {
                event.preventDefault();
                this.save();
            });
        }
        updateUserMode() {
            this.view.querySelector('#audioMemorySelectedUsers').hidden = this.allUsers.checked;
        }
        renderUsers(users, selected) {
            const normalize = id => String(id || '').replace(/-/g, '').toLowerCase();
            const chosen = new Set(selected.map(normalize));
            const known = new Set(users.map(user => normalize(user.Id)));
            const rows = users.slice().sort((a, b) => (a.Name || '').localeCompare(b.Name || '', 'ru'));
            // Keep unavailable selections visible and removable instead of silently dropping them.
            selected.filter(id => !known.has(normalize(id))).forEach(id => rows.push({Id:id, Name:'Недоступный пользователь'}));
            const container = this.view.querySelector('#audioMemoryUsers');
            container.textContent = '';
            this.userChoices = rows.map(user => {
                const label = document.createElement('label');
                label.style.cssText = 'display:flex;align-items:center;gap:12px;padding:10px 0;cursor:pointer';
                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.style.cssText = 'width:20px;height:20px;margin:0;flex-shrink:0';
                checkbox.checked = chosen.has(normalize(user.Id));
                const name = document.createElement('span');
                name.textContent = user.Name || 'Пользователь без имени';
                label.appendChild(checkbox); label.appendChild(name); container.appendChild(label);
                return {id:user.Id, checkbox};
            });
            if (!rows.length) container.textContent = 'На сервере пока нет пользователей.';
        }
        renderPrioritySettings(config, users, studios) {
            this.studios = studios;
            this.priorityDrafts = {'': {inherit:false, profile:this.copyProfile(config.DefaultPriorities)}};
            (config.UserPriorities || []).forEach(item => {
                this.priorityDrafts[this.userKey(item.UserId)] = {inherit:false, profile:this.copyProfile(item.Profile)};
            });
            const scope = this.view.querySelector('#audioPriorityScope');
            scope.textContent = '';
            const option = (value, title) => { const node=document.createElement('option');node.value=value;node.textContent=title;scope.appendChild(node); };
            option('', 'Общие настройки');
            users.slice().sort((a,b)=>(a.Name||'').localeCompare(b.Name||'', 'ru')).forEach(user => option(this.userKey(user.Id), user.Name || 'Пользователь без имени'));
            Object.keys(this.priorityDrafts).filter(id=>id && !users.some(u=>this.userKey(u.Id)===id)).forEach(id=>option(id,'Недоступный пользователь'));
            this.priorityScope = ''; scope.value='';
            scope.onchange = () => {this.captureProfile();this.priorityScope=scope.value;this.showProfile();};
            this.view.querySelector('#audioPriorityInherit').onchange = () => {
                const draft=this.priorityDrafts[this.priorityScope];
                draft.inherit=this.view.querySelector('#audioPriorityInherit').checked;
                this.view.querySelector('#audioPriorityEditor').hidden=draft.inherit;
            };
            this.view.querySelector('#audioPriorityAdd').onclick = () => {
                this.captureProfile();this.priorityDrafts[this.priorityScope].profile.Rules.push({Kind:'studio',Studio:this.studios[0].Id,Adult:false});this.showProfile();
            };
            this.showProfile();
        }
        userKey(id) { return String(id || '').replace(/-/g,'').toLowerCase(); }
        copyProfile(profile) { return JSON.parse(JSON.stringify(profile || {Language:'rus',Rules:[]})); }
        captureProfile() {
            if (!this.priorityDrafts) return;
            const draft=this.priorityDrafts[this.priorityScope];
            draft.profile.Language=this.view.querySelector('#audioPriorityLanguage').value;
            draft.profile.Rules=this.priorityRows.map(row=>{
                const parts=row.select.value.split(':');
                return {Kind:parts[0], Studio:parts[0]==='custom'?row.custom.value.trim():(parts[1]||''), Adult:parts[2]==='adult'};
            });
        }
        showProfile() {
            const id=this.priorityScope;
            if (!this.priorityDrafts[id]) this.priorityDrafts[id]={inherit:true,profile:this.copyProfile(this.priorityDrafts[''].profile)};
            const draft=this.priorityDrafts[id];
            this.view.querySelector('#audioPriorityInheritLabel').hidden=!id;
            this.view.querySelector('#audioPriorityInherit').checked=draft.inherit;
            this.view.querySelector('#audioPriorityEditor').hidden=draft.inherit;
            const language=this.view.querySelector('#audioPriorityLanguage');language.textContent='';
            const languages=[['rus','Русский'],['eng','Английский'],['ukr','Украинский']];
            let selected=draft.profile.Language||'rus';selected=({ru:'rus',en:'eng',uk:'ukr'})[selected]||selected;
            if (!languages.some(l=>l[0]===selected)) languages.push([selected,selected]);
            languages.forEach(l=>{const o=document.createElement('option');o.value=l[0];o.textContent=l[1];language.appendChild(o);});language.value=selected;
            const container=this.view.querySelector('#audioPriorityRows');container.textContent='';
            const rules=draft.profile.Rules||[];
            this.priorityRows=rules.map((rule,index)=>{
                const row=document.createElement('div');row.style.cssText='display:flex;flex-wrap:wrap;gap:8px;align-items:center;margin-bottom:12px';
                const number=document.createElement('span');number.textContent=(index+1)+'.';row.appendChild(number);
                const select=document.createElement('select');select.style.cssText='flex:1;min-width:180px;padding:8px';
                const option=(value,title)=>{const o=document.createElement('option');o.value=value;o.textContent=title;select.appendChild(o);};
                this.studios.forEach(studio=>{option('studio:'+studio.Id,studio.Name);option('studio:'+studio.Id+':adult',studio.Name+' 18+');});
                option('dub','Любой дубляж');option('adult','Любая 18+ дорожка');option('custom','Своя студия');option('custom::adult','Своя студия 18+');
                select.value=rule.Kind==='studio'?'studio:'+rule.Studio+(rule.Adult?':adult':''):rule.Kind==='custom'?'custom'+(rule.Adult?'::adult':''):rule.Kind;
                const custom=document.createElement('input');custom.type='text';custom.placeholder='Название студии в дорожке';custom.setAttribute('aria-label','Название своей студии');custom.value=rule.Kind==='custom'?rule.Studio:'';custom.style.cssText='padding:8px;min-width:160px';
                const update=()=>{custom.hidden=select.value.split(':')[0]!=='custom';};select.onchange=update;update();
                row.appendChild(select);row.appendChild(custom);
                const button=(text,label,action,disabled)=>{const b=document.createElement('button');b.type='button';b.textContent=text;b.setAttribute('aria-label',label);b.style.cssText='padding:7px 12px';b.disabled=disabled;b.onclick=action;row.appendChild(b);};
                const move=offset=>{this.captureProfile();const values=draft.profile.Rules;[values[index],values[index+offset]]=[values[index+offset],values[index]];this.showProfile();};
                button('↑','Повысить приоритет',()=>move(-1),index===0);button('↓','Понизить приоритет',()=>move(1),index===rules.length-1);
                button('×','Удалить приоритет',()=>{this.captureProfile();draft.profile.Rules.splice(index,1);this.showProfile();},false);
                container.appendChild(row);return {select,custom};
            });
        }
        async onResume(options) {
            super.onResume(options);
            this.ready = false;
            this.button.disabled = true;
            this.message.textContent = 'Загрузка…';
            try {
                const [config, users, studios] = await Promise.all([ApiClient.getPluginConfiguration(pluginId), ApiClient.getJSON(ApiClient.getUrl('Users')), ApiClient.getJSON(ApiClient.getUrl('AudioMemory/Studios'))]);
                if (!Array.isArray(users) || !Array.isArray(studios) || !studios.length) throw new Error('Invalid settings data');
                this.view.querySelector('#audioMemoryEnable').checked = config.Enable;
                this.view.querySelector('#audioMemoryDebug').checked = config.DebugLogging;
                this.allUsers.checked = !!config.EnableForAllUsers;
                this.renderUsers(users, config.AllowedUserIds || []);
                this.updateUserMode();
                this.renderPrioritySettings(config, users, studios);
                this.ready = true;
                this.button.disabled = this.saving;
                this.message.textContent = '';
            } catch (error) {
                this.message.textContent = 'Не удалось загрузить настройки. Откройте страницу повторно.';
            }
            try {
                const status = await ApiClient.getJSON(ApiClient.getUrl('AudioMemory/Status'));
                this.view.querySelector('#audioMemoryStatus').textContent = JSON.stringify({Error: status.Error, Preferences: status.Preferences, ApplyRunning: status.ApplyRunning, ApplyError: status.ApplyError}, null, 2);
            } catch (error) {
                this.view.querySelector('#audioMemoryStatus').textContent = 'Не удалось загрузить состояние плагина.';
            }
        }
        async save() {
            if (!this.ready || this.saving) return;
            this.captureProfile();
            if (Object.values(this.priorityDrafts).some(d=>!d.inherit && d.profile.Rules.some(r=>r.Kind==='custom' && !r.Studio))) {
                this.message.textContent='Введите название своей студии или удалите пустой приоритет.';return;
            }
            const defaults=this.copyProfile(this.priorityDrafts[''].profile);
            const profiles=Object.keys(this.priorityDrafts).filter(id=>id && !this.priorityDrafts[id].inherit).map(id=>({UserId:id,Profile:this.copyProfile(this.priorityDrafts[id].profile)}));
            this.saving = true;
            this.button.disabled = true;
            const enable = this.view.querySelector('#audioMemoryEnable').checked;
            const debug = this.view.querySelector('#audioMemoryDebug').checked;
            const allUsers = this.allUsers.checked;
            const users = this.userChoices.filter(user => user.checkbox.checked).map(user => user.id);
            try {
                const config = await ApiClient.getPluginConfiguration(pluginId);
                config.Enable = enable;
                config.EnableForAllUsers = allUsers;
                config.DebugLogging = debug;
                config.AllowedUserIds = users;
                config.DefaultPriorities = defaults;
                config.UserPriorities = profiles;
                await ApiClient.updatePluginConfiguration(pluginId, config);
                this.message.textContent = 'Настройки сохранены.';
                if (enable) {
                    try { await ApiClient.ajax({type:'POST',url:ApiClient.getUrl('AudioMemory/Reconcile'),data:JSON.stringify({DryRun:false,Background:true}),contentType:'application/json'}); this.message.textContent='Настройки сохранены. Приоритеты применяются в фоне.'; }
                    catch (error) { this.message.textContent='Настройки сохранены, но применить их сейчас не удалось. Плагин повторит попытку по расписанию.'; }
                }
            } catch (error) {
                this.message.textContent = 'Не удалось сохранить настройки. Повторите попытку.';
            } finally {
                this.saving = false;
                this.button.disabled = !this.ready;
            }
        }
    };
});
