<script setup>
/** MapNavigator.vue - Harita Navigasyon ve Odaklanma Kontrolleri
 * Uçağa odaklanma, haritayı merkezleme ve zoom işlemlerini yönetir. */

const props = defineProps({
  map: Object,           // Leaflet harita objesi
  mapRoutes: Object,     // Rota çizimi tetikleme için
  selectedFlight: Object // Aktif seçili olan uçuş verisi
});

const activeIcao = defineModel('activeIcao');
const isPaused = defineModel('isPaused');

// Haritayı belirli koordinatlara yumuşak bir şekilde odaklar
const zoomToPlane = (lat, lon) => {
  if (lat && lon && props.map) {
    props.map.setView([lat, lon], 8, { animate: true, duration: 1 });
  }
};

// Aktif seçili uçağı haritanın merkezine getirir
const recenterMap = () => {
  if (props.selectedFlight) {
    zoomToPlane(props.selectedFlight.lat, props.selectedFlight.lon);
  }
};

// Bir uçak tıklandığında veya seçildiğinde ona odaklanır ve rotasını çizer
const focusFlight = (f) => {
  if (f.lat && f.lon) {
    if (activeIcao.value !== f.icao24) {
      activeIcao.value = f.icao24;
      isPaused.value = true;
      props.mapRoutes?.drawFullRoute(f.icao24);
    }
    zoomToPlane(f.lat, f.lon);
  }
};

// Dışarıya açılan metodlar
defineExpose({
  zoomToPlane,
  recenterMap,
  focusFlight
});
</script>

<template>
  <!-- Mantıksal bileşen -->
</template>
