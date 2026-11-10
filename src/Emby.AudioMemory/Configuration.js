define(['baseView', 'emby-input', 'emby-button', 'emby-checkbox'], function (BaseView) {
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
            this.form.addEventListener('submit', (event) => {
                event.preventDefault();
                this.save();
            });
        }
        async onResume(options) {
            super.onResume(options);
            this.ready = false;
            this.button.disabled = true;
            this.message.textContent = 'Загрузка…';
            try {
                const config = await ApiClient.getPluginConfiguration(pluginId);
                this.view.querySelector('#audioMemoryEnable').checked = config.Enable;
                this.view.querySelector('#audioMemoryDebug').checked = config.DebugLogging;
                this.view.querySelector('#audioMemoryUsers').value = (config.AllowedUserIds || []).join(', ');
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
            const users = this.view.querySelector('#audioMemoryUsers').value.split(',').map(x => x.trim()).filter(Boolean);
            try {
                const config = await ApiClient.getPluginConfiguration(pluginId);
                config.Enable = enable;
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
