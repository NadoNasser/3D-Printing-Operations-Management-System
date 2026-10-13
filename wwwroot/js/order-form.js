/* Shared by Orders/Create and Orders/Edit: customer picker, dynamic order lines, print-file rows, totals. */
window.OrderForm = (function () {
    'use strict';

    const num = v => { const n = parseFloat(v); return isNaN(n) ? 0 : n; };
    const fmt = n => n.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const fmtSize = b => b > 1048576 ? (b / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(b / 1024)) + ' KB';

    // ---------- Customer search + blacklist warning ----------
    function initCustomer() {
        const search = document.getElementById('customerSearch');
        const customerId = document.getElementById('customerId');
        const error = document.getElementById('customerError');
        const warning = document.getElementById('blacklistWarning');
        const reason = document.getElementById('blacklistReason');
        const options = Array.from(document.querySelectorAll('#customerList option'));
        let selected = null;

        function update() {
            const value = search.value.trim();
            selected = options.find(o => o.value === value) || null;
            customerId.value = selected ? selected.dataset.id : '';
            error.textContent = (!selected && value) ? 'Please select a customer from the search results.' : '';
            const bl = !!selected && selected.dataset.blacklisted === 'true';
            reason.textContent = bl && selected.dataset.blacklistReason ? ' Reason: ' + selected.dataset.blacklistReason : '';
            warning.classList.toggle('d-none', !bl);
        }
        search.addEventListener('input', update);
        if (customerId.value && customerId.value !== '0') {
            const pre = options.find(o => o.dataset.id === customerId.value);
            if (pre) search.value = pre.value;
        }
        update();
        return { search, customerId, error, selected: () => selected };
    }

    // ---------- Order lines ----------
    function initLines(o) {
        const host = document.getElementById(o.hostId || 'items');
        const tpl = document.getElementById('lineTemplate');
        const grand = document.getElementById(o.grandId || 'grandTotal');
        let uid = 0;

        const selectedOption = line => { const s = line.querySelector('.line-item'); return s.selectedIndex >= 0 ? s.options[s.selectedIndex] : null; };
        const isPrinting = line => { const opt = selectedOption(line); return !!opt && opt.dataset.printing === 'true'; };
        const defaultPrice = line => { const opt = selectedOption(line); return opt ? num(opt.dataset.price) : 0; };

        function addLine(init) {
            const frag = tpl.content.cloneNode(true);
            const line = frag.querySelector('.order-line');
            const id = ++uid;
            line.querySelectorAll('[id^="job-"]').forEach(e => e.id = 'job-' + id);
            line.querySelectorAll('label[for^="job-"]').forEach(e => e.htmlFor = 'job-' + id);
            host.appendChild(frag);

            const select = line.querySelector('.line-item');
            if (init && init.itemId) select.value = init.itemId; else select.selectedIndex = -1;
            if (init && init.itemId) {
                line.querySelector('.line-qty').value = init.qty || 1;
                line.querySelector('.line-price').value = init.price || 0;
                line.querySelector('.line-createjob').checked = !!init.createJob;
                line.querySelector('[name$=".Priority"]').value = init.priority || 2;
                line.querySelector('[name$=".EstimatedHours"]').value = init.hours || 1;
            }
            wireLine(line);
            applyItemChange(line, !(init && init.itemId));
            reindex();
            recalc();
            return line;
        }

        function wireLine(line) {
            line.querySelector('.line-item').addEventListener('change', () => { applyItemChange(line, true); reindex(); recalc(); });
            line.querySelector('.line-qty').addEventListener('input', recalc);
            line.querySelector('.line-price').addEventListener('input', recalc);
            line.querySelector('.line-createjob').addEventListener('change', () => applyPanelState(line));
            line.querySelector('.remove-line').addEventListener('click', () => { line.remove(); reindex(); recalc(); });
            line.querySelector('.line-picker').addEventListener('change', e => { addFiles(line, e.target.files); e.target.value = ''; });
        }

        function applyItemChange(line, resetPrice) {
            const printing = isPrinting(line);
            if (resetPrice) line.querySelector('.line-price').value = defaultPrice(line);
            line.querySelector('.print-panel').classList.toggle('d-none', !printing);
            if (!printing) line.querySelector('.file-rows').innerHTML = '';
            // inputs inside a hidden print panel must not be posted
            line.querySelectorAll('.print-panel input, .print-panel select').forEach(el => { el.disabled = !printing; });
            applyPanelState(line);
        }

        function applyPanelState(line) {
            const printing = isPrinting(line);
            const hasFiles = line.querySelectorAll('.file-row').length > 0;
            const wantsJob = line.querySelector('.line-createjob').checked;
            line.querySelectorAll('.job-field, .job-field-head').forEach(el => el.classList.toggle('d-none', !wantsJob));
            line.querySelectorAll('.nofile-only').forEach(el => el.classList.toggle('d-none', !wantsJob || hasFiles));
            line.querySelector('.file-table').classList.toggle('d-none', !hasFiles);
            line.querySelectorAll('.file-row .file-hours').forEach(el => el.classList.toggle('d-none', !wantsJob));
            line.querySelectorAll('.plain-fields').forEach(el => el.classList.toggle('d-none', printing && hasFiles));
            line.querySelector('.line-qty').disabled = printing && hasFiles;
            line.querySelector('.line-price').disabled = printing && hasFiles;
            line.querySelectorAll('.file-row .file-hours input').forEach(el => el.disabled = !wantsJob);
        }

        function addFiles(line, fileList) {
            const rows = line.querySelector('.file-rows');
            Array.from(fileList).forEach(file => {
                const row = document.createElement('div');
                row.className = 'row g-2 align-items-center file-row mb-1';
                row.innerHTML =
                    '<input type="file" name="Items[0].Files[0].Upload" hidden />' +
                    '<div class="col-md-4 text-truncate"><span class="file-name"></span> <span class="text-muted small file-size"></span></div>' +
                    '<div class="col-md-2"><input name="Items[0].Files[0].Quantity" value="1" class="form-control form-control-sm file-qty" /></div>' +
                    '<div class="col-md-2"><input name="Items[0].Files[0].UnitPrice" class="form-control form-control-sm file-price" ' + (o.canSetFilePrice ? '' : 'readonly') + ' /></div>' +
                    '<div class="col-md-2 file-hours"><input name="Items[0].Files[0].EstimatedHours" value="1" class="form-control form-control-sm" /></div>' +
                    '<div class="col-md-1 text-end fw-semibold file-total">0.00</div>' +
                    '<div class="col-md-1 text-end"><button type="button" class="btn btn-sm btn-outline-danger remove-file" title="Remove file">&times;</button></div>';
                row.querySelector('.file-name').textContent = file.name;
                row.querySelector('.file-size').textContent = fmtSize(file.size);
                row.querySelector('.file-price').value = defaultPrice(line);
                const dt = new DataTransfer(); dt.items.add(file);
                row.querySelector('input[type=file]').files = dt.files;
                row.querySelector('.file-qty').addEventListener('input', recalc);
                row.querySelector('.file-price').addEventListener('input', recalc);
                row.querySelector('.remove-file').addEventListener('click', () => { row.remove(); applyPanelState(line); reindex(); recalc(); });
                rows.appendChild(row);
            });
            applyPanelState(line);
            reindex();
            recalc();
        }

        function recalc() {
            let total = 0;
            host.querySelectorAll('.order-line').forEach(line => {
                let lineTotal = 0;
                const files = line.querySelectorAll('.file-row');
                if (isPrinting(line) && files.length) {
                    files.forEach(r => {
                        const t = Math.max(0, parseInt(r.querySelector('.file-qty').value) || 0) * num(r.querySelector('.file-price').value);
                        r.querySelector('.file-total').textContent = fmt(t);
                        lineTotal += t;
                    });
                } else {
                    const price = (o.canOverridePrice || !selectedOption(line)) ? num(line.querySelector('.line-price').value) : defaultPrice(line);
                    lineTotal = Math.max(0, parseInt(line.querySelector('.line-qty').value) || 0) * price;
                }
                line.querySelector('.line-total').textContent = fmt(lineTotal);
                total += lineTotal;
            });
            grand.textContent = fmt(total + (o.getBaseTotal ? o.getBaseTotal() : 0));
        }

        // Model binding needs gap-free indexes, so renumber every field name after any add/remove.
        function reindex() {
            host.querySelectorAll('.order-line').forEach((line, li) => {
                line.querySelectorAll('[name]').forEach(el => { el.name = el.name.replace(/^Items\[\d+\]/, 'Items[' + li + ']'); });
                line.querySelectorAll('.file-row').forEach((row, fi) => {
                    row.querySelectorAll('[name]').forEach(el => { el.name = el.name.replace(/Files\[\d+\]/, 'Files[' + fi + ']'); });
                });
            });
        }

        document.getElementById(o.addBtnId || 'addLineBtn').addEventListener('click', () => addLine(null));
        (o.initialLines || []).forEach(l => addLine(l));
        recalc();
        return { recalc, reindex };
    }

    // ---------- Submit guard ----------
    function attachSubmit(form, customer, lines) {
        form.addEventListener('submit', function (event) {
            lines.reindex();
            if (!customer.customerId.value) {
                event.preventDefault();
                customer.error.textContent = 'Please select a customer from the search results.';
                customer.search.focus();
                return;
            }
            const sel = customer.selected();
            if (sel && sel.dataset.blacklisted === 'true') {
                const r = sel.dataset.blacklistReason;
                const msg = 'WARNING: This customer is blacklisted.' + (r ? '\nReason: ' + r : '') + '\n\nDo you want to continue with this order?';
                if (!window.confirm(msg)) { event.preventDefault(); return; }
            }
            let total = 0, tooBig = null;
            form.querySelectorAll('.file-row input[type=file]').forEach(i => {
                const f = i.files[0]; if (!f) return;
                total += f.size;
                if (f.size > 100 * 1024 * 1024) tooBig = f.name;
            });
            if (tooBig) { event.preventDefault(); alert(tooBig + ' exceeds the 100 MB per-file limit.'); return; }
            if (total > 300 * 1024 * 1024) { event.preventDefault(); alert('The total upload size cannot exceed 300 MB.'); }
        });
    }

    return { initCustomer, initLines, attachSubmit, fmt, num };
})();
