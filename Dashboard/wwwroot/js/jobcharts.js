window.JobCharts = (function () {
    const instances = {};

    const COLORS = [
        "#4e79a7", "#f28e2b", "#e15759", "#76b7b2",
        "#59a14f", "#edc948", "#b07aa1", "#ff9da7",
    ];

    function destroy(id) {
        if (instances[id]) {
            instances[id].destroy();
            delete instances[id];
        }
    }

    function bar(id, labels, data, label) {
        destroy(id);
        const ctx = document.getElementById(id);
        if (!ctx) return;
        instances[id] = new Chart(ctx, {
            type: "bar",
            data: {
                labels,
                datasets: [{
                    label,
                    data,
                    backgroundColor: COLORS.slice(0, data.length),
                    borderRadius: 4,
                }],
            },
            options: {
                responsive: true,
                indexAxis: "y",
                plugins: { legend: { display: false } },
                scales: { x: { beginAtZero: true } },
            },
        });
    }

    function doughnut(id, labels, data) {
        destroy(id);
        const ctx = document.getElementById(id);
        if (!ctx) return;
        instances[id] = new Chart(ctx, {
            type: "doughnut",
            data: {
                labels,
                datasets: [{
                    data,
                    backgroundColor: COLORS.slice(0, data.length),
                    borderWidth: 2,
                }],
            },
            options: {
                responsive: true,
                plugins: { legend: { position: "bottom" } },
            },
        });
    }

    function line(id, labels, data, label) {
        destroy(id);
        const ctx = document.getElementById(id);
        if (!ctx) return;
        instances[id] = new Chart(ctx, {
            type: "line",
            data: {
                labels,
                datasets: [{
                    label,
                    data,
                    fill: true,
                    tension: 0.3,
                    borderColor: "#4e79a7",
                    backgroundColor: "rgba(78,121,167,0.15)",
                    pointRadius: 4,
                }],
            },
            options: {
                responsive: true,
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true } },
            },
        });
    }

    function groupedBar(id, labels, datasets) {
        destroy(id);
        const ctx = document.getElementById(id);
        if (!ctx) return;
        instances[id] = new Chart(ctx, {
            type: "bar",
            data: {
                labels,
                datasets: datasets.map((ds, i) => ({
                    label: ds.label,
                    data: ds.data,
                    backgroundColor: COLORS[i % COLORS.length],
                    borderRadius: 3,
                    barPercentage: 0.75,
                })),
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: "y",
                plugins: { legend: { position: "top" } },
                scales: { x: { beginAtZero: true, ticks: { precision: 0 } } },
            },
        });
    }

    return { bar, doughnut, line, groupedBar };
})();
