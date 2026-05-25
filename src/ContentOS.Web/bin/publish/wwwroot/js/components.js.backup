/**
 * ContentOS Component Library — JavaScript Interop
 * Handles: Command Palette, Dropdown/Popover/Select click-outside,
 * Sheet animations, Toast management, Calendar/DatePicker, Theme toggling
 */

// ============================================================
// COMMAND PALETTE
// ============================================================
window.contentOsCmd = {
    open: function () {
        const overlay = document.getElementById('cmdOverlay');
        if (overlay) {
            overlay.classList.add('open');
            const input = overlay.querySelector('.cmd-input');
            if (input) { input.value = ''; input.focus(); }
        }
    },
    close: function () {
        const overlay = document.getElementById('cmdOverlay');
        if (overlay) overlay.classList.remove('open');
    },
    filter: function (query) {
        const list = document.getElementById('cmdList');
        if (!list) return;
        // Blazor handles rendering; this is for any JS-side filtering if needed
    }
};

// ============================================================
// DROPDOWN / POPOVER / SELECT — Click-outside
// ============================================================
window.contentOsMenu = {
    closeAll: function () {
        document.querySelectorAll('[data-menu].open, [data-popover].open, [data-select-list].open, [data-calendar].open').forEach(el => el.classList.remove('open'));
        document.querySelectorAll('[data-select-trigger].open').forEach(el => el.classList.remove('open'));
    }
};

document.addEventListener('click', function (e) {
    if (!e.target.closest('.menu-anchor') && !e.target.closest('.popover-anchor')) {
        window.contentOsMenu.closeAll();
    }
});

// ============================================================
// SHEET
// ============================================================
window.contentOsSheet = {
    open: function (id) {
        const sheet = document.getElementById(id || 'cosSheet');
        const overlay = document.getElementById(id ? id + 'Overlay' : 'cosSheetOverlay');
        if (sheet) sheet.classList.add('open');
        if (overlay) overlay.classList.add('open');
    },
    close: function (id) {
        const sheet = document.getElementById(id || 'cosSheet');
        const overlay = document.getElementById(id ? id + 'Overlay' : 'cosSheetOverlay');
        if (sheet) sheet.classList.remove('open');
        if (overlay) overlay.classList.remove('open');
    }
};

// ============================================================
// DIALOG
// ============================================================
window.contentOsDialog = {
    open: function (id) {
        const overlay = document.getElementById(id + 'Overlay');
        if (overlay) overlay.classList.add('open');
    },
    close: function (id) {
        const overlay = document.getElementById(id ? id + 'Overlay' : 'dialogOverlay');
        if (overlay) overlay.classList.remove('open');
    }
};

// ============================================================
// TOAST
// ============================================================
window.contentOsToast = {
    _containerId: 'cosToastContainer',
    _icons: {
        success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><polyline points="20 6 9 17 4 12"/></svg>',
        error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>',
        info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round"><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>'
    },
    show: function (variant, title, desc, durationMs) {
        let container = document.getElementById(this._containerId);
        if (!container) {
            container = document.createElement('div');
            container.id = this._containerId;
            container.className = 'toast-container';
            document.body.appendChild(container);
        }
        const t = document.createElement('div');
        t.className = 'toast';
        const icons = this._icons;
        t.innerHTML =
            '<div class="toast-icon ' + variant + '"><span class="icon">' + (icons[variant] || icons.info) + '</span></div>' +
            '<div class="toast-body"><div class="toast-title">' + title + '</div>' +
            (desc ? '<div class="toast-desc">' + desc + '</div>' : '') +
            '</div>' +
            '<button class="toast-close" onclick="this.parentElement.classList.add(\'removing\'); setTimeout(()=>this.parentElement.remove(), 200)">' +
            '<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg></button>';
        container.appendChild(t);
        const dur = durationMs || 4500;
        setTimeout(function () {
            if (t.parentElement) {
                t.classList.add('removing');
                setTimeout(function () { t.remove(); }, 200);
            }
        }, dur);
    }
};

