// DueMap ApexCharts interop (self-hosted lib/apexcharts/apexcharts.min.js).
// Charts are styled from the live Osen CSS tokens so they match the theme in
// BOTH modes, and every chart re-renders automatically when the .dark class
// on <html> flips (theme.js toggle) — no Blazor round-trip needed.
window.dueMapCharts = (function () {
    const charts = {};   // elId -> { instance, spec }

    const cssVar = (name) =>
        getComputedStyle(document.documentElement).getPropertyValue(name).trim();

    const isDark = () => document.documentElement.classList.contains('dark');

    // "$1,234" — symbol comes from the PM's book currency (PmMoney), digits
    // grouped per the same culture. Charts round to whole units for legibility.
    const moneyFmt = (spec) => (v) =>
        (spec.currencySymbol || '') +
        new Intl.NumberFormat(spec.locale || 'en-US', { maximumFractionDigits: 0 }).format(v);

    function baseOptions(spec) {
        return {
            chart: {
                type: spec.type,
                height: spec.height || 260,
                fontFamily: 'Inter, ui-sans-serif, system-ui, sans-serif',
                foreColor: cssVar('--text-muted'),
                background: 'transparent',
                toolbar: { show: false },
                animations: { speed: 500 }
            },
            tooltip: { theme: isDark() ? 'dark' : 'light' },
            grid: { borderColor: cssVar('--line'), strokeDashArray: 3 },
            legend: { labels: { colors: cssVar('--text-base') } }
        };
    }

    function buildOptions(spec) {
        const base = baseOptions(spec);
        const fmt = moneyFmt(spec);

        if (spec.type === 'donut') {
            return Object.assign(base, {
                series: spec.series,
                labels: spec.labels,
                colors: [cssVar('--success'), cssVar('--warning'), cssVar('--danger')],
                stroke: { colors: [cssVar('--surface')], width: 2 },
                dataLabels: {
                    formatter: (val) => Math.round(val) + '%',
                    style: { fontSize: '11px', fontWeight: 600 },
                    dropShadow: { enabled: false }
                },
                legend: Object.assign(base.legend, { position: 'bottom', fontSize: '12px' }),
                plotOptions: {
                    pie: {
                        donut: {
                            size: '72%',
                            labels: {
                                show: true,
                                name: { fontSize: '12px', color: cssVar('--text-muted'), offsetY: 18 },
                                value: {
                                    fontSize: '18px', fontWeight: 600,
                                    color: cssVar('--text-heading'), offsetY: -14,
                                    formatter: fmt
                                },
                                total: {
                                    show: true, label: 'outstanding',
                                    fontSize: '11px', color: cssVar('--text-muted'),
                                    formatter: (w) => fmt(w.globals.seriesTotals.reduce((a, b) => a + b, 0))
                                }
                            }
                        }
                    }
                },
                tooltip: Object.assign(base.tooltip, { y: { formatter: fmt } })
            });
        }

        // grouped bar
        return Object.assign(base, {
            series: spec.series,
            colors: [cssVar('--primary'), cssVar('--success')],
            plotOptions: {
                bar: { columnWidth: '55%', borderRadius: 4, borderRadiusApplication: 'end' }
            },
            dataLabels: { enabled: false },
            stroke: { show: true, width: 3, colors: ['transparent'] },
            xaxis: {
                categories: spec.categories,
                axisBorder: { color: cssVar('--line') },
                axisTicks: { color: cssVar('--line') }
            },
            yaxis: { labels: { formatter: fmt } },
            legend: Object.assign(base.legend, { position: 'top', horizontalAlign: 'right', fontSize: '12px' }),
            tooltip: Object.assign(base.tooltip, { y: { formatter: fmt } })
        });
    }

    function render(elId, spec) {
        const el = document.getElementById(elId);
        if (!el || typeof ApexCharts === 'undefined') return;

        if (charts[elId]) {
            charts[elId].instance.destroy();
        }
        const instance = new ApexCharts(el, buildOptions(spec));
        charts[elId] = { instance, spec };
        instance.render();
    }

    function destroy(elId) {
        if (charts[elId]) {
            charts[elId].instance.destroy();
            delete charts[elId];
        }
    }

    // Re-skin every live chart when the theme class flips.
    new MutationObserver(() => {
        for (const [elId, entry] of Object.entries(charts)) {
            const el = document.getElementById(elId);
            if (!el) { delete charts[elId]; continue; }
            entry.instance.destroy();
            entry.instance = new ApexCharts(el, buildOptions(entry.spec));
            entry.instance.render();
        }
    }).observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });

    return { render, destroy };
})();
