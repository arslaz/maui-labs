using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using laba12.Models;
using laba12.Services;

namespace laba12.ViewModels;

public partial class RouteMapViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IRoutingService _routingService;
    private readonly ITelemetryService _telemetryService;
    private readonly ICacheService _cacheService;
    private readonly ISecureStorageService _secureStorageService;
    private readonly IDeviceLocationService _locationService;
    private RouteInfo? _passedRoute;

    [ObservableProperty]
    private string mapHtml = string.Empty;

    [ObservableProperty]
    private string routeStatsText = "Маршрут загружается...";

    [ObservableProperty]
    private string routeDistance = "0 км";

    [ObservableProperty]
    private string routeDuration = "0 мин";

    [ObservableProperty]
    private int waypointCount = 0;

    [ObservableProperty]
    private string gpsStatusText = "GPS: ожидание координат";

    [ObservableProperty]
    private List<string> routeInstructions = new();

    public RouteMapViewModel(
        IRoutingService routingService,
        ITelemetryService telemetryService,
        ICacheService cacheService,
        ISecureStorageService secureStorageService,
        IDeviceLocationService locationService)
    {
        _routingService = routingService;
        _telemetryService = telemetryService;
        _cacheService = cacheService;
        _secureStorageService = secureStorageService;
        _locationService = locationService;

        Title = "Карта и навигация OSRM";
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("CalculatedRoute", out var r) && r is RouteInfo route)
        {
            _passedRoute = route;
        }
    }

    public async Task InitializeMapAsync(RouteInfo? precomputedRoute = null)
    {
        IsBusy = true;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(15));

        try
        {
            var driverId = await _secureStorageService.GetDriverIdAsync() ?? "DRV-104";
            var points = await _cacheService.GetCachedDeliveriesAsync(driverId) ?? new List<DeliveryPoint>();

            var routeToUse = precomputedRoute ?? _passedRoute;
            double startLat;
            double startLon;

            if (routeToUse?.StartPoint != null)
            {
                startLat = routeToUse.StartPoint.Latitude;
                startLon = routeToUse.StartPoint.Longitude;
            }
            else
            {
                // Query real device position directly
                var loc = await _locationService.GetCurrentLocationAsync(token);
                startLat = loc.Latitude;
                startLon = loc.Longitude;
                _telemetryService.UpdateDriverPosition(startLat, startLon);
            }

            GpsStatusText = $"GPS: {startLat:F4}, {startLon:F4}";

            RouteInfo route = routeToUse ?? await _routingService.CalculateRouteAsync(startLat, startLon, points, token);

            if (route.IsSuccess)
            {
                RouteDistance = route.FormattedDistance;
                RouteDuration = route.FormattedDuration;
                WaypointCount = points.Count;
                RouteStatsText = $"Дистанция: {RouteDistance} • Время в пути: ~{RouteDuration}";
                RouteInstructions = route.Instructions;

                // Generate Leaflet OpenStreetMap HTML
                MapHtml = GenerateLeafletHtml(startLat, startLon, points, route);
            }
            else
            {
                ErrorMessage = route.ErrorMessage ?? "Не удалось построить гео-маршрут.";
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Расчет маршрута прерван.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка карты: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task LocateMeAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var token = CreateLinkedToken(TimeSpan.FromSeconds(10));

        try
        {
            var driverId = await _secureStorageService.GetDriverIdAsync() ?? "DRV-104";
            var points = await _cacheService.GetCachedDeliveriesAsync(driverId) ?? new List<DeliveryPoint>();

            // Query physical location
            var loc = await _locationService.GetCurrentLocationAsync(token);
            _telemetryService.UpdateDriverPosition(loc.Latitude, loc.Longitude);

            GpsStatusText = $"GPS: {loc.Latitude:F4}, {loc.Longitude:F4}";

            var route = await _routingService.CalculateRouteAsync(loc.Latitude, loc.Longitude, points, token);
            if (route.IsSuccess)
            {
                _passedRoute = route;
                RouteDistance = route.FormattedDistance;
                RouteDuration = route.FormattedDuration;
                WaypointCount = points.Count;
                RouteStatsText = $"Дистанция: {RouteDistance} • Время в пути: ~{RouteDuration}";
                RouteInstructions = route.Instructions;
                MapHtml = GenerateLeafletHtml(loc.Latitude, loc.Longitude, points, route);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Не удалось определить GPS: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ResetCancellation();
        }
    }

    [RelayCommand]
    public async Task RecalculateRouteAsync()
    {
        await InitializeMapAsync(precomputedRoute: null);
    }

    [RelayCommand]
    public async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    private string GenerateLeafletHtml(double driverLat, double driverLon, List<DeliveryPoint> points, RouteInfo route)
    {
        var polylinePointsJson = JsonSerializer.Serialize(
            route.Geometry.Select(g => new[] { g.Latitude, g.Longitude })
        );

        var markersJson = JsonSerializer.Serialize(
            points.Select((p, idx) => new
            {
                index = idx + 1,
                lat = p.Latitude,
                lon = p.Longitude,
                order = p.OrderNumber,
                client = p.ClientName,
                address = p.Address,
                cargo = p.CargoDescription,
                status = p.Status
            })
        );

        var dLat = driverLat.ToString("F6", CultureInfo.InvariantCulture);
        var dLon = driverLon.ToString("F6", CultureInfo.InvariantCulture);

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"" />
    <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
    <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
    <style>
        html, body, #map {{
            height: 100%;
            width: 100%;
            margin: 0;
            padding: 0;
            background: #0f172a;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            overflow: hidden;
        }}
        .driver-marker {{
            background: #2563eb;
            color: #ffffff;
            border: 3px solid #ffffff;
            border-radius: 50%;
            width: 36px;
            height: 36px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 18px;
            box-shadow: 0 4px 14px rgba(37,99,235,0.6);
            cursor: grab;
            transition: transform 0.2s;
        }}
        .driver-marker:active {{
            cursor: grabbing;
            transform: scale(1.15);
        }}
        .delivery-marker {{
            border-radius: 50%;
            box-shadow: 0 4px 10px rgba(0,0,0,0.4);
        }}
        .custom-popup .leaflet-popup-content-wrapper {{
            background: #1e293b;
            color: #f8fafc;
            border-radius: 12px;
            box-shadow: 0 10px 25px rgba(0,0,0,0.4);
            border: 1px solid #334155;
        }}
        .custom-popup .leaflet-popup-tip {{
            background: #1e293b;
        }}
        .popup-title {{
            font-size: 14px;
            font-weight: 700;
            color: #38bdf8;
            margin-bottom: 4px;
        }}
        .popup-client {{
            font-size: 13px;
            font-weight: 600;
            color: #ffffff;
        }}
        .popup-desc {{
            font-size: 12px;
            color: #94a3b8;
            margin-top: 4px;
        }}
        /* Floating controls bar */
        .map-floating-bar {{
            position: absolute;
            bottom: 24px;
            right: 16px;
            z-index: 1000;
            display: flex;
            flex-direction: column;
            gap: 10px;
        }}
        .floating-btn {{
            background: #2563eb;
            color: white;
            border: none;
            border-radius: 28px;
            padding: 10px 16px;
            font-size: 13px;
            font-weight: 700;
            box-shadow: 0 4px 12px rgba(0,0,0,0.35);
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 6px;
            transition: all 0.2s ease;
        }}
        .floating-btn:hover {{
            background: #1d4ed8;
            transform: translateY(-2px);
        }}
        .floating-btn:active {{
            transform: scale(0.96);
        }}
        .btn-green {{
            background: #059669;
        }}
        .btn-green:hover {{
            background: #047857;
        }}
        /* Top GPS indicator pill */
        .gps-pill {{
            position: absolute;
            top: 14px;
            left: 50%;
            transform: translateX(-50%);
            z-index: 1000;
            background: rgba(15, 23, 42, 0.88);
            backdrop-filter: blur(8px);
            color: #38bdf8;
            padding: 6px 14px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
            border: 1px solid rgba(56, 189, 248, 0.3);
            box-shadow: 0 4px 15px rgba(0,0,0,0.3);
            pointer-events: none;
            white-space: nowrap;
        }}
        /* Toast notification */
        .toast-msg {{
            position: absolute;
            bottom: 80px;
            left: 50%;
            transform: translateX(-50%);
            z-index: 1001;
            background: #1e293b;
            color: #f8fafc;
            padding: 8px 18px;
            border-radius: 24px;
            font-size: 12px;
            border: 1px solid #3b82f6;
            box-shadow: 0 6px 20px rgba(0,0,0,0.4);
            opacity: 0;
            transition: opacity 0.3s ease;
            pointer-events: none;
        }}
        .toast-msg.show {{
            opacity: 1;
        }}
    </style>
</head>
<body>
    <div id=""map""></div>
    <div id=""gpsPill"" class=""gps-pill"">📍 Машина: {dLat}, {dLon}</div>
    <div id=""toast"" class=""toast-msg"">Определение геолокации...</div>

    <div class=""map-floating-bar"">
        <button id=""btnLocate"" class=""floating-btn btn-green"" onclick=""locateUser(true)"">
            <span>🎯</span> Моё местоположение
        </button>
        <button class=""floating-btn"" onclick=""zoomToDeliveries()"">
            <span>🇧🇾</span> К складам РБ
        </button>
    </div>

    <script>
        var currentDriverLat = {dLat};
        var currentDriverLon = {dLon};
        var deliveries = {markersJson};
        var routeCoordinates = {polylinePointsJson};

        var map = L.map('map', {{ zoomControl: true }}).setView([currentDriverLat, currentDriverLon], 12);

        // OpenStreetMap standard tiles
        L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
            maxZoom: 19,
            attribution: '© OpenStreetMap | OSRM Routing'
        }}).addTo(map);

        // Driver Truck Marker (draggable: true - user can drag to their exact street/spot)
        var driverIcon = L.divIcon({{
            className: 'driver-marker',
            html: '🚛',
            iconSize: [36, 36],
            iconAnchor: [18, 18]
        }});

        var driverMarker = L.marker([currentDriverLat, currentDriverLon], {{
            icon: driverIcon,
            draggable: true
        }}).addTo(map);

        driverMarker.bindPopup('<div class=""custom-popup""><div class=""popup-title"">🚛 Текущая позиция ТС</div><div class=""popup-client"">Перетащите маркер или нажмите 🎯 для GPS</div></div>');

        // Dragend listener: recalculates OSRM route in real time
        driverMarker.on('dragend', function(e) {{
            var p = e.target.getLatLng();
            recalculateRoute(p.lat, p.lng);
        }});

        // Route Polyline from OSRM
        var polyline = L.polyline(routeCoordinates, {{
            color: '#2563eb',
            weight: 5,
            opacity: 0.85,
            lineJoin: 'round'
        }}).addTo(map);

        // Delivery Waypoints in Belarus
        var deliveryBounds = [];
        deliveries.forEach(function(item) {{
            var pinColor = item.status === 'Доставлен' ? '#10b981' : (item.status === 'В пути' ? '#3b82f6' : '#f59e0b');
            var pinIcon = L.divIcon({{
                className: 'delivery-marker',
                html: '<div style=""background:' + pinColor + '; border-radius:50%; width:26px; height:26px; display:flex; align-items:center; justify-content:center; color:white; font-size:12px; font-weight:bold; border:2px solid white; box-shadow:0 3px 8px rgba(0,0,0,0.35);"">' + item.index + '</div>',
                iconSize: [28, 28],
                iconAnchor: [14, 14]
            }});

            var marker = L.marker([item.lat, item.lon], {{ icon: pinIcon }}).addTo(map);
            var popupContent = '<div class=""custom-popup"">' +
                '<div class=""popup-title"">#' + item.index + ' ' + item.order + ' (' + item.status + ')</div>' +
                '<div class=""popup-client"">' + item.client + '</div>' +
                '<div class=""popup-desc"">' + item.address + '<br/>Груз: ' + item.cargo + '</div>' +
                '</div>';
            marker.bindPopup(popupContent);
            deliveryBounds.push([item.lat, item.lon]);
        }});

        function fitWholeRoute() {{
            var allBounds = [[currentDriverLat, currentDriverLon]].concat(deliveryBounds);
            if (allBounds.length > 1) {{
                map.fitBounds(allBounds, {{ padding: [50, 50] }});
            }}
        }}

        fitWholeRoute();

        // Click on map to place truck
        map.on('click', function(e) {{
            recalculateRoute(e.latlng.lat, e.latlng.lng);
        }});

        function zoomToDeliveries() {{
            if (deliveryBounds.length > 0) {{
                map.fitBounds(deliveryBounds, {{ padding: [40, 40] }});
                showToast('Показаны склады в Беларуси');
            }}
        }}

        function showToast(text) {{
            var t = document.getElementById('toast');
            if (t) {{
                t.innerText = text;
                t.classList.add('show');
                setTimeout(function() {{
                    t.classList.remove('show');
                }}, 3000);
            }}
        }}

        // Dynamic OSRM recalculation client-side
        function recalculateRoute(newLat, newLon) {{
            currentDriverLat = newLat;
            currentDriverLon = newLon;
            driverMarker.setLatLng([newLat, newLon]);

            var pill = document.getElementById('gpsPill');
            if (pill) {{
                pill.innerText = '📍 Позиция: ' + newLat.toFixed(4) + ', ' + newLon.toFixed(4);
            }}

            showToast('🔄 Пересчет маршрута OSRM...');

            var coords = [newLon.toFixed(6) + ',' + newLat.toFixed(6)];
            deliveries.forEach(function(d) {{
                coords.push(d.lon.toFixed(6) + ',' + d.lat.toFixed(6));
            }});

            var url = 'https://router.project-osrm.org/route/v1/driving/' + coords.join(';') + '?overview=full&geometries=geojson&steps=true';

            fetch(url)
                .then(function(res) {{ return res.json(); }})
                .then(function(data) {{
                    if (data && data.routes && data.routes.length > 0) {{
                        var r = data.routes[0];
                        var points = r.geometry.coordinates.map(function(c) {{ return [c[1], c[0]]; }});

                        if (polyline) {{
                            map.removeLayer(polyline);
                        }}

                        polyline = L.polyline(points, {{
                            color: '#2563eb',
                            weight: 5,
                            opacity: 0.85,
                            lineJoin: 'round'
                        }}).addTo(map);

                        var km = (r.distance / 1000).toFixed(1);
                        var mins = Math.round(r.duration / 60);
                        showToast('✅ Маршрут: ' + km + ' км (~' + mins + ' мин)');
                    }}
                }})
                .catch(function(err) {{
                    console.error('OSRM route fetch failed:', err);
                    showToast('⚠️ Ошибка гео-сервера OSRM');
                }});
        }}

        // HTML5 Geolocation function
        function locateUser(recenter) {{
            var btn = document.getElementById('btnLocate');
            if (btn) btn.innerText = '⏳ Поиск GPS...';
            showToast('Запрос геопозиции GPS...');

            if (navigator.geolocation) {{
                navigator.geolocation.getCurrentPosition(
                    function(pos) {{
                        var userLat = pos.coords.latitude;
                        var userLon = pos.coords.longitude;
                        if (btn) btn.innerHTML = '<span>🎯</span> Моё местоположение';
                        showToast('🎯 GPS найден: ' + userLat.toFixed(4) + ', ' + userLon.toFixed(4));
                        recalculateRoute(userLat, userLon);
                        if (recenter) {{
                            map.flyTo([userLat, userLon], 14, {{ duration: 1.2 }});
                        }}
                    }},
                    function(err) {{
                        console.warn('HTML5 Geolocation failed:', err.message);
                        // Fallback: try IP geolocation
                        fetch('https://ipapi.co/json/')
                            .then(function(r) {{ return r.json(); }})
                            .then(function(data) {{
                                if (data && data.latitude && data.longitude) {{
                                    if (btn) btn.innerHTML = '<span>🎯</span> Моё местоположение';
                                    showToast('🌐 Позиция по сети: ' + data.city);
                                    recalculateRoute(data.latitude, data.longitude);
                                    if (recenter) map.flyTo([data.latitude, data.longitude], 12);
                                }} else {{
                                    if (btn) btn.innerHTML = '<span>🎯</span> Моё местоположение';
                                    showToast('⚠️ GPS недоступен. Перетащите маркер 🚛');
                                }}
                            }})
                            .catch(function() {{
                                if (btn) btn.innerHTML = '<span>🎯</span> Моё местоположение';
                                showToast('⚠️ GPS недоступен. Перетащите маркер 🚛');
                            }});
                    }},
                    {{
                        enableHighAccuracy: true,
                        timeout: 8000,
                        maximumAge: 0
                    }}
                );
            }} else {{
                if (btn) btn.innerHTML = '<span>🎯</span> Моё местоположение';
                showToast('Браузер не поддерживает Geolocation');
            }}
        }}

        // Standard map behavior: automatically auto-locate on initial page load
        setTimeout(function() {{
            locateUser(false);
        }}, 400);
    </script>
</body>
</html>";
    }
}
