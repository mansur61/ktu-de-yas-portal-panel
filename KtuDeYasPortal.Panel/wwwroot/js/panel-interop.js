// ════════════════════════════════════════════════════════════════════════
//  panel-interop.js — KTU DeYas Admin Panel
// ════════════════════════════════════════════════════════════════════════

// ── Leaflet Harita ──────────────────────────────────────────────────────
let mapInstance = null;
let mapMarker   = null;

// İl koordinat merkez tablosu
const PROVINCE_COORDS = {
    "Artvin":      [41.18, 41.82], "Erzurum":  [39.91, 41.27],
    "Rize":        [41.02, 40.52], "Trabzon":  [41.00, 39.73],
    "Bayburt":     [40.26, 40.23], "Gümüşhane":[40.46, 39.48],
    "Giresun":     [40.91, 38.39], "Ordu":     [40.98, 37.88],
    "Samsun":      [41.29, 36.33], "Amasya":   [40.65, 35.83],
    "Tokat":       [40.31, 36.55], "Sivas":    [39.75, 37.02],
    "Erzincan":    [39.75, 39.49], "Kars":     [40.61, 43.10],
    "Ardahan":     [41.11, 42.70], "Iğdır":    [39.89, 44.05]
};

// İlçe koordinatları (bazı illerin ilçeleri — gerektiğinde genişletilebilir)
const DISTRICT_COORDS = {
    "Artvin": {
        "Yusufeli":  [40.82, 41.52], "Hopa": [41.41, 41.43],
        "Borçka":    [41.37, 41.67], "Ardanuç": [41.12, 42.06],
        "Arhavi":    [41.35, 41.30], "Murgul": [41.29, 41.60],
        "Şavşat":    [41.24, 42.36], "Merkez": [41.18, 41.82]
    },
    "Erzurum": {
        "Merkez":    [39.91, 41.27], "Oltu": [40.54, 41.99],
        "İspir":     [40.48, 40.99], "Tortum": [40.29, 41.54],
        "Pasinler":  [39.98, 41.67], "Uzundere": [40.24, 41.62]
    },
    "Rize": {
        "Merkez":    [41.02, 40.52], "Çamlıhemşin": [41.07, 40.90],
        "Ardeşen":   [41.19, 40.98], "Pazar": [41.18, 40.88],
        "Fındıklı":  [41.22, 40.93], "İkizdere": [40.79, 40.54],
        "Çayeli":    [41.09, 40.72], "Kalkandere": [40.88, 40.43]
    },
    "Trabzon": {
        "Merkez":    [41.00, 39.73], "Of": [40.94, 40.26],
        "Maçka":     [40.82, 39.62], "Sürmene": [40.91, 40.11],
        "Akçaabat":  [41.01, 39.56], "Araklı": [40.94, 40.13],
        "Çaykara":   [40.74, 40.20], "Tonya": [40.88, 39.27]
    }
};

function initMap(elementId, lat, lng) {
    const container = document.getElementById(elementId);
    if (!container) return;

    if (mapInstance) { mapInstance.remove(); mapInstance = null; mapMarker = null; }

    mapInstance = L.map(elementId, {
        center: [lat, lng], zoom: 10,
        zoomControl: true, attributionControl: false
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19 }).addTo(mapInstance);

    mapMarker = L.marker([lat, lng], { draggable: true }).addTo(mapInstance);

    mapMarker.on('dragend', function (e) {
        const pos = e.target.getLatLng();
        updateLatLngInputs(pos.lat, pos.lng);
        reverseGeocode(pos.lat, pos.lng);
    });

    mapInstance.on('click', function (e) {
        mapMarker.setLatLng(e.latlng);
        updateLatLngInputs(e.latlng.lat, e.latlng.lng);
        reverseGeocode(e.latlng.lat, e.latlng.lng);
    });
}

// İl seçilince harita o ilin merkezine gider
function panToProvince(provinceName) {
    const coords = PROVINCE_COORDS[provinceName];
    if (!coords || !mapInstance) return;
    mapInstance.setView(coords, 10, { animate: true });
    if (mapMarker) mapMarker.setLatLng(coords);
    updateLatLngInputs(coords[0], coords[1]);
}

// İl + ilçe seçilince harita o ilçenin merkezine gider
function panToDistrict(provinceName, districtName) {
    const districtMap = DISTRICT_COORDS[provinceName];
    if (districtMap && districtMap[districtName]) {
        const coords = districtMap[districtName];
        if (!mapInstance) return;
        mapInstance.setView(coords, 13, { animate: true });
        if (mapMarker) mapMarker.setLatLng(coords);
        updateLatLngInputs(coords[0], coords[1]);
    } else {
        // İlçe koordinatı yoksa il merkezine git
        panToProvince(provinceName);
    }
}

