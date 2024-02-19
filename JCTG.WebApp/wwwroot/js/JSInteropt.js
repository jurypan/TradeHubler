window.charts = {};

export function initTradingView(element, refId, options) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	// Prepare chart element
	window.charts[refId] = LightweightCharts.createChart(element, {
		width: options.width > 0 ?
			options.width : 0,
		height: options.height > 0 ?
			options.height : 0,
		layout: {
			background: {
				color: options.layoutBackgroundColor,
			},
			textColor: options.layoutTextColor,
		},
		grid: {
			vertLines: {
				color: options.vertLinesColor,
			},
			horzLines: {
				color: options.horzLinesColor,
			},
		},
		crosshair: {
			mode: LightweightCharts.CrosshairMode.Normal,
		},
		rightPriceScale: {
			borderColor: options.rightPriceScaleBorderColor,
		},
		timeScale: {
			borderColor: options.timeScaleBorderColor,
			timeVisible: options.timeScaleTimeVisible,
			secondsVisible: options.timeScaleSecondsVisible
		}
	});

	// Force resize if applicable
	var timerID;
	if (options.width < 0) {
		// Set size on initial load
		window.charts[refId].resize(element.parentElement.offsetWidth - (options.width * -1), options.height);

		// Regular check
		document.body.onresize = function () {
			if (timerID) clearTimeout(timerID);
			timerID = setTimeout(function () {
				window.charts[refId].resize(element.parentElement.offsetWidth - (options.width * -1), options.height);
			}, 200);
		}
	}
}

export function addCandleStickSeries(element, refId, data, options) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"] = window.charts[refId].addCandlestickSeries({
		upColor: 'rgb(237, 213, 152)',
		downColor: 'rgb(247, 126, 126)',
		wickUpColor: 'rgb(237, 213, 152)',
		wickDownColor: 'rgb(247, 126, 126)',
		borderVisible: true,
		priceFormat: {
			type: 'price',
			precision: options.rightPriceScaleDecimalPrecision,
			minMove: 1 / (10 ** options.rightPriceScaleDecimalPrecision),
		}
	});
	window.charts[refId]["CandleSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateCandleStickSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["CandleSeries"] == null) {
		console.error("Lineseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateCandleStick(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["CandleSeries"] == null) {
		console.error("Lineseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"].update(data);
	window.charts[refId].timeScale().fitContent();
}

export function setMarkersToCandlestickSeriesAsync(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["CandleSeries"] == null) {
		console.error("Lineseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"].setMarkers(data);
}

export function addAreaSeries(element, refId, data, options) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["AreaSeries"] = window.charts[refId].addAreaSeries({
		lineColor: 'rgb(38,166,154)',
		topColor: '#2962FF',
		bottomColor: 'rgba(41, 98, 255, 0.28)',
		priceFormat: {
			type: 'price',
			precision: options.rightPriceScaleDecimalPrecision,
			minMove: 1 / (10 ** options.rightPriceScaleDecimalPrecision),
		}
	});
	window.charts[refId]["AreaSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateAreaSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["AreaSeries"] == null) {
		console.error("Areaseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["AreaSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function addLineSeries(element, refId, data, options) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["LineSeries"] = window.charts[refId].addLineSeries({
		color: '#00C',
		lineWidth: 2,
		lineStyle: options.lineStyle,
		axisLabelVisible: true,
		borderVisible: true,
		priceFormat: {
			type: 'price',
			precision: options.rightPriceScaleDecimalPrecision,
			minMove: 1 / (10 ** options.rightPriceScaleDecimalPrecision),
		}
	});
	window.charts[refId]["LineSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateLineSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["LineSeries"] == null) {
		console.error("Lineseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["LineSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function addVolumeSeries(element, refId, data, options) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["VolumeSeries"] = window.charts[refId].addHistogramSeries({
		color: '#26a69a',
		priceFormat: {
			type: 'volume',
		},
		priceScaleId: '',
		scaleMargins: {
			top: 0.8,
			bottom: 0,
		}
	});
	window.charts[refId]["VolumeSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateVolumeSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["VolumeSeries"] == null) {
		console.error("Volumeseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["VolumeSeries"].setData(data);
	window.charts[refId].timeScale().fitContent();
}

export function addMarkersToCandleStickSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["CandleSeries"] == null) {
		console.error("Candleseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"].setMarkers(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateMarkersToCandleStickSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["CandleSeries"] == null) {
		console.error("Candleseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["CandleSeries"].setMarkers(data);
	window.charts[refId].timeScale().fitContent();
}

export function addMarkersToLineSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["LineSeries"] == null) {
		console.error("Lineseries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["LineSeries"].setMarkers(data);
	window.charts[refId].timeScale().fitContent();
}

export function updateMarkersToLineSeries(element, refId, data) {
	if (element == null) {
		console.error("element was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	if (window.charts[refId]["LineSeries"] == null) {
		console.error("LineSeries collection was null. Please define a reference for your TradingViewChart element.");
		return;
	}

	window.charts[refId]["LineSeries"].setMarkers(data);
	window.charts[refId].timeScale().fitContent();
}