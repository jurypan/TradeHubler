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

export function apexLineChartInit(element, refId, name1, data1, name2, data2) {
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
                    name: name1,
                    data: data1,
                },
                {
                    name: name2,
                    data: data2,
                },
            ],
            chart: {
                height: 400,
                type: "line",
                fontFamily: "inherit",
                zoom: {
                    enabled: false,
                },
                toolbar: {
                    show: false,
                },
            },
            colors: ["#2962ff", "#dadada"],
            dataLabels: {
                enabled: false,
            },
            stroke: {
                curve: "straight",
                colors: ["#2962ff", "#dadada"],
                width: 1,
            },
            markers: {
                size: 4,
                colors: ["#2962ff", "#dadada"],
                strokeColors: "transparent",
            },
            grid: {
                show: false,
            },
            xaxis: {
                type: "datetime",
                labels: {
                    style: {
                        colors: [
                            "#a1aab2"
                        ],
                    },
                },
            },
            yaxis: {
                labels: {
                    style: {
                        colors: [
                            "#a1aab2"
                        ],
                    },
                },
            },
            tooltip: {
                x: {
                    format: "dd/MM/yy HH:mm:ss"
                },
                theme: "dark",
                //custom: function ({ series, seriesIndex, dataPointIndex, w }) {
                //    var data = series[seriesIndex][dataPointIndex];

                //    return '<ul>' +
                //        '<li><b>RR</b>: ' + data.Price + '</li>' +
                //        '<li><b>Magic</b>: ' + data.Magic + '</li>' +
                //        '</ul>';
                //}
            },
            legend: {
                show: false,
            }
        }
    );
    window.apexcharts[refId].render();
}

export function apexLineChartUpdate(element, refId, name1, data1, name2, data2) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    window.apexcharts[refId].updateSeries([
            {
                name: name1,
                data: data1,
            },
            {
                name: name2,
                data: data2,
            },
        ]
    );
}

export function apexClearAnnotations(element, refId) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    window.apexcharts[refId].clearAnnotations();
}

export function apexAddPointAnnotations(element, refId, annotations) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    for (var i = 0; i < annotations.length; i++) {
        window.apexcharts[refId].addPointAnnotation({
            x: annotations[i].x,
            y: annotations[i].y,
            marker: {
                size: 6,
                fillColor: "#fff",
                strokeColor: "#2698FF",
                radius: 2
            },
            label: {
                borderColor: "#FF4560",
                offsetY: 0,
                style: {
                    color: "#fff",
                    background: "#FF4560"
                },
                text: annotations[i].text
            }
        });
    }
}

export function apexAddYAxisAnnotations(element, refId, annotations) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    for (var i = 0; i < annotations.length; i++) {
        window.apexcharts[refId].addYaxisAnnotation({
            y: annotations[i].y,
            borderColor: "#00E396",
            opacity: 0.3,
            label: {
                borderColor: "#00E396",
                style: {
                    color: "#fff",
                    background: "#00E396"
                },
                text: annotations[i].text
            }
        });
    }
}

export function apexAddXAxisAnnotations(element, refId, annotations) {
    if (element == null) {
        console.error("element was null. Please define a reference for your Apex element.");
        return;
    }

    // Prepare chart element
    for (var i = 0; i < annotations.length; i++) {
        window.apexcharts[refId].addXaxisAnnotation({
            x: annotations[i].x,
            strokeDashArray: 0,
            borderColor: "#775DD0",
            opacity: 0.3,
            label: {
                borderColor: "#775DD0",
                style: {
                    color: "#fff",
                    background: "#775DD0"
                },
                text: annotations[i].text
            }
        });
    }
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