// Marker tıklanınca reverse geocode ile adres al
function reverseGeocode(lat, lng) {
    fetch(`https://nominatim.openstreetmap.org/reverse?format=json&lat=${lat}&lon=${lng}&accept-language=tr`)
        .then(r => r.json())
        .then(data => {
            const addr = data.address;
            // Adres alanını doldur
            const addrInput = document.getElementById('address-input');
            if (addrInput) {
                const parts = [
                    addr.road, addr.neighbourhood,
                    addr.suburb, addr.village, addr.town,
                    addr.city_district, addr.city
                ].filter(Boolean);
                addrInput.value = parts.slice(0, 4).join(', ');
                addrInput.dispatchEvent(new Event('change', { bubbles: true }));
            }
        })
        .catch(() => { /* offline veya hız sınırı — sessiz geç */ });
}

function setMapPosition(lat, lng) {
    if (mapInstance && mapMarker) {
        mapMarker.setLatLng([lat, lng]);
        mapInstance.setView([lat, lng], mapInstance.getZoom());
    }
}

function updateLatLngInputs(lat, lng) {
    const latInput = document.getElementById('lat-input');
    const lngInput = document.getElementById('lng-input');
    if (latInput) { latInput.value = lat.toFixed(6); latInput.dispatchEvent(new Event('change', { bubbles: true })); }
    if (lngInput) { lngInput.value = lng.toFixed(6); lngInput.dispatchEvent(new Event('change', { bubbles: true })); }
}

function destroyMap() {
    if (mapInstance) { mapInstance.remove(); mapInstance = null; mapMarker = null; }
}

// ── Yapı Görsel Overlay ─────────────────────────────────────────────────
let _currentContainer = null;
let _currentSensors   = [];
let pendingClickPos   = null;

function initStructureImage(elementId, imageUrl, sensors) {
    const container = document.getElementById(elementId);
    if (!container) return;

    _currentContainer = container;
    _currentSensors   = sensors || [];
    pendingClickPos   = null;

    container.innerHTML  = '';
    container.style.cssText = 'position:relative;overflow:hidden;';

    if (!imageUrl) {
        // Görsel yoksa placeholder
        container.style.display = 'flex';
        container.style.alignItems = 'center';
        container.style.justifyContent = 'center';
        container.style.minHeight = '300px';
        container.style.background = '#1a1a2e';
        container.innerHTML = '<div style="text-align:center;color:#6c757d"><span style="font-size:3rem">🏗️</span><p style="margin-top:8px;font-size:0.9rem">Görsel tanımlanmamış</p></div>';
        container.onclick = null;
        return;
    }

    container.style.display = '';

    const img = document.createElement('img');
    img.src              = imageUrl;
    img.className        = 'structure-image-full';
    img.draggable        = false;
    img.alt              = 'Yapı görseli';
    container.appendChild(img);

    // Tıklama hint
    const hint = document.createElement('div');
    hint.className = 'structure-image-click-hint';
    hint.innerHTML = '<span class="bi bi-plus-circle me-1"></span>Resme tıklayarak sensör ekleyin';
    container.appendChild(hint);

    container.onclick = function (e) {
        if (e.target.closest('.sensor-marker')) return;
        const rect = container.getBoundingClientRect();
        const x = ((e.clientX - rect.left) / rect.width)  * 100;
        const y = ((e.clientY - rect.top)  / rect.height) * 100;
        pendingClickPos = {
            x: Math.round(x * 10) / 10,
            y: Math.round(y * 10) / 10
        };
    };

    renderSensorMarkers(container, _currentSensors);
}

function getHasPendingClickPosition()  { return pendingClickPos !== null; }
function getPendingClickPositionX()    { return pendingClickPos ? pendingClickPos.x : 0; }
function getPendingClickPositionY()    { return pendingClickPos ? pendingClickPos.y : 0; }
function clearPendingClickPosition()   { pendingClickPos = null; }

function renderSensorMarkers(container, sensors) {
    // Eski marker'ları sil
    container.querySelectorAll('.sensor-marker').forEach(m => m.remove());

    if (!sensors || !container) return;

    sensors.forEach(s => {
        if (s.imagePositionX == null || s.imagePositionY == null) return;

        const marker = document.createElement('div');
        marker.className          = 'sensor-marker';
        marker.title              = s.name || s.deviceId;
        marker.dataset.sensorId   = s.id;
        marker.style.left         = s.imagePositionX + '%';
        marker.style.top          = s.imagePositionY + '%';

        // Durum rengi
        const statusClass = {
            Online: 'sensor-marker-online',
            Warning: 'sensor-marker-warning',
            Alarm: 'sensor-marker-alarm',
            Offline: 'sensor-marker-offline'
        }[s.status] || 'sensor-marker-offline';
        marker.classList.add(statusClass);

        // Tip ikonu
        marker.textContent = _sensorIcon(s.sensorType);

        marker.onclick = function (e) {
            e.stopPropagation();
            _showSensorDetailPopup(s, e);
        };

        container.appendChild(marker);
    });
}

