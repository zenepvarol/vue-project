<template>
  <div class="app-container">
    <div class="sidebar">
      <div class="sidebar-header">
        <h3>Uçuş Listesi</h3>
        <input v-model="searchQuery" placeholder="Uçuş (Callsign) ara..." class="search-input" />
      </div>

      <ul class="flight-list">
        <li v-for="f in filteredFlights" :key="f.icao24" @click="focusFlight(f)">
          <div class="flight-info">
            <span class="callsign">{{ f.callsign || 'Bilinmiyor' }}</span>
            <span class="details">Hız: {{ Math.round(f.velocity) }} | Rakım: {{ Math.round(f.baroaltitude) }}ft</span>
          </div>
          <div class="arrow" :style="{ transform: `rotate(${f.heading}deg)` }">✈</div>
        </li>
      </ul>
    </div>
    <div id="map"></div>
  </div>
</template>

<script setup>
import { onMounted, ref, computed } from 'vue';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import 'leaflet-rotatedmarker'; // marker döndürme

// REAKTİF DEĞİŞKENLER (State)
const searchQuery = ref('');
const currentFlights = ref({}); 
const markers = {}; 
const flightPaths = ref({}); 
const animationSteps = ref({}); 
const activeIcao = ref(null); 
let map = null;

// COMPUTED ÖZELLİKLER
const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});


onMounted(async () => {
  map = L.map('map').setView([-28.4095, 151.9313], 7);

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap'
  }).addTo(map);

  try {
    const response = await fetch('/DH8D_valid.json');
    const data = await response.json();
    
    const grouped = data.reduce((acc, row) => {
      if (!acc[row.icao24]) acc[row.icao24] = [];
      acc[row.icao24].push(row);
      return acc;
    }, {});

    flightPaths.value = grouped;

    const planeIcon = L.divIcon({
      html: `<div class="moving-plane">✈</div>`,
      className: 'plane-icon',
      iconSize: [40, 40],
      iconAnchor: [20, 20]
    });

    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0];
      currentFlights.value[icao] = firstPoint;
      animationSteps.value[icao] = 0;

      if (firstPoint.lat && firstPoint.lon) {
        // marker oluşturulurken tıklama olayı
        const marker = L.marker([firstPoint.lat, firstPoint.lon], {
          icon: planeIcon,
          rotationAngle: (firstPoint.heading || 0) - 45
        }).addTo(map);

        // popup
        marker.bindPopup(`<b>Uçuş: ${firstPoint.callsign || 'Bilinmiyor'}</b>`);

        // uçağa tıklama
        marker.on('click', () => {
          activeIcao.value = icao;
          marker.openPopup();
        });

        markers[icao] = marker;
      }
    });

    // her saniye aktif uçak hareket eder
    setInterval(() => {
      if (activeIcao.value && flightPaths.value[activeIcao.value]) {
        const icao = activeIcao.value;
        const path = flightPaths.value[icao];
        const step = animationSteps.value[icao];
        
        const nextStep = (step + 1) % path.length;
        const point = path[nextStep];
        
        animationSteps.value[icao] = nextStep;
        currentFlights.value[icao] = point;

        if (markers[icao]) {
          markers[icao].setLatLng([point.lat, point.lon]);
          markers[icao].setRotationAngle((point.heading || 0) - 45);
        }
      }
    }, 1000);

  } catch (error) {
    console.error("Hata:", error);
  }
});

const focusFlight = (f) => {
  if (f.lat && f.lon) {
    activeIcao.value = f.icao24; 
    map.setView([f.lat, f.lon], 12); // yakınlaşma seviyesi
    markers[f.icao24]?.openPopup();
  }
};
</script>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { height: 100%; width: 100%; font-family: 'Segoe UI', sans-serif; background: #121212; overflow: hidden; }

.app-container { display: flex; height: 100vh; width: 100vw; position: absolute; top: 0; left: 0; }

.sidebar {
  width: 320px; min-width: 320px;
  background: #f7d9c4; color: #333;
  display: flex; flex-direction: column;
  border-right: 1px solid #333; z-index: 1000;
}

.sidebar-header { padding: 20px; background: #e2cfc4; border-bottom: 1px solid #333; }
.sidebar-header h3 { margin: 0 0 10px 0; color: #000; }
.search-input { width: 100%; padding: 10px; border-radius: 4px; border: 1px solid #fff; background: #faedcb; color: #000; }

.flight-list { flex: 1; overflow-y: auto; list-style: none; padding: 0; }
.flight-list li { padding: 15px 20px; border-bottom: 1px solid #e2cfc4; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }
.flight-list li:hover { background: #fec3a6; }
.callsign { font-weight: bold; color: #282828; }
.details { font-size: 0.8em; color: #666; }

#map { flex-grow: 1; height: 100%; background: #0b0b0b; }

.moving-plane {
  font-size: 40px; 
  color: #9381ff; 
  text-shadow: 1px 1px 2px black;
  transition: all 1s linear; 
}
.plane-icon { background: none !important; border: none !important; }
</style>