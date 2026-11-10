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
        async onResume(options) {
            super.onResume(options);
            this.ready = false;
            this.button.disabled = true;
            this.message.textContent = 'Загрузка…';
            try {
                const [config, users] = await Promise.all([ApiClient.getPluginConfiguration(pluginId), ApiClient.getJSON(ApiClient.getUrl('Users'))]);
                if (!Array.isArray(users)) throw new Error('Invalid user list');
                this.view.querySelector('#audioMemoryEnable').checked = config.Enable;
                this.view.querySelector('#audioMemoryDebug').checked = config.DebugLogging;
                this.allUsers.checked = !!config.EnableForAllUsers;
                this.renderUsers(users, config.AllowedUserIds || []);
                this.updateUserMode();
                this.ready = true;
                this.button.disabled = this.saving;
                this.message.textContent = '';
            } catch (error) {
                this.message.textContent = 'Не удалось загрузить настройки. Откройте страницу повторно.';
            }
            try {
                const status = await ApiClient.getJSON(ApiClient.getUrl('AudioMemory/Status'));
                this.view.querySelector('#audioMemoryStatus').textContent = JSON.stringify({Error: status.Error, Preferences: status.Preferences}, null, 2);
            } catch (error) {
                this.view.querySelector('#audioMemoryStatus').textContent = 'Не удалось загрузить состояние плагина.';
            }
        }
        async save() {
            if (!this.ready || this.saving) return;
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
                await ApiClient.updatePluginConfiguration(pluginId, config);
                this.message.textContent = 'Настройки сохранены.';
            } catch (error) {
                this.message.textContent = 'Не удалось сохранить настройки. Повторите попытку.';
            } finally {
                this.saving = false;
                this.button.disabled = !this.ready;
            }
        }
    };
});
