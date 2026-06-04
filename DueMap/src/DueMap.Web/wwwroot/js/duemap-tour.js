// DueMap onboarding tour. Wraps driver.js so the Blazor side only has to
// call `dueMapTour.start()` after first sign-in. Steps target stable CSS
// selectors via `data-tour="…"` attributes so future markup tweaks don't
// silently break the tour.
//
// Persistence: localStorage flag per browser. A different machine / browser
// will see the tour again — acceptable for v1; a server-side flag on the
// PropertyManager row is a separate task once we want to dedupe across devices.

(function () {
    const STORAGE_KEY = 'duemap-tour-completed';

    function isCompleted() {
        try { return localStorage.getItem(STORAGE_KEY) === '1'; } catch { return false; }
    }
    function markCompleted() {
        try { localStorage.setItem(STORAGE_KEY, '1'); } catch { /* private mode */ }
    }
    function reset() {
        try { localStorage.removeItem(STORAGE_KEY); } catch { /* private mode */ }
    }

    function buildSteps() {
        return [
            {
                popover: {
                    title: "Welcome to DueMap",
                    description:
                        "Your accounting data is connected. Let's take a 30-second tour of where everything lives.",
                    showButtons: ['next', 'close']
                }
            },
            {
                element: '[data-tour="connection-pill"]',
                popover: {
                    title: "Your workspace",
                    description:
                        "This shows the QuickBooks (or Xero) workspace DueMap is reading from. " +
                        "Click any time to reconnect or switch providers.",
                    side: 'bottom',
                    align: 'start'
                }
            },
            {
                element: '[data-tour="kpi-tiles"]',
                popover: {
                    title: "Portfolio at a glance",
                    description:
                        "Customers, open invoices, overdue counts, and active leases — all clickable. " +
                        "Each tile drills into the matching detail grid.",
                    side: 'bottom'
                }
            },
            {
                element: '[data-tour="recent-invoices"]',
                popover: {
                    title: "Recent invoices",
                    description:
                        "Your latest synced invoices show up here. Use the status pill to spot overdue ones at a glance.",
                    side: 'top'
                }
            },
            {
                element: '[data-tour="sidebar-setup"]',
                popover: {
                    title: "Set up your notices",
                    description:
                        "Notice preferences control when reminders go out. Templates let you customise the copy per " +
                        "notice type and state — required legal placeholders stay enforced.",
                    side: 'right',
                    align: 'start'
                }
            },
            {
                element: '[data-tour="sidebar-portfolio"]',
                popover: {
                    title: "Your portfolio",
                    description:
                        "Leases for per-lease overrides, the customer ↔ lease linker, the daily processing run history, " +
                        "and the new invoices grid all live here.",
                    side: 'right',
                    align: 'start'
                }
            },
            {
                popover: {
                    title: "You're set",
                    description:
                        "That's the whole shape of it. The 15-minute sweep is running in the background — your " +
                        "first notices go out on the next due date that matches your preferences.",
                    showButtons: ['close']
                }
            }
        ];
    }

    function startTour(options) {
        options = options || {};
        if (!options.force && isCompleted()) return;
        if (typeof window.driver === 'undefined' || typeof window.driver.js === 'undefined') {
            console.warn('[duemap-tour] driver.js is not loaded — tour skipped.');
            return;
        }

        const drv = window.driver.js.driver({
            showProgress: true,
            showButtons: ['next', 'previous', 'close'],
            steps: buildSteps(),
            onDestroyed: () => markCompleted()
        });
        drv.drive();
    }

    window.dueMapTour = {
        start: startTour,
        reset: reset,
        isCompleted: isCompleted
    };
})();