// ============================================================
// CALENDAR / DATE PICKER
// ============================================================
window.contentOsCalendar = {
    _state: {},

    getState: function (id) {
        return this._state[id] || null;
    },

    init: function (id) {
        const now = new Date();
        this._state[id] = { month: now.getMonth(), year: now.getFullYear(), selected: null };
    },

    render: function (triggerEl, calendarEl, id) {
        if (!this._state[id]) this.init(id);
        const s = this._state[id];
        const monthNames = ['January','February','March','April','May','June','July','August','September','October','November','December'];
        const today = new Date();
        const firstDay = new Date(s.year, s.month, 1);
        const lastDay = new Date(s.year, s.month + 1, 0);
        const startDow = firstDay.getDay();
        const daysInMonth = lastDay.getDate();
        const prevMonthLast = new Date(s.year, s.month, 0).getDate();

        let html = '<div class="calendar-head">' +
            '<button class="calendar-nav" onclick="contentOsCalendar.changeMonth(\'' + id + '\', -1)">' +
            '<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><polyline points="15 18 9 12 15 6"/></svg></button>' +
            '<div class="calendar-month">' + monthNames[s.month] + ' ' + s.year + '</div>' +
            '<button class="calendar-nav" onclick="contentOsCalendar.changeMonth(\'' + id + '\', 1)">' +
            '<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><polyline points="9 18 15 12 9 6"/></svg></button></div>';

        html += '<div class="calendar-grid">';
        ['S','M','T','W','T','F','S'].forEach(function(d) { html += '<div class="calendar-dow">' + d + '</div>'; });
        for (let i = startDow - 1; i >= 0; i--) {
            html += '<button class="calendar-day muted">' + (prevMonthLast - i) + '</button>';
        }
        for (let d = 1; d <= daysInMonth; d++) {
            const isToday = d === today.getDate() && s.month === today.getMonth() && s.year === today.getFullYear();
            const isSelected = s.selected && d === s.selected.d && s.month === s.selected.m && s.year === s.selected.y;
            let cls = 'calendar-day';
            if (isToday) cls += ' today';
            if (isSelected) cls += ' selected';
            html += '<button class="' + cls + '" onclick="contentOsCalendar.pickDay(\'' + id + '\',' + d + ')">' + d + '</button>';
        }
        const totalCells = startDow + daysInMonth;
        const trailing = (7 - (totalCells % 7)) % 7;
        for (let d = 1; d <= trailing; d++) {
            html += '<button class="calendar-day muted">' + d + '</button>';
        }
        html += '</div>';
        html += '<div class="calendar-foot">' +
            '<input type="time" class="input" value="09:00" />' +
            '<button class="btn btn-primary btn-sm" onclick="contentOsCalendar.confirm(\'' + id + '\')">Schedule</button></div>';
        calendarEl.innerHTML = html;
    },

    toggle: function (triggerEl, calendarEl, id) {
        const isOpen = calendarEl.classList.contains('open');
        window.contentOsMenu.closeAll();
        if (!isOpen) {
            this.render(triggerEl, calendarEl, id);
            calendarEl.classList.add('open');
        }
    },

    changeMonth: function (id, delta) {
        const s = this._state[id];
        if (!s) return;
        s.month += delta;
        if (s.month < 0) { s.month = 11; s.year--; }
        if (s.month > 11) { s.month = 0; s.year++; }
        const calendarEl = document.querySelector('[data-calendar-id="' + id + '"]');
        if (calendarEl) this.render(null, calendarEl, id);
    },

    pickDay: function (id, day) {
        const s = this._state[id];
        if (!s) return;
        s.selected = { d: day, m: s.month, y: s.year };
        const calendarEl = document.querySelector('[data-calendar-id="' + id + '"]');
        if (calendarEl) this.render(null, calendarEl, id);
    },

    confirm: function (id) {
        const s = this._state[id];
        if (!s || !s.selected) {
            window.contentOsToast.show('error', 'Pick a date', 'Select a day from the calendar first.');
            return null;
        }
        const monthNames = ['January','February','March','April','May','June','July','August','September','October','November','December'];
        const dateStr = monthNames[s.selected.m] + ' ' + s.selected.d + ', ' + s.selected.y;
        window.contentOsMenu.closeAll();
        return dateStr;
    }
};

