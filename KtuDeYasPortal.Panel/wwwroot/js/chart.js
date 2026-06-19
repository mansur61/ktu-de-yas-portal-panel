// Chart.js zaman serisi grafik çizimi
let chartInstance = null;

function renderChart(canvasId, labels, values, labelName) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;

    // Chart.js zaten yüklü kontrolü
    if (typeof Chart === 'undefined') {
        console.warn('Chart.js not loaded');
        return;
    }

    const ctx = canvas.getContext('2d');

    if (chartInstance) {
        chartInstance.destroy();
    }

    const colors = ['#0d6efd', '#198754', '#dc3545', '#ffc107', '#6f42c1', '#fd7e14'];

    chartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: labelName,
                data: values,
                borderColor: colors[0],
                backgroundColor: colors[0] + '20',
                borderWidth: 2,
                pointRadius: 1,
                pointHoverRadius: 5,
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 300 },
            plugins: {
                legend: { display: true, position: 'top' },
                tooltip: {
                    mode: 'index',
                    intersect: false
                }
            },
            scales: {
                x: {
                    display: true,
                    title: { display: true, text: 'Zaman' },
                    ticks: { maxRotation: 45, maxTicksLimit: 20 }
                },
                y: {
                    display: true,
                    title: { display: true, text: labelName },
                    beginAtZero: false
                }
            },
            interaction: {
                mode: 'index',
                intersect: false
            }
        }
    });
}
