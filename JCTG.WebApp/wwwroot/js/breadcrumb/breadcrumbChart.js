$(function () {
    "use strict";
  
    // -----------------------------------------------------------------------
    // breacrumb chart
    // -----------------------------------------------------------------------
    var breadbar = {
      series: [
        {
          name: "",
          data: [5, 10, 8, 12, 7, 5, 15],
        },
      ],
      chart: {
        type: "bar",
        width: 60,
        height: 40,
        toolbar: {
          show: false,
        },
        sparkline: {
          enabled: true,
        },
      },
      colors: ["var(--bs-primary)"],
      grid: {
        show: false,
      },
      plotOptions: {
        bar: {
          horizontal: false,
          borderRadius:2,
          columnWidth: "50%",
          barHeight: "100%",
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
        axisBorder: {
          show: false,
        },
        axisTicks: {
          show: false,
        },
        labels: {
          show: false,
        },
      },
      yaxis: {
        labels: {
          show: false,
        },
      },
      axisBorder: {
        show: false,
      },
      fill: {
        opacity: 1,
      },
      tooltip: {
        theme: "dark",
        style: {
          fontFamily: "inherit",
        },
        x: {
          show: false,
        },
        y: {
          formatter: undefined,
        },
      },
    };
    var chart_column_breadbar = new ApexCharts(
      document.querySelector(".breadbar"),
      breadbar
    );
    chart_column_breadbar.render();
  
  });
  