<script setup>
/** MapLayers.vue - Harita Katman ve Sabit Nesne Yönetimi
 * Bu bileşen, haritanın temel görsel katmanlarını ve havaalanı gibi statik marker'ların yüklenmesi ve görüntülenmesini sağlar. */
import { watch } from 'vue'; import L from 'leaflet';
import { getAirportIcon } from '@/utils/mapVisuals';
import { airportService } from '@/api/airportService';
import { aircraftService } from '@/api/aircraftService';
import { getDistance } from '@/utils/physics';

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

      // Uçakların başlangıç noktalarını da Ana Üs (Havalimanı) olarak ekle
      const aircraftResponse = await aircraftService.getAircrafts();
      const aircrafts = aircraftResponse.data;

      aircrafts.forEach(aircraft => {
        const aLat = parseFloat(aircraft.latitude);
        const aLon = parseFloat(aircraft.longitude);

        // Mevcut havalimanları arasında bu koordinatlara aşırı yakın (2 km'den az) olan var mı?
        const isNearExistingAirport = airData.some(ap => {
          if (ap.type === 'Base') return false; // Diğer üsleri yoksay
          const dist = getDistance({ lat: aLat, lon: aLon }, { lat: ap.lat, lon: ap.lon });
          return dist < 2.0; // 2 km tolerans
        });

        // Eğer yakınında bir havalimanı yoksa, yeni bir üs marker'ı ekle (Üst üste çakışmayı önlemek için)
        if (!isNearExistingAirport) {
          const baseId = `BASE_${aircraft.icao24}`;
          if (!airData.find(a => a.id === baseId)) {
            airData.push({
              id: baseId,
              name: `${aircraft.callsign || aircraft.icao24} Ana Üssü`,
              lat: aLat,
              lon: aLon,
              type: 'Base'
            });
          }
        }
      });

      emit('airports-loaded', airData);

      // 3. MARKER OLUŞTURMA: Her bir havaalanı için harita üzerinde ikon ve bilgi kutusu (popup) oluşturur
      airData.forEach(ap => {
        const airportIcon = getAirportIcon(ap.type === 'Base' ? 'ÜS' : ap.id);
        L.marker([ap.lat, ap.lon], { icon: airportIcon })
          .addTo(newMap)
          .bindPopup(`<b>${ap.name}</b><br>${ap.type === 'Base' ? 'Ana Üs' : 'Acil İniş Noktası'}`);
      });
    } catch (error) {
      console.error("Havaalanları yüklenirken hata oluştu:", error);
    }
  }
}, { immediate: true });
</script>
<template><!-- Mantıksal bileşen --></template>