<script setup>
/** MapPlanes.vue - Uçak Marker Yönetimi
 * Haritadaki tüm uçakların ikonlarını (marker) oluşturur, koordinatlarını günceller ve tıklandığında uçağa odaklanılmasını sağlar. */
import { watch, onUnmounted } from 'vue'; import L from 'leaflet';
import { getPlaneIcon } from '@/utils/mapVisuals';

const props = defineProps({
  map: Object,           // Leaflet harita objesi
  currentFlights: Object, // Tüm uçuş verileri
  activeIcao: String,    // Seçili uçağın ICAO kodu
  myFleetIcaos: Array    // Envanterdeki İHA'ların listesi
});

const emit = defineEmits(['marker-click']);
const markers = {}; // Harita üzerindeki Leaflet marker objelerini tutan sözlük

/** WATCHER: Uçuş verileri değiştikçe marker'ları yönetir */
watch(() => props.currentFlights, (flights) => {
  if (!props.map || !flights) return;

  Object.keys(flights).forEach(icao => {
    const plane = flights[icao];
    const isEnvanter = props.myFleetIcaos.includes(String(icao));

    // 1- Yeni Marker Oluşturma: Uçak henüz haritada yoksa
    if (!markers[icao]) {
      if (!plane.lat || !plane.lon) return;

      const marker = L.marker([plane.lat, plane.lon], {
        icon: getPlaneIcon(isEnvanter || plane.isSiha, plane.isApi),
        rotationAngle: (plane.heading || 0) - 45
      }).addTo(props.map);

      marker.on('click', (e) => {
        L.DomEvent.stopPropagation(e);
        emit('marker-click', plane);
      });

      markers[icao] = marker;
    }
    // 2- Mevcut Marker'ı Güncelleme (Sadece İkon ve Döndürme - Konum MapEngine'den yönetilir)
    else {
      const marker = markers[icao];
      marker.setIcon(getPlaneIcon(isEnvanter || plane.isSiha, plane.isApi));
      // Yerel simülasyon yoksa (Remote uçaksa) rotasyonu buradan da güncelleyebiliriz
      if (!props.myFleetIcaos.includes(icao)) {
        marker.setRotationAngle((plane.heading || 0) - 45);
      }
    }
  });
}, { deep: true });

/** CLEANUP: Bileşen kapandığında tüm marker'ları temizle */
onUnmounted(() => {
  Object.values(markers).forEach(m => m.remove());
});

/** EXPOSE: Harita motorunun (MapEngine) marker'lara erişip slideTo gibi metodları çağırabilmesi için markers listesini dışarı açar. */
defineExpose({ markers });
</script>

<template>
  <!-- Mantıksal bir bileşen olduğu için UI elementi barındırmaz -->
</template>