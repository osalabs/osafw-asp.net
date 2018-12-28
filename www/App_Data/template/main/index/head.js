window.Chart.defaults.global = $.extend(true, window.Chart.defaults.global, {
    responsive: false,
    maintainAspectRatio: false,
    defaultColor: '#999999',
    defaultFontColor: '#999999',
// //    defaultFontFamily: t.base,
     defaultFontSize: 13,
    layout: {
        padding: 0
    },
    legend: {
        display: false,
        position: "bottom",
        labels: {
            usePointStyle: true,
            padding: 16
        }
    },
    elements: {
        point: {
            radius: 0,
            backgroundColor: '#333333'
        },
        line: {
            tension: 0.4,
            borderWidth: 3,
            borderColor: '#333333',
            backgroundColor: 'transparent',
            borderCapStyle: "rounded"
        },
        rectangle: {
            backgroundColor: '#333333'
        },
        arc: {
            backgroundColor: '#333333',
            borderColor: '#ffffff',
            borderWidth: 4
        }
    }
});
