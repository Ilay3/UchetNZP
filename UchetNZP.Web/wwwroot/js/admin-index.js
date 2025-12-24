(function () {
    const optionsElement = document.getElementById('adminOptions');
    const partOptions = JSON.parse(optionsElement.dataset.partOptions || '[]');
    const operationOptions = JSON.parse(optionsElement.dataset.operationOptions || '[]');
    const sectionOptions = JSON.parse(optionsElement.dataset.sectionOptions || '[]');

    const tokenElement = document.querySelector('#adminAntiForgery input[name="__RequestVerificationToken"]');
    const requestVerificationToken = tokenElement ? tokenElement.value : '';

    const templates = {
        parts: document.getElementById('partsActionTemplate').innerHTML,
        operations: document.getElementById('operationsActionTemplate').innerHTML,
        sections: document.getElementById('sectionsActionTemplate').innerHTML,
        balances: document.getElementById('balancesActionTemplate').innerHTML
    };

    const panelTitle = document.getElementById('adminEntityPanelTitle');
    const panelHint = document.getElementById('adminEntityPanelHint');
    const modalFields = document.getElementById('adminEntityFields');
    const validationElement = document.getElementById('adminEntityValidation');
    const formElement = document.getElementById('adminEntityForm');
    const activeEntityLabel = document.getElementById('adminActiveEntityLabel');
    const selectionHint = document.getElementById('adminSelectionHint');

    let currentEntity = null;
    let currentId = null;

    const entityConfig = {
        parts: {
            titleCreate: 'Новая деталь',
            titleEdit: 'Изменение детали',
            tableId: 'partsTable',
            createUrl: '/admin/api/parts',
            updateUrl: (id) => `/admin/api/parts/${id}`,
            deleteUrl: (id) => `/admin/api/parts/${id}`,
            formTemplateId: 'partFormTemplate',
            columns: ['name', 'code']
        },
        operations: {
            titleCreate: 'Новая операция',
            titleEdit: 'Изменение операции',
            tableId: 'operationsTable',
            createUrl: '/admin/api/operations',
            updateUrl: (id) => `/admin/api/operations/${id}`,
            deleteUrl: (id) => `/admin/api/operations/${id}`,
            formTemplateId: 'partFormTemplate',
            columns: ['name', 'code']
        },
        sections: {
            titleCreate: 'Новый участок',
            titleEdit: 'Изменение участка',
            tableId: 'sectionsTable',
            createUrl: '/admin/api/sections',
            updateUrl: (id) => `/admin/api/sections/${id}`,
            deleteUrl: (id) => `/admin/api/sections/${id}`,
            formTemplateId: 'partFormTemplate',
            columns: ['name', 'code']
        },
        balances: {
            titleCreate: 'Новый остаток',
            titleEdit: 'Изменение остатка',
            tableId: 'balancesTable',
            createUrl: '/admin/api/balances',
            updateUrl: (id) => `/admin/api/balances/${id}`,
            deleteUrl: (id) => `/admin/api/balances/${id}`,
            formTemplateId: 'balanceFormTemplate',
            columns: ['partId', 'sectionId', 'opNumber', 'quantity']
        }
    };

    function replacePlaceholders(template, values) {
        let ret = template;
        Object.keys(values).forEach((key) => {
            const regexp = new RegExp(`{{${key}}}`, 'g');
            ret = ret.replace(regexp, values[key] ?? '');
        });
        return ret;
    }

    function resolveOptionValue(option) {
        return option.value ?? option.Value ?? '';
    }

    function resolveOptionText(option) {
        return option.text ?? option.Text ?? '';
    }

    function optionMarkup(options, selected) {
        return options
            .map((item) => {
                const value = resolveOptionValue(item);
                const text = resolveOptionText(item);
                const isSelected = Boolean(selected) && value === selected;
                return `<option value="${value}" ${isSelected ? 'selected' : ''}>${text}</option>`;
            })
            .join('');
    }

    function openPanel(entity, row) {
        currentEntity = entity;
        currentId = row ? row.id : null;
        const config = entityConfig[entity];
        const templateId = config.formTemplateId;
        const templateElement = document.getElementById(templateId);
        const rawTemplate = templateElement ? templateElement.innerHTML : '';

        const values = {
            name: row ? row.name : '',
            code: row ? row.code : '',
            opNumber: row ? row.opNumberFormatted : '',
            quantity: row ? row.quantity : '',
            partOptions: optionMarkup(partOptions, row ? row.partId : ''),
            sectionOptions: optionMarkup(sectionOptions, row ? row.sectionId : ''),
            operationOptions: optionMarkup(operationOptions, row ? row.operationId : ''),
            operationLabel: row ? row.operationLabel || '' : ''
        };

        modalFields.innerHTML = replacePlaceholders(rawTemplate, values);
        panelTitle.textContent = row ? config.titleEdit : config.titleCreate;
        panelHint.textContent = row ? 'Редактирование выбранной записи.' : 'Заполните поля и сохраните изменения.';
        validationElement.classList.add('d-none');
        validationElement.textContent = '';
    }

    function gatherFormData() {
        const ret = {};
        modalFields.querySelectorAll('input, select, textarea').forEach((element) => {
            ret[element.name] = element.value;
        });

        return ret;
    }

    function buildPayload(entity) {
        const raw = gatherFormData();
        if (entity === 'balances') {
            return {
                partId: raw.partId,
                sectionId: raw.sectionId,
                opNumber: (raw.opNumber || '').trim(),
                quantity: Number(raw.quantity || 0),
                operationId: raw.operationId || null,
                operationLabel: raw.operationLabel ? raw.operationLabel.trim() : null
            };
        }

        return {
            name: raw.name,
            code: raw.code
        };
    }

    async function submitEntity() {
        if (!currentEntity) {
            return;
        }

        const config = entityConfig[currentEntity];
        const payload = buildPayload(currentEntity);
        const isEdit = Boolean(currentId);
        const url = isEdit ? config.updateUrl(currentId) : config.createUrl;
        const method = isEdit ? 'PUT' : 'POST';

        try {
            const response = await fetch(url, {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    RequestVerificationToken: requestVerificationToken
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const error = await response.json();
                showErrors(error.errors || ['Не удалось сохранить изменения.']);
                return;
            }

            refreshTable(config.tableId);
            panelHint.textContent = 'Изменения сохранены.';
        } catch (e) {
            showErrors(['Произошла ошибка при сохранении.']);
        }
    }

    async function deleteEntity(entity, id) {
        const config = entityConfig[entity];
        if (!confirm('Удалить запись? Это действие нельзя отменить.')) {
            return;
        }

        try {
            const response = await fetch(config.deleteUrl(id), {
                method: 'DELETE',
                headers: {
                    RequestVerificationToken: requestVerificationToken
                }
            });

            if (!response.ok) {
                const error = await response.json();
                showErrors(error.errors || ['Не удалось удалить запись.']);
                return;
            }

            refreshTable(config.tableId);
        } catch (e) {
            showErrors(['Произошла ошибка при удалении.']);
        }
    }

    function showErrors(messages) {
        validationElement.textContent = Array.isArray(messages) ? messages.join(' ') : messages;
        validationElement.classList.remove('d-none');
    }

    function refreshTable(tableId) {
        const table = document.getElementById(tableId);
        if (!table) {
            return;
        }

        $(table).bootstrapTable('refresh');
    }

    function setTableLoading(tableId, isLoading) {
        const indicator = document.getElementById(`${tableId}Loading`);
        if (!indicator) {
            return;
        }

        if (isLoading) {
            indicator.classList.remove('d-none');
        } else {
            indicator.classList.add('d-none');
        }
    }

    function getActiveEntity() {
        const activeTab = document.querySelector('#adminTabs .nav-link.active');
        const ret = activeTab ? activeTab.dataset.entity : null;
        return ret;
    }

    function updateActiveEntityLabel() {
        const entity = getActiveEntity();
        const map = {
            parts: 'Детали',
            operations: 'Операции',
            sections: 'Участки',
            balances: 'Остатки'
        };
        const text = entity ? map[entity] : '';
        if (activeEntityLabel) {
            activeEntityLabel.textContent = text ? `Активный раздел: ${text}` : '';
        }
    }

    function updateSelectionHint(count) {
        if (!selectionHint) {
            return;
        }

        const text = count ? `Выбрано записей: ${count}` : 'Нет выбранных записей.';
        selectionHint.textContent = text;
    }

    function getSelectedRows(entity) {
        const config = entityConfig[entity];
        const table = $(`#${config.tableId}`);
        const rows = table.bootstrapTable('getSelections') || [];
        return rows;
    }

    function formatActions(entity, id) {
        return templates[entity].replace(/{{id}}/g, id);
    }

    window.partsActionFormatter = function (value, row) {
        return formatActions('parts', row.id);
    };

    window.operationsActionFormatter = function (value, row) {
        return formatActions('operations', row.id);
    };

    window.sectionsActionFormatter = function (value, row) {
        return formatActions('sections', row.id);
    };

    window.balancesActionFormatter = function (value, row) {
        return formatActions('balances', row.id);
    };

    function buildQueryParams(formId) {
        return function (params) {
            const form = document.getElementById(formId);
            if (!form) {
                return params;
            }

            const formData = new FormData(form);
            formData.forEach((value, key) => {
                if (value) {
                    params[key] = value;
                }
            });

            return params;
        };
    }

    function initTables() {
        $('#partsTable').bootstrapTable({ queryParams: buildQueryParams('partsFilterForm') });
        $('#operationsTable').bootstrapTable({ queryParams: buildQueryParams('operationsFilterForm') });
        $('#sectionsTable').bootstrapTable({ queryParams: buildQueryParams('sectionsFilterForm') });
        $('#balancesTable').bootstrapTable({ queryParams: buildQueryParams('balancesFilterForm') });
    }

    function initFilterForms() {
        $('.admin-filter-form').on('submit', function (event) {
            event.preventDefault();
            const target = $(this).data('table-target');
            refreshTable(target);
        });

        $('.admin-filter-reset').on('click', function () {
            const form = $(this).closest('form')[0];
            if (form) {
                form.reset();
                $(form).trigger('submit');
            }
        });
    }

    function onCreate(entity) {
        const targetEntity = entity || getActiveEntity();
        if (!targetEntity) {
            return;
        }

        openPanel(targetEntity, null);
    }

    function onEdit(entity, id) {
        const config = entityConfig[entity];
        const table = $(`#${config.tableId}`);
        const row = table.bootstrapTable('getRowByUniqueId', id);
        openPanel(entity, row || null);
    }

    async function onDelete(entity, ids) {
        const idList = Array.isArray(ids) ? ids : [ids];
        for (const id of idList) {
            await deleteEntity(entity, id);
        }
    }

    function initQuickActions() {
        $('.admin-quick-create').on('click', function () {
            onCreate(null);
        });

        $('.admin-quick-delete').on('click', async function () {
            const entity = getActiveEntity();
            if (!entity) {
                return;
            }

            const rows = getSelectedRows(entity);
            if (!rows.length) {
                showErrors(['Выберите запись для удаления.']);
                return;
            }

            const ids = rows.map((row) => row.id);
            await onDelete(entity, ids);
        });

        $('.admin-quick-export').on('click', function () {
            const entity = getActiveEntity();
            if (!entity) {
                return;
            }

            exportTable(entity);
        });
    }

    function initRowActions() {
        $(document).on('click', '.admin-edit', function () {
            const entity = $(this).data('entity');
            const id = $(this).data('id');
            onEdit(entity, id);
        });

        $(document).on('click', '.admin-delete', function () {
            const entity = $(this).data('entity');
            const id = $(this).data('id');
            onDelete(entity, id);
        });
    }

    function initModalForm() {
        formElement.addEventListener('submit', function (event) {
            event.preventDefault();
            submitEntity();
        });
    }

    function initSelectionTracking() {
        const tableIds = ['partsTable', 'operationsTable', 'sectionsTable', 'balancesTable'];
        tableIds.forEach((tableId) => {
            $(`#${tableId}`).on('check.bs.table uncheck.bs.table check-all.bs.table uncheck-all.bs.table', function () {
                const entity = getActiveEntity();
                if (!entity) {
                    return;
                }

                const rows = getSelectedRows(entity);
                updateSelectionHint(rows.length);
                if (rows.length) {
                    openPanel(entity, rows[0]);
                }
            });

            $(`#${tableId}`).on('click-row.bs.table', function (event, row) {
                const entity = getActiveEntity();
                if (!entity) {
                    return;
                }

                openPanel(entity, row);
            });
        });
    }

    function initLoadingIndicators() {
        const tableIds = ['partsTable', 'operationsTable', 'sectionsTable', 'balancesTable'];
        tableIds.forEach((tableId) => {
            setTableLoading(tableId, true);
            $(`#${tableId}`).on('refresh.bs.table page-change.bs.table sort.bs.table search.bs.table', function () {
                setTableLoading(tableId, true);
            });
            $(`#${tableId}`).on('post-body.bs.table load-success.bs.table load-error.bs.table', function () {
                setTableLoading(tableId, false);
            });
        });
    }

    function initTabTracking() {
        $('#adminTabs').on('shown.bs.tab', function () {
            updateActiveEntityLabel();
            const entity = getActiveEntity();
            if (!entity) {
                return;
            }

            const rows = getSelectedRows(entity);
            updateSelectionHint(rows.length);
        });
    }

    function exportTable(entity) {
        const config = entityConfig[entity];
        const table = $(`#${config.tableId}`);
        const rows = table.bootstrapTable('getData') || [];
        if (!rows.length) {
            showErrors(['Нет данных для экспорта.']);
            return;
        }

        const exportConfig = {
            parts: { columns: ['name', 'code'], headers: ['Название', 'Код'] },
            operations: { columns: ['name', 'code'], headers: ['Название', 'Код'] },
            sections: { columns: ['name', 'code'], headers: ['Название', 'Код'] },
            balances: { columns: ['partName', 'sectionName', 'operationName', 'operationLabel', 'opNumberFormatted', 'quantity'], headers: ['Деталь', 'Участок', 'Операция', 'Ярлык', '№ операции', 'Количество'] }
        };

        const exportItem = exportConfig[entity];
        const lines = [];
        lines.push(exportItem.headers.join(';'));
        rows.forEach((row) => {
            const values = exportItem.columns.map((column) => String(row[column] ?? '').replace(/;/g, ','));
            lines.push(values.join(';'));
        });

        const blob = new Blob([`\uFEFF${lines.join('\n')}`], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `admin-export-${entity}.csv`;
        link.click();
        URL.revokeObjectURL(url);
    }

    function resetFormPanel() {
        currentId = null;
        currentEntity = getActiveEntity();
        modalFields.innerHTML = '';
        validationElement.classList.add('d-none');
        validationElement.textContent = '';
        panelTitle.textContent = 'Форма';
        panelHint.textContent = 'Выберите запись в таблице или нажмите «Добавить».';
    }

    function initFormReset() {
        $('.admin-form-reset').on('click', function () {
            resetFormPanel();
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initTables();
        initFilterForms();
        initQuickActions();
        initRowActions();
        initModalForm();
        initSelectionTracking();
        initTabTracking();
        initFormReset();
        initLoadingIndicators();
        updateActiveEntityLabel();
    });
})();