// ============================================================
// KEYBOARD SHORTCUTS
// ============================================================
document.addEventListener('keydown', function (e) {
    // Cmd/Ctrl+K opens command palette
    if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        window.contentOsCmd.open();
    }
    // Escape closes overlays
    if (e.key === 'Escape') {
        window.contentOsCmd.close();
        // Close any open dialogs
        document.querySelectorAll('.dialog-overlay.open').forEach(function (el) { el.classList.remove('open'); });
        // Close sheet
        window.contentOsSheet.close();
        // Close menus
        window.contentOsMenu.closeAll();
    }
});

// ============================================================
// BLAZER INTEROP — Toast from .NET
// ============================================================
window.contentOsInterop = {
    showToast: function (variant, title, desc) {
        window.contentOsToast.show(variant, title, desc);
    },
    openSheet: function (id) {
        window.contentOsSheet.open(id);
    },
    closeSheet: function (id) {
        window.contentOsSheet.close(id);
    },
    openDialog: function (id) {
        window.contentOsDialog.open(id);
    },
    closeDialog: function (id) {
        window.contentOsDialog.close(id);
    },
    openCommandPalette: function () {
        window.contentOsCmd.open();
    },
    closeCommandPalette: function () {
        window.contentOsCmd.close();
    },
    toggleMenu: function (menuEl) {
        const isOpen = menuEl.classList.contains('open');
        window.contentOsMenu.closeAll();
        if (!isOpen) menuEl.classList.add('open');
    },
    togglePopover: function (popoverEl) {
        const isOpen = popoverEl.classList.contains('open');
        window.contentOsMenu.closeAll();
        if (!isOpen) popoverEl.classList.add('open');
    },
    toggleSelect: function (triggerEl, listEl) {
        const isOpen = listEl.classList.contains('open');
        window.contentOsMenu.closeAll();
        if (!isOpen) {
            listEl.classList.add('open');
            triggerEl.classList.add('open');
        }
    },
    closeAllMenus: function () {
        window.contentOsMenu.closeAll();
    },
    toggleCalendar: function (triggerEl, calendarEl, id) {
        window.contentOsCalendar.toggle(triggerEl, calendarEl, id);
    },
    confirmCalendar: function (id) {
        return window.contentOsCalendar.confirm(id);
    },

    // Click-outside handlers for dropdowns, popovers
    setupClickOutside: function (dotNetRef) {
        const handler = function (e) {
            const menu = document.querySelector('.dropdown-menu.open, .popover-menu.open');
            if (menu && !menu.contains(e.target)) {
                dotNetRef.invokeMethodAsync('Close');
            }
        };
        document.addEventListener('click', handler);
        return handler;
    },
    addClass: function (el, cls) {
        if (el) el.classList.add(cls);
    },

    // Dialog escape key handler
    setupDialogEscape: function (dotNetRef) {
        const handler = function (e) {
            if (e.key === 'Escape') {
                dotNetRef.invokeMethodAsync('Close');
            }
        };
        document.addEventListener('keydown', handler);
        return handler;
    },

    // Sheet escape key handler
    setupSheetEscape: function (dotNetRef) {
        const handler = function (e) {
            if (e.key === 'Escape') {
                dotNetRef.invokeMethodAsync('Close');
            }
        };
        document.addEventListener('keydown', handler);
        return handler;
    },

    // Select click-outside handler
    setupSelectClickOutside: function (dotNetRef) {
        const handler = function (e) {
            const sel = document.querySelector('.select-list.open');
            if (sel && !sel.contains(e.target)) {
                const trigger = document.querySelector('.select-trigger.open');
                if (trigger && !trigger.contains(e.target)) {
                    dotNetRef.invokeMethodAsync('Close');
                }
            }
        };
        document.addEventListener('click', handler);
        return handler;
    },

    // Calendar click-outside handler
    setupCalendarClickOutside: function (dotNetRef) {
        const handler = function (e) {
            const cal = document.querySelector('.calendar.open');
            if (cal && !cal.contains(e.target)) {
                dotNetRef.invokeMethodAsync('CloseCalendar');
            }
        };
        document.addEventListener('click', handler);
        return handler;
    }
};