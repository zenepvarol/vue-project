<script setup>
/** MapLayers.vue - Harita Katman ve Sabit Nesne Yönetimi
 * Bu bileşen, haritanın temel görsel katmanlarını ve havaalanı gibi statik marker'ların yüklenmesi ve görüntülenmesini sağlar. */
import { watch } from 'vue'; import L from 'leaflet';
import { getAirportIcon } from '@/utils/mapVisuals';
import { airportService } from '@/api/airportService';

/** PROPS: Üst bileşenden (MapEngine) gelen aktif Leaflet harita objesi */
const props = defineProps({ map: Object });

/** EMITS: Yüklenen havaalanı verilerini simülasyon mantığı için MapEngine'e aktarır */
const emit = defineEmits(['airports-loaded']);

/** İZLEYİCİ (WATCHER): Harita objesi MapEngine tarafında oluşturulduğu anda temel katman kurulumunu ve veri çekme işlemlerini başlatır. */
watch(() => props.map, async (newMap) => {
  if (newMap) {
    // 1. TEMEL KATMAN: OpenStreetMap verilerini kullanarak dünya haritasını çizer
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '© OpenStreetMap', noWrap: true }).addTo(newMap);

    // 2. VERİ YÜKLEME: Havaalanı kütüphanesini API'den asenkron olarak çeker
    try {
      const response = await airportService.getAirports();
      const airData = response.data;
      emit('airports-loaded', airData);

      // 3. MARKER OLUŞTURMA: Her bir havaalanı için harita üzerinde ikon ve bilgi kutusu (popup) oluşturur
      airData.forEach(ap => {
          const airportIcon = getAirportIcon(ap.id);
          L.marker([ap.lat, ap.lon], { icon: airportIcon })
            .addTo(newMap)
            .bindPopup(`<b>${ap.name}</b><br>Acil İniş Noktası`);
      });
    } catch (error) {
      console.error("Havaalanları yüklenirken hata oluştu:", error);
    }
  }
}, { immediate: true });
</script>
<template><!-- Mantıksal bileşen --></template>