function updateSensorMarkerOnImage(sensorData) {
    if (!_currentContainer) return;
    const marker = _currentContainer.querySelector(`[data-sensor-id="${sensorData.sensorId}"]`);
    if (!marker) return;

    marker.className = 'sensor-marker';
    const statusClass = {
        Online: 'sensor-marker-online',
        Warning: 'sensor-marker-warning',
        Alarm: 'sensor-marker-alarm',
        Offline: 'sensor-marker-offline'
    }[sensorData.status] || 'sensor-marker-offline';
    marker.classList.add(statusClass);
}

function _sensorIcon(type) {
    const icons = {
        temperature:   '🌡️',
        humidity:      '💧',
        pressure:      '🔽',
        vibration:     '📳',
        accelerometer: '📐',
        acceleration:  '📐',
        camera:        '📷',
        image:         '🖼️',
        lidar:         '📡',
        ultrasonic:    '🔊',
        gps:           '📍',
        wind:          '💨',
        water:         '🌊',
        strain:        '📏',
        tilt:          '📐',
        generic:       '🔘'
    };
    return icons[(type || '').toLowerCase()] || icons.generic;
}

// ── Sensör Detay Popup ──────────────────────────────────────────────────
let _activeSensorPopup = null;

function _showSensorDetailPopup(sensor, event) {
    if (_activeSensorPopup) { _activeSensorPopup.remove(); _activeSensorPopup = null; }

    const statusColors  = { Online:'#28a745', Warning:'#ffc107', Alarm:'#dc3545', Offline:'#6c757d' };
    const statusLabels  = { Online:'Çevrimiçi', Warning:'Uyarı', Alarm:'Alarm', Offline:'Çevrimdışı' };
    const color = statusColors[sensor.status] || '#6c757d';

    const popup = document.createElement('div');
    popup.className = 'sensor-popup';
    popup.innerHTML = `
        <div class="sensor-popup-header" style="background:${color}">
            <span>${_sensorIcon(sensor.sensorType)} ${sensor.name || sensor.deviceId}</span>
            <button class="sensor-popup-close" onclick="this.closest('.sensor-popup').remove()">&times;</button>
        </div>
        <div class="sensor-popup-body">
            ${_popupRow('Cihaz ID', `<code>${sensor.deviceId || '-'}</code>`)}
            ${_popupRow('Sensör Tipi', sensor.sensorType || '-')}
            ${_popupRow('Topic', sensor.topic ? `<code>${sensor.topic}</code>` : '-')}
            ${_popupRow('Birim', sensor.unit || '-')}
            ${_popupRow('Durum', `<span style="color:${color};font-weight:700">${statusLabels[sensor.status] || sensor.status}</span>`)}
            ${_popupRow('Son Değer', sensor.lastValue != null ? `<strong>${Number(sensor.lastValue).toFixed(2)}${sensor.unit ? ' ' + sensor.unit : ''}</strong>` : '-')}
            ${_popupRow('Son Güncelleme', sensor.lastUpdated ? new Date(sensor.lastUpdated).toLocaleString('tr-TR') : '-')}
            ${sensor.alertMessage ? _popupRow('Alarm', `<span style="color:#dc3545">${sensor.alertMessage}</span>`) : ''}
        </div>`;

    document.body.appendChild(popup);
    _activeSensorPopup = popup;

    // Konumlandır
    const pw = 340, ph = 260;
    let x = event.clientX + 14;
    let y = event.clientY - 10;
    if (x + pw > window.innerWidth)  x = event.clientX - pw - 14;
    if (y + ph > window.innerHeight) y = window.innerHeight - ph - 14;
    popup.style.left = Math.max(8, x) + 'px';
    popup.style.top  = Math.max(8, y) + 'px';

    // Dışarı tıklayınca kapat
    setTimeout(() => {
        document.addEventListener('click', function _close(e) {
            if (!popup.contains(e.target)) { popup.remove(); document.removeEventListener('click', _close); }
        });
    }, 10);
}

function _popupRow(label, value) {
    return `<div class="sensor-popup-row"><span class="label">${label}</span><span class="value">${value}</span></div>`;
}

// ── SignalR ─────────────────────────────────────────────────────────────
let signalRConn = null;

function connectSensorHub() {
    if (signalRConn && signalRConn.state === 'Connected') return Promise.resolve();

    signalRConn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/sensor')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    signalRConn.on('SensorStatusChanged', data => updateSensorMarkerOnImage(data));

    return signalRConn.start()
        .then(() => console.info('[Panel] SignalR connected'))
        .catch(err => console.warn('[Panel] SignalR failed:', err));
}

function joinStructureGroup(structureId) {
    if (signalRConn?.state === 'Connected')
        signalRConn.invoke('JoinStructureGroup', structureId).catch(() => {});
}

function leaveStructureGroup(structureId) {
    if (signalRConn?.state === 'Connected')
        signalRConn.invoke('LeaveStructureGroup', structureId).catch(() => {});
}

function disconnectSensorHub() {
    if (signalRConn) { signalRConn.stop(); signalRConn = null; }
}
