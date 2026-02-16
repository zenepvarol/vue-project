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
import 'leaflet-rotatedmarker';

// REAKTİF DEGİSKENLER 
const searchQuery = ref('');
const currentFlights = ref({}); 
const markers = {}; 
const flightPaths = ref({}); 
const animationSteps = ref({}); 
const activeIcao = ref(null); 

// GUZERGAH GORSELLESTİRME 
const staticRoutes = {}; // full rota
const activeRoutes = {}; // üzerinden gecilen yol
const terminalMarkers = {}; // baslangıc ve varis noktalari

let map = null;

// COMPUTED: Liste aramaya göre filtrelenir
const filteredFlights = computed(() => {
  const query = searchQuery.value.toLowerCase();
  return Object.values(currentFlights.value).filter(f =>
    f.callsign?.toString().toLowerCase().includes(query)
  );
});

const drawFullRoute = (icao) => { // ucak uzerinden gectikce koyulasır yol
  if (staticRoutes[icao]) map.removeLayer(staticRoutes[icao]); // eski cizimler siliniyor
  if (activeRoutes[icao]) map.removeLayer(activeRoutes[icao]);
  if (terminalMarkers[icao]) terminalMarkers[icao].forEach(m => map.removeLayer(m));

  const allPoints = flightPaths.value[icao].map(p => [p.lat, p.lon]);
  const currentStep = animationSteps.value[icao];
  
  // tam güzergah - soluk
  staticRoutes[icao] = L.polyline(allPoints, {
    color: '#9381ff',
    weight: 2,
    opacity: 0.2
  }).addTo(map);

  // baslangictan mevcut konuma kadar olan yol - koyu
  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, {
    color: '#9381ff', 
    weight: 4,
    opacity: 1
  }).addTo(map);

  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 });
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 });
  
  startCircle.addTo(map);
  endCircle.addTo(map);
  terminalMarkers[icao] = [startCircle, endCircle];
};

onMounted(async () => {
  map = L.map('map').setView([-28.4095, 151.9313], 7); // harita baslangic merkezi

  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© OpenStreetMap'
  }).addTo(map);

  try {
    const response = await fetch('/DH8D_valid.json');
    const data = await response.json();
    
    // veriler icao24'e (ucak kodu) gore gruplandırarak rota cikartilir
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

// ucaklar haritaya sabit yerlestirilir
    Object.keys(grouped).forEach(icao => {
      const firstPoint = grouped[icao][0]; // ucaklarin rota dizisindeki ilk koordinat verisi
      currentFlights.value[icao] = firstPoint; // ucaklarin baslangic verisi listede görünmesi için reaktif degisken yaptik
      animationSteps.value[icao] = 0; //ucak 0. adımdan basliyor

      if (firstPoint.lat && firstPoint.lon) {

        // Leaflet marker nesnesini ucsk konumu ve yonu kullanılarak oluşturuyoruz (yon-heading)
        const marker = L.marker([firstPoint.lat, firstPoint.lon], { 
          icon: planeIcon, 
          rotationAngle: (firstPoint.heading || 0) - 45 // icon gidis yönüne göre donuyor
        }).addTo(map); // olusan markerlar haritaya ekleniyor

        marker.bindPopup(`<b>Uçuş: ${firstPoint.callsign || 'Bilinmiyor'}</b>`);  // tıklandığında açılan popup

        marker.on('click', () => { //icona tıklanınca ucak aktiflesiyor
          activeIcao.value = icao;
          
          drawFullRoute(icao); // tam guzergah
          setTimeout(() => marker.openPopup(), 100); // harita guncellenirlen popup kapanmasın diye gecikme kodu
        });

        markers[icao] = marker; // marker a daha sonra erisebilmek icin sozlukte sakliyoruz
      }
    });

    // her saniye konum güncelleme
    setInterval(() => {
      if (activeIcao.value && flightPaths.value[activeIcao.value]) {
        const icao = activeIcao.value;
        const path = flightPaths.value[icao];
        const step = animationSteps.value[icao];
        
        const nextStep = (step + 1) % path.length;
        const point = path[nextStep];
        
        animationSteps.value[icao] = nextStep;
        currentFlights.value[icao] = point; // Listeyi güncelle

        if (markers[icao]) {
          const newPos = [point.lat, point.lon];
          markers[icao].setLatLng(newPos);
          markers[icao].setRotationAngle((point.heading || 0) - 45);

          if (activeRoutes[icao]) {
            if (nextStep === 0) {
              activeRoutes[icao].setLatLngs([newPos]); // rota basa donunce temizle
            } else {
              activeRoutes[icao].addLatLng(newPos); 
            }
          }
        }
      }
    }, 1000);

  } catch (error) {
    console.error("Veri hatası:", error);
  }
});

const focusFlight = (f) => {
  if (f.lat && f.lon) { // koordinat verileri gecerli mi 
    
    activeIcao.value = f.icao24; // tıklanan ucak aktiflesir
    drawFullRoute(f.icao24); // guzergah cizimi
    map.setView([f.lat, f.lon], 12); // harita odagı zoom seviyesi 12
    setTimeout(() => {
      markers[f.icao24]?.openPopup();
    }, 350); // gorsel kayma olmasın diye 350 ms bekleme
  }
};
</script>

<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { 
  height: 100%; 
  width: 100%; 
  font-family: 'Segoe UI', sans-serif; 
  background: #121212; 
  overflow: hidden; 
}

.app-container { /* sidebar ve harita yan yana tam ekran*/
  display: flex; 
  height: 100vh; 
  width: 100vw; 
  position: absolute; 
  top: 0; 
  left: 0; 
}

.sidebar { /*ucus listesi alani*/
  width: 320px; 
  min-width: 320px;
  background: #f7d9c4; 
  color: #333;
  display: flex; 
  flex-direction: column;
  border-right: 1px solid #333; 
  z-index: 1000;
}

.sidebar-header { padding: 20px; background: #e2cfc4; border-bottom: 1px solid #333; }
.sidebar-header h3 { margin: 0 0 10px 0; color: #000; }
.search-input { width: 100%; padding: 10px; border-radius: 4px; border: 1px solid #fff; background: #faedcb; color: #000; box-sizing: border-box; }

.flight-list { flex: 1; overflow-y: auto; list-style: none; padding: 0; }
.flight-list li { padding: 15px 20px; border-bottom: 1px solid #e2cfc4; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }
.flight-list li:hover { background: #fec3a6; } /* fare üstüne gelince renk degisimi */
.callsign { font-weight: bold; color: #282828; }
.details { font-size: 0.8em; color: #666; }

#map { flex-grow: 1; height: 100%; background: #0b0b0b; } /* Sidebar'dan kalan tüm alan*/

.moving-plane {
  font-size: 40px; 
  color: #9381ff; 
  text-shadow: 1px 1px 2px black;
  transition: all 1s linear; /* sürekli akici hareket */
}
.plane-icon { background: none !important; border: none !important; } 
</style>