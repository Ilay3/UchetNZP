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

    const modalElement = document.getElementById('adminEntityModal');
    const modal = new bootstrap.Modal(modalElement);
    const modalTitle = document.getElementById('adminEntityModalLabel');
    const modalFields = document.getElementById('adminEntityFields');
    const validationElement = document.getElementById('adminEntityValidation');
    const formElement = document.getElementById('adminEntityForm');

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

    function openModal(entity, row) {
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
        modalTitle.textContent = row ? config.titleEdit : config.titleCreate;
        validationElement.classList.add('d-none');
        validationElement.textContent = '';
        modal.show();
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

            modal.hide();
            refreshTable(config.tableId);
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

    function initCreateButtons() {
        $('.admin-create').on('click', function () {
            const entity = $(this).data('entity');
            openModal(entity, null);
        });
    }

    function initRowActions() {
        $(document).on('click', '.admin-edit', function () {
            const entity = $(this).data('entity');
            const id = $(this).data('id');
            const config = entityConfig[entity];
            const table = $(`#${config.tableId}`);
            const row = table.bootstrapTable('getRowByUniqueId', id);
            openModal(entity, row || null);
        });

        $(document).on('click', '.admin-delete', function () {
            const entity = $(this).data('entity');
            const id = $(this).data('id');
            deleteEntity(entity, id);
        });
    }

    function initModalForm() {
        formElement.addEventListener('submit', function (event) {
            event.preventDefault();
            submitEntity();
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initTables();
        initFilterForms();
        initCreateButtons();
        initRowActions();
        initModalForm();
    });
})();
