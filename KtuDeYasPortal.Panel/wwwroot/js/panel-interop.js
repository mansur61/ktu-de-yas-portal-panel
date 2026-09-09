window.panelDownloadBase64 = function (fileName, contentType, base64) {
    const bytes = Uint8Array.from(atob(base64), character => character.charCodeAt(0));
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
};
// ════════════════════════════════════════════════════════════════════════
//  panel-interop.js — KTU DeYas Admin Panel
// ════════════════════════════════════════════════════════════════════════

// ── Leaflet Harita ──────────────────────────────────────────────────────
let mapInstance = null;
let mapMarker   = null;

// İl koordinat merkez tablosu
const PROVINCE_COORDS = {
    "Adana":[37.00,35.32],"Adıyaman":[37.76,38.28],"Afyonkarahisar":[38.76,30.54],"Ağrı":[39.72,43.05],"Aksaray":[38.37,34.03],"Amasya":[40.65,35.83],"Ankara":[39.93,32.86],"Antalya":[36.89,30.70],"Ardahan":[41.11,42.70],"Artvin":[41.18,41.82],"Aydın":[37.85,27.84],"Balıkesir":[39.65,27.89],"Bartın":[41.63,32.34],"Batman":[37.88,41.13],"Bayburt":[40.26,40.23],"Bilecik":[40.15,29.98],"Bingöl":[38.89,40.50],"Bitlis":[38.40,42.11],"Bolu":[40.73,31.61],"Burdur":[37.72,30.29],"Bursa":[40.20,29.06],"Çanakkale":[40.15,26.41],"Çankırı":[40.60,33.62],"Çorum":[40.55,34.95],"Denizli":[37.78,29.09],"Diyarbakır":[37.91,40.24],"Düzce":[40.84,31.16],"Edirne":[41.68,26.56],"Elazığ":[38.68,39.22],"Erzincan":[39.75,39.49],"Erzurum":[39.91,41.27],"Eskişehir":[39.78,30.52],"Gaziantep":[37.07,37.38],"Giresun":[40.91,38.39],"Gümüşhane":[40.46,39.48],"Hakkâri":[37.58,43.74],"Hatay":[36.20,36.16],"Iğdır":[39.89,44.05],"Isparta":[37.76,30.55],"İstanbul":[41.01,28.98],"İzmir":[38.42,27.14],"Kahramanmaraş":[37.58,36.93],"Karabük":[41.20,32.63],"Karaman":[37.18,33.22],"Kars":[40.61,43.10],"Kastamonu":[41.38,33.78],"Kayseri":[38.72,35.48],"Kırıkkale":[39.85,33.51],"Kırklareli":[41.73,27.23],"Kırşehir":[39.15,34.16],"Kilis":[36.72,37.12],"Kocaeli":[40.77,29.94],"Konya":[37.87,32.48],"Kütahya":[39.42,29.98],"Malatya":[38.35,38.31],"Manisa":[38.61,27.43],"Mardin":[37.32,40.73],"Mersin":[36.81,34.64],"Muğla":[37.22,28.36],"Muş":[38.73,41.49],"Nevşehir":[38.62,34.71],"Niğde":[37.97,34.68],"Ordu":[40.98,37.88],"Osmaniye":[37.07,36.25],"Rize":[41.02,40.52],"Sakarya":[40.78,30.40],"Samsun":[41.29,36.33],"Siirt":[37.93,41.94],"Sinop":[42.03,35.15],"Sivas":[39.75,37.02],"Şanlıurfa":[37.17,38.79],"Şırnak":[37.52,42.46],"Tekirdağ":[40.98,27.51],"Tokat":[40.31,36.55],"Trabzon":[41.00,39.73],"Tunceli":[39.11,39.55],"Uşak":[38.68,29.41],"Van":[38.49,43.38],"Yalova":[40.65,29.27],"Yozgat":[39.82,34.81],"Zonguldak":[41.45,31.79]
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

// İlçe listesi Razor tarafındaki resmi il/ilçe sözlüğünden gelir. Her ilçeyi
// tek tek elle koordinatlandırmak yerine, bilinen ilçe merkezlerini üstte tutup
// eksik ilçeleri kendi il merkezine güvenli fallback ile bağlarız.
for (const province of Object.keys(PROVINCE_COORDS)) {
    const known = DISTRICT_COORDS[province] || {};
    DISTRICT_COORDS[province] = new Proxy(known, {
        get(target, district) {
            return target[district] || PROVINCE_COORDS[province];
        }
    });
}

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
