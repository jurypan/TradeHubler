// -------------------------------------------------------------------------------------------------------------------------------------------
// Dashboard 5 : Chart Init Js
// -------------------------------------------------------------------------------------------------------------------------------------------
$(function () {
  "use strict";

  // earnings
  var options_earnings_box = {
    series: [
      {
        name: "Earnings",
        data: [5, 6, 5, 7, 9, 10, 14, 12, 11, 9, 8, 7, 10, 6, 12, 10, 8],
      },
    ],
    chart: {
      height: 75,
      type: "area",
      fontFamily: "inherit",
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
    colors: ["#40c4ff"],
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
  };

  var chart_line_earn_box = new ApexCharts(
    document.querySelector(".earnings-month"),
    options_earnings_box
  );
  chart_line_earn_box.render();

  // day chart
  var options_product = {
    series: [
      {
        name: "Sales ",
        data: [5, 6, 3, 7, 9, 10, 14, 12, 11, 9, 8, 7, 10, 6, 12, 10, 8],
      },
      {
        name: "Sales ",
        data: [1, 2, 8, 3, 4, 5, 7, 6, 5, 6, 4, 3, 3, 12, 5, 6, 3],
      },
    ],
    chart: {
      fontFamily: "inherit",
      type: "bar",
      height: 400,
      toolbar: {
        show: false,
      },
    },
    grid: {
      show: false,
    },
    colors: ["var(--bs-primary)", "var(--bs-info)", "#f62d51"],
    plotOptions: {
      bar: {
        horizontal: false,
        columnWidth: "30%",
        borderRadius: [6],
        borderRadiusApplication: "end",
        borderRadiusWhenStacked: "all"
      },
    },
    dataLabels: {
      enabled: false,
    },
    stroke: {
      show: true,
      width: 0,
      colors: ["transparent"],
    },
    xaxis: {

      type: "category",
      categories: [
        "0",
        "",
        "2",
        "",
        "4",
        "",
        "6",
        "",
        "8",
        "",
        "10",
        "",
        "12",
        "",
        "14",
        "",
        "16",
      ],
      tickAmount: "16",
      axisBorder: {
        show: false,
      },
      tickPlacement: "on",
      axisTicks: {
        show: true,
        borderType: "solid",
        color: "#adb0bb",
        height: 6,
        offsetX: 0,
        offsetY: 0,
      },
      labels: {
        style: {
          colors: [
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
            "#adb0bb",
          ],
        },
      },
    },
    yaxis: {
      axisBorder: {
        show: false,
      },
      labels: {
        style: {
          colors: "#adb0bb",
        },
      },
    },
    fill: {
      opacity: 1,
    },
    tooltip: {
      theme: "dark",
    },
    legend: {
      show: false,
    },
  };

  var chart_column_basic = new ApexCharts(
    document.querySelector(".day-chart"),
    options_product
  );
  chart_column_basic.render();

  // -----------------------------------------------------------------------
  // week chart
  // -----------------------------------------------------------------------
  var options_week_chart = {
    chart: {
      height: 300,
      type: "radialBar",
      fontFamily: "inherit",
      sparkline: {
        enabled: true,
      },
    },
    series: [85],
    colors: ["var(--bs-primary)"],
    fill: {
      opacity: 1,
    },
    plotOptions: {
      radialBar: {
        hollow: {
          margin: 15,
          size: "85%",
        },
        track: {
          show: true,
          background: "#dadada",
          opacity: 1,
        },
        dataLabels: {
          showOn: "always",
          name: {
            show: true,
            offsetY: 10,
          },
          value: {
            show: false,
          },
          total: {
            show: true,
            fontSize: "13px",
            fontWeight: 300,
            color: "#adb0bb",
            label: "Weekly",
          },
        },
      },
    },
    responsive: [
      {
        breakpoint: 1200,
        options: {
          height: 80,
        },
      },
    ],
    stroke: {
      lineCap: "sqare",
    },
    tooltip: {
      enabled: true,
      theme: "dark",
      fillSeriesColor: false,
    },
  };

  var chart_radial = new ApexCharts(
    document.querySelector(".week-chart"),
    options_week_chart
  );
  chart_radial.render();

  // -----------------------------------------------------------------------
  // month chart
  // -----------------------------------------------------------------------
  var option_month_chart = {
    series: [45, 27, 18, 15],
    labels: ["Email", "Mobile", "Other", "Website"],
    chart: {
      type: "donut",
      height: 300,
      fontFamily: "inherit",
    },
    dataLabels: {
      enabled: false,
    },
    stroke: {
      width: 0,
    },
    plotOptions: {
      pie: {
        expandOnClick: true,
        donut: {
          size: "80",
          labels: {
            show: true,
            name: {
              show: true,
              offsetY: 10,
            },
            value: {
              show: false,
            },
            total: {
              show: true,
              color: "#adb0bb",
              fontSize: "13px",
              label: "Revenue",
            },
          },
        },
      },
    },
    colors: ["var(--bs-info)", "#ff821c", "#7e74fb", "var(--bs-primary)"],
    tooltip: {
      show: true,
      fillSeriesColor: false,
    },
    legend: {
      show: true,
      position: "bottom",
      labels: {
        colors: "#adb0bb",
      },
      markers: {
        radius: 0,
      },
    },
    responsive: [
      {
        breakpoint: 480,
        options: {
          chart: {
            width: 200,
          },
        },
      },
    ],
  };

  var chart_pie_donut = new ApexCharts(
    document.querySelector(".month-chart"),
    option_month_chart
  );
  chart_pie_donut.render();

  // -----------------------------------------------------------------------
  // order stats
  // -----------------------------------------------------------------------
  var order_stats = {
    series: [65, 17, 15],
    labels: ["Pending", "Failed", "Success"],
    chart: {
      type: "donut",
      fontFamily: "inherit",
      height: 250,
    },
    dataLabels: {
      enabled: false,
    },
    stroke: {
      width: 0,
    },
    plotOptions: {
      pie: {
        expandOnClick: true,
        donut: {
          size: "75",
          labels: {
            show: true,
            name: {
              show: true,
              offsetY: 10,
            },
            value: {
              show: false,
            },
            total: {
              show: true,
              color: "#adb0bb",
              fontSize: "13px",
              label: "Orders",
            },
          },
        },
      },
    },
    colors: ["var(--bs-info)", "#fb8c00", "var(--bs-primary)"],
    tooltip: {
      show: true,
      fillSeriesColor: false,
    },
    legend: {
      show: false,
    },
    responsive: [
      {
        breakpoint: 480,
        options: {
          chart: {
            width: 200,
          },
        },
      },
    ],
  };

  var chart_pie_donut_order_stats = new ApexCharts(
    document.querySelector(".order-stats"),
    order_stats
  );
  chart_pie_donut_order_stats.render();
});
