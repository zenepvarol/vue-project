<script setup>
/** MapRoutes.vue - Rota ve Çizgi Yönetimi
 * Uçakların izleri (polylines), görev rotaları ve seçili uçuşun tüm rota haritasını çizen görsel katmanları yönetir. */
import { ref, watch, onUnmounted } from 'vue';
import L from 'leaflet';
import { interpolateSlerp } from '@/utils/physics';

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
const missionPaths = {}; // Her uçağın aktif görev rotaları (ICAO -> LayerGroup)
const emergencyRoute = ref(null);   // Acil iniş rotası

/** METOD: Tüm rotaları ve katmanları temizler */
const clearAllRoutes = () => {
  routeLayer.clearLayers();
  // Tüm görev rotalarını temizle
  Object.values(missionPaths).forEach(layer => {
    if (props.map) props.map.removeLayer(layer);
  });
  // Objenin içini boşalt
  for (let icao in missionPaths) delete missionPaths[icao];

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
  const icao = plane.icao || plane.icao24;
  
  // Eğer bu uçağın zaten bir rotası varsa haritadan kaldır
  if (missionPaths[icao]) props.map.removeLayer(missionPaths[icao]);

  // Başlangıç ve hedef koordinatlarını obje formatına hazırla
  const startCoord = { lat: plane.lat, lon: plane.lon };
  const targetCoord = { lat: targetPos.lat, lon: targetPos.lon };

  // Slerp ile kavisli önizleme yolu oluştur (Preview)
  const previewPoints = [];
  for (let i = 0; i <= 50; i++) {
    const pos = interpolateSlerp(startCoord, targetCoord, i / 50);
    previewPoints.push([pos.lat, pos.lon]);
  }

  // Kavisli kesikli çizgi (Önizleme)
  const previewLine = L.polyline(previewPoints, {
    color: '#e74c3c', weight: 4, dashArray: '10, 5', opacity: 0.7
  });

  // Hedef noktası ikonu
  const targetLatLng = [targetPos.lat, targetPos.lon];
  const targetCircle = L.circleMarker(targetLatLng, {
    radius: 8, color: '#e74c3c', fillOpacity: 1, weight: 2
  }).bindPopup(`<b>HEDEF (${icao})</b>`);

  // Uçağın ilerledikçe arkasında bıraktığı kırmızı katı çizgi (Canlı takip)
  const progressLine = L.polyline([[plane.lat, plane.lon]], {
    color: '#e74c3c', weight: 4, opacity: 1
  });

  // Katmanları birleştirip haritaya ekle
  missionPaths[icao] = L.layerGroup([previewLine, targetCircle, progressLine]).addTo(props.map);
  return progressLine;
};

/** METOD: Telemetriden gelen uzak uçağın rotasını günceller veya çizer */
const updateRemoteMissionRoute = (icao, planeLat, planeLon, destLat, destLon) => {
  if (!props.map || !destLat || !destLon) {
    // Eğer hedef yoksa uçağın eski rotasını temizle
    if (missionPaths[icao]) {
      props.map.removeLayer(missionPaths[icao]);
      delete missionPaths[icao];
    }
    return;
  }

  // Uzak uçaklara kavisli (Slerp) hedef çizgisi çiziyoruz (Fizikle uyumlu olması için)
  if (missionPaths[icao]) props.map.removeLayer(missionPaths[icao]);

  const points = [];
  const startPos = { lat: planeLat, lon: planeLon };
  const endPos = { lat: destLat, lon: destLon };
  
  // 30 noktadan oluşan kavisli bir hat oluştur
  for (let i = 0; i <= 30; i++) {
    const pos = interpolateSlerp(startPos, endPos, i / 30);
    points.push([pos.lat, pos.lon]);
  }

  const previewLine = L.polyline(points, {
    color: '#3498db', weight: 3, dashArray: '5, 10', opacity: 0.6
  });

  const targetCircle = L.circleMarker([destLat, destLon], {
    radius: 5, color: '#3498db', fillOpacity: 0.8, weight: 1
  });

  missionPaths[icao] = L.layerGroup([previewLine, targetCircle]).addTo(props.map);
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

// Görev ilerlemesini (çizgi takibi) günceller
const updateMissionProgress = (plane) => {
  const icao = plane.icao || plane.icao24;
  if (missionPaths[icao]) {
    const progressLine = missionPaths[icao].getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
    if (progressLine) progressLine.addLatLng([plane.lat, plane.lon]);
  }
};

//Görev tamamlandığında çizgiyi yeşil yapar
const setMissionSuccess = (icao) => {
  if (missionPaths[icao]) {
    const progressLine = missionPaths[icao].getLayers().find(l => l instanceof L.Polyline && !l.options.dashArray);
    if (progressLine) progressLine.setStyle({ color: '#2ecc71', dashArray: null });
  }
};

// Acil durum rotasını oluşturur (Kavisli)
const setEmergencyRoute = (start, end) => {
  if (!props.map) return;
  if (emergencyRoute.value) props.map.removeLayer(emergencyRoute.value);

  // Acil iniş yapılacak en yakın havalimanına kavisli bir hat oluştur
  const points = [];
  for (let i = 0; i <= 30; i++) {
    const pos = interpolateSlerp(start, end, i / 30);
    points.push([pos.lat, pos.lon]);
  }

  // Kırmızı kesikli acil iniş hattını çiz
  emergencyRoute.value = L.polyline(points, {
    color: 'red', weight: 4, dashArray: '10, 10', opacity: 0.8
  }).addTo(props.map);
  return emergencyRoute.value;
};

// Metodları ve Layer referanslarını MapEngine'e aç
defineExpose({
  clearAllRoutes,
  resetActivePath,
  drawMissionRoute,
  updateRemoteMissionRoute,
  drawFullRoute,
  updateMissionProgress,
  setMissionSuccess,
  setEmergencyRoute,
  missionPaths,
  emergencyRoute,
  activeRoutes,
  routeLayer
});
</script>

<template>
  <!-- Mantıksal bir bileşen olduğu için UI elementi barındırmaz -->
</template>