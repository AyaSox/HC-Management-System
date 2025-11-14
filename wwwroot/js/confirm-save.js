// Shared confirmation modal utilities for Create/Edit save flows
(function (global) {
  const modalEl = () => document.getElementById('confirmSaveModal');
  const createSummaryEl = () => document.getElementById('confirmCreateSummary');
  const editSummaryEl = () => document.getElementById('confirmEditSummary');
  const createListEl = () => document.getElementById('confirmCreateList');
  const changesBodyEl = () => document.getElementById('changesTbody');
  const noChangesEl = () => document.getElementById('noChangesNote');
  const confirmBtnEl = () => document.getElementById('confirmSaveBtn');

  function formatCurrencyZA(amount) {
    const n = parseFloat(amount ?? 0);
    try {
      return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR', minimumFractionDigits: 2 }).format(n);
    } catch {
      return `R ${isNaN(n) ? '0.00' : n.toFixed(2)}`;
    }
  }

  function getSelectTextByValue(selectEl, value) {
    if (!selectEl) return '';
    const v = value == null ? '' : String(value);
    const opt = Array.from(selectEl.options).find(o => o.value === v);
    return opt ? opt.text : '';
  }

  function showModal() {
    const m = new bootstrap.Modal(modalEl());
    m.show();
    return m;
  }

  function showCreate(items, onConfirm, loadingText) {
    // Populate list
    const list = createListEl();
    if (list) {
      list.innerHTML = '';
      (items || []).forEach(([k, v]) => {
        const li = document.createElement('li');
        li.className = 'list-group-item';
        li.innerHTML = `<strong>${k}:</strong> ${v || '-'}`;
        list.appendChild(li);
      });
    }

    // Toggle sections
    const createEl = createSummaryEl();
    const editEl = editSummaryEl();
    if (createEl) createEl.classList.remove('d-none');
    if (editEl) editEl.classList.add('d-none');

    const modal = showModal();
    const btn = confirmBtnEl();
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = '<i class="fas fa-check"></i> Confirm & Save';
      btn.onclick = function () {
        btn.disabled = true;
        btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${loadingText || 'Saving...'}`;
        modal.hide();
        if (typeof onConfirm === 'function') onConfirm();
      };
    }
  }

  function showEdit(diffRows, noChanges, onConfirm, loadingText) {
    // Populate diff table
    const tbody = changesBodyEl();
    if (tbody) {
      tbody.innerHTML = '';
      (diffRows || []).forEach(([label, oldVal, newVal]) => {
        const tr = document.createElement('tr');
        tr.innerHTML = `<td>${label}</td><td>${oldVal || '-'}</td><td>${newVal || '-'}</td>`;
        tbody.appendChild(tr);
      });
    }

    // Toggle sections
    const createEl = createSummaryEl();
    const editEl = editSummaryEl();
    if (createEl) createEl.classList.add('d-none');
    if (editEl) editEl.classList.remove('d-none');

    const nc = noChangesEl();
    if (nc) nc.classList.toggle('d-none', !noChanges);

    const modal = showModal();
    const btn = confirmBtnEl();
    if (btn) {
      btn.disabled = false;
      btn.innerHTML = '<i class="fas fa-check"></i> Confirm & Save';
      btn.onclick = function () {
        btn.disabled = true;
        btn.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${loadingText || 'Saving...'}`;
        modal.hide();
        if (typeof onConfirm === 'function') onConfirm();
      };
    }
  }

  global.hrConfirm = {
    formatCurrencyZA,
    getSelectTextByValue,
    showCreate,
    showEdit
  };
})(window);
