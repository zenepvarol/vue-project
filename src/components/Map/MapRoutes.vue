<script setup>
/** MapRoutes.vue - Rota ve Çizgi Yönetimi
 * Uçakların izleri (polylines), görev rotaları ve seçili uçuşun tüm rota haritasını çizen görsel katmanları yönetir. */
import { ref, watch, onUnmounted } from 'vue';
import L from 'leaflet';

const props = defineProps({
  map: Object,           // Leaflet harita objesi
  activeIcao: String,    // Seçili uçağın ICAO kodu
  currentFlights: Object, // Tüm uçuş verileri
  flightPaths: Object,   // Ham rota verileri (JSON'dan gelen)
  animationSteps: Object // Her uçağın animasyon adımları
});

// Ana rota katmanı (Tüm çizgiler bu grupta toplanır)
const routeLayer = L.layerGroup();
// Özel rota referansları
const activeRoutes = {}; // Her uçağın arkasındaki canlı izler
const missionPathLayer = ref(null); // Aktif görev rotası (kesikli çizgi vb.)
const emergencyRoute = ref(null);   // Acil iniş rotası

/** METOD: Tüm rotaları ve katmanları temizler */
const clearAllRoutes = () => {
  routeLayer.clearLayers();
  if (emergencyRoute.value && props.map) {
    props.map.removeLayer(emergencyRoute.value);
    emergencyRoute.value = null;
  }
};

/** METOD: Belirli bir uçağın canlı izini sıfırlar */
const resetActivePath = (icao) => {
  if (activeRoutes[icao]) {
    routeLayer.removeLayer(activeRoutes[icao]);
    const plane = props.currentFlights[icao];
    if (plane) {
      activeRoutes[icao] = L.polyline([[plane.lat, plane.lon]], { 
        color: '#9381ff', weight: 4, opacity: 1 
      }).addTo(routeLayer);
    }
  }
};

/** METOD: Görev rotasını (Hedef çizgisi) çizer */
const drawMissionRoute = (plane, targetPos) => {
  if (!props.map) return;
  if (missionPathLayer.value) props.map.removeLayer(missionPathLayer.value);

  const startPos = [plane.lat, plane.lon];
  const targetCoord = [targetPos.lat, targetPos.lon];

  // Hedefe kadar olan kesikli yol (Preview)
  const previewLine = L.polyline([startPos, targetCoord], {
    color: '#e74c3c', weight: 4, dashArray: '10, 5', opacity: 0.7
  });

  // Hedef noktası ikonu
  const targetCircle = L.circleMarker(targetCoord, {
    radius: 8, color: '#e74c3c', fillOpacity: 1, weight: 2
  }).bindPopup('<b>BOMBALAMA HEDEFİ</b>');

  // Uçağın ilerledikçe arkasında bıraktığı kırmızı katı çizgi
  const progressLine = L.polyline([startPos], {
    color: '#e74c3c', weight: 4, opacity: 1
  });

  missionPathLayer.value = L.layerGroup([previewLine, targetCircle, progressLine]).addTo(props.map);
  return progressLine;
};

/** METOD: Seçili uçağın tüm geçmiş ve gelecek rotasını haritaya döker */
const drawFullRoute = (icao) => {
  if (!props.map || !props.flightPaths[icao]) return;
  
  clearAllRoutes();
  const path = props.flightPaths[icao];
  const allPoints = path.map(p => [p.lat, p.lon]);
  const currentStep = props.animationSteps[icao] || 0;

  // Tüm rotayı gösteren soluk çizgi
  const staticPath = L.polyline(allPoints, { color: '#0077b6', weight: 2, opacity: 0.5 });
  
  // Şu ana kadar kat edilen yolu gösteren belirgin çizgi
  const pointsSoFar = allPoints.slice(0, currentStep + 1);
  activeRoutes[icao] = L.polyline(pointsSoFar, { color: '#9381ff', weight: 4, opacity: 1 });

  // Başlangıç ve bitiş noktalarını işaretle
  const startCircle = L.circleMarker(allPoints[0], { radius: 6, color: '#2ecc71', fillOpacity: 1, weight: 2 });
  const endCircle = L.circleMarker(allPoints[allPoints.length - 1], { radius: 6, color: '#e74c3c', fillOpacity: 1, weight: 2 });

  staticPath.addTo(routeLayer);
  activeRoutes[icao].addTo(routeLayer);
  startCircle.addTo(routeLayer);
  endCircle.addTo(routeLayer);
  routeLayer.addTo(props.map);
};

// Harita objesi hazır olduğunda ana katmanı ekle
watch(() => props.map, (newMap) => {
  if (newMap) routeLayer.addTo(newMap);
}, { immediate: true });

onUnmounted(() => {
  routeLayer.remove();
});

// Metodları ve Layer referanslarını MapEngine'e aç
defineExpose({
  clearAllRoutes,
  resetActivePath,
  drawMissionRoute,
  drawFullRoute,
  missionPathLayer,
  emergencyRoute,
  activeRoutes,
  routeLayer
});
</script>

<template>
  <!-- Mantıksal bir bileşendir -->
</template>
