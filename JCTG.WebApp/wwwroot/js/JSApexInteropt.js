window.apexcharts = {};


export function apexAreaChartInit(element, refId, name, data, options) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    window.apexcharts[refId] = new ApexCharts(
        element,
        {
            series: [
                {
                    name: name,
                    data: data,
                },
            ],
            chart: {
                id: "area-datetime",
                fontFamily: "Plus Jakarta Sans', sans-serif",
                type: "area",
                height: 350,
                zoom: {
                    autoScaleYaxis: true,
                },
                toolbar: {
                    show: false,
                },
            },
            grid: {
                show: false,
            },
            colors: ["#615dff"],
            dataLabels: {
                enabled: false,
            },
            xaxis: {
                type: "datetime",
                tickAmount: 6,
                labels: {
                    style: {
                        colors: [
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                        ],
                    },
                },
            },
            yaxis: {
                labels: {
                    style: {
                        colors: [
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                        ],
                    },
                },
            },
            tooltip: {
                x: {
                    format: "dd MMM yyyy",
                },
                theme: "dark",
            },
            fill: {
                type: "gradient",
                gradient: {
                    shadeIntensity: 1,
                    opacityFrom: 0.7,
                    opacityTo: 0.9,
                    stops: [0, 100],
                },
            },
        }
    );
    window.apexcharts[refId].render();
}

export function apexAreaChartMiniInit(element, refId, name, data, color) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    window.apexcharts[refId] = new ApexCharts(
        element,
        {
            series: [
                {
                    name: name,
                    data: data,
                },
            ],
            chart: {
                height: 75,
                type: "area",
                fontFamily: '"Nunito Sans",sans-serif',
                zoom: {
                    enabled: false,
                },
                toolbar: {
                    show: false,
                },
                sparkline: {
                    enabled: true,
                },
            },
            dataLabels: {
                enabled: false,
            },
            colors: [color],
            stroke: {
                curve: "smooth",
                width: 2,
            },
            fill: {
                type: "solid",
                opacity: 0.2,
            },
            grid: {
                show: false,
            },
            xaxis: {
                show: false,
            },
            yaxis: {
                show: false,
            },
            tooltip: {
                theme: "dark",
            },
        }
    );
    window.apexcharts[refId].render();
}

export function apexCandleChartInit(element, refId, name, data, options) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    window.apexcharts[refId] = new ApexCharts(
        element,
        {
            series: [
                {
                    name: name,
                    data: data,
                },
            ],
            chart: {
                id: "candle-datetime",
                fontFamily: "Plus Jakarta Sans', sans-serif",
                type: "candlestick",
                height: 350,
                zoom: {
                    autoScaleYaxis: true,
                },
                toolbar: {
                    show: false,
                },
            },
            grid: {
                show: false,
            },
            colors: ["#615dff"],
            dataLabels: {
                enabled: false,
            },
            xaxis: {
                type: "datetime",
                tickAmount: 6,
                labels: {
                    style: {
                        colors: [
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                        ],
                    },
                },
            },
            yaxis: {
                labels: {
                    style: {
                        colors: [
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                            "#a1aab2",
                        ],
                    },
                },
            },
            tooltip: {
                x: {
                    format: "dd MMM yyyy",
                },
                theme: "dark",
            },
            fill: {
                type: "gradient",
                gradient: {
                    shadeIntensity: 1,
                    opacityFrom: 0.7,
                    opacityTo: 0.9,
                    stops: [0, 100],
                },
            },
        }
    );
    window.apexcharts[refId].render();
}