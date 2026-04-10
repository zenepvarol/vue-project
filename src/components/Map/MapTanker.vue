<script setup>
/** MapTanker.vue - Yakıt İkmal Sistemi Mantığı
 * Bu bileşen, uçakların yakıt durumunu izler ve ihtiyaç halinde en yakın üsten tanker uçağı sevk ederek ikmal sürecini yönetir. */
import { ref } from 'vue';
import L from 'leaflet';
import Swal from 'sweetalert2';
import { getDistance, calculateNextPosition } from '@/utils/physics';
import { getTankerIcon } from '@/utils/mapVisuals';
import { FLIGHT_STATUS } from '@/constants/flightConstants';

// PROPS: Harita objesi, uçuş verileri ve havalimanı listesi
const props = defineProps({
  map: Object,
  currentFlights: Object,
  airports: Array
});

// Sistemde o an aktif olan tanker uçağının verileri ve harita simgesi
const tankerFlight = ref(null);
const tankerMarker = ref(null);

// Belirli bir koordinata en yakın havalimanını bulur
const getNearestAirport = (lat, lon) => {
  if (!props.airports || !props.airports.length) return null;
  const pos = { lat, lon };
  return props.airports.reduce((nearest, airport) => {
    return getDistance(pos, airport) < getDistance(pos, nearest) ? airport : nearest;
  });
};

// ANA GÜNCELLEME DÖNGÜSÜ: MapEngine tarafından her saniye tetiklenir
const update = (activeIcao, isPaused, finalTarget) => {
  if (!activeIcao || isPaused || !props.currentFlights[activeIcao]) return;

  const plane = props.currentFlights[activeIcao];
  const currentPos = { lat: plane.lat, lon: plane.lon };

  // 1. İKMAL TALEBİ: Yakıt kritik seviyenin altına düştüğünde tanker sevk eder
  if (finalTarget && !tankerFlight.value) {
    const distToFinal = getDistance(currentPos, finalTarget);
    // TB3 5700km menzil baz alınarak yakıt/mesafe kontrolü yapılır
    if (plane.energy < 50 && plane.energy < (distToFinal / 57)) {
      const tankerBase = getNearestAirport(plane.lat, plane.lon);
      if (tankerBase) {
        tankerFlight.value = {
          targetIcao: activeIcao,
          lat: tankerBase.lat,
          lon: tankerBase.lon,
          heading: 0,
          velocity: 350,
          energy: 100,
          startBase: tankerBase,
          status: FLIGHT_STATUS.APPROACHING
        };
        tankerMarker.value = L.marker([tankerBase.lat, tankerBase.lon], {
          icon: getTankerIcon(),
          zIndexOffset: 1000
        }).addTo(props.map);

        Swal.fire({
          title: 'İKMAL TALEBİ', text: 'Yakıt yetersiz! Tanker uçağı sevk edildi.', icon: 'warning',
          toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
        });
      }
    }
  }

  // 2. TANKER HAREKET YÖNETİMİ
  if (tankerFlight.value) {
    const tanker = tankerFlight.value;
    const tMarker = tankerMarker.value;
    const targetPlane = props.currentFlights[tanker.targetIcao];

    if (!targetPlane) {
      tanker.status = FLIGHT_STATUS.RETURNING;
    } else {
      tanker.energy = Math.max(0, tanker.energy - 0.0008);

      // A) YAKLAŞMA: Tanker, hedef uçağa doğru yüksek hızla ilerler
      if (tanker.status === FLIGHT_STATUS.APPROACHING) {
        const { nextLat, nextLon, heading } = calculateNextPosition(tanker.lat, tanker.lon, targetPlane.lat, targetPlane.lon, 0.4);
        tanker.lat = nextLat;
        tanker.lon = nextLon;
        tanker.heading = heading;
        tMarker.setLatLng([nextLat, nextLon]);
        tMarker.setRotationAngle(heading - 45);

        // Mesafe 500m altına düştüğünde ikmal başlar
        if (getDistance({ lat: tanker.lat, lon: tanker.lon }, { lat: targetPlane.lat, lon: targetPlane.lon }) < 0.5) {
          tanker.status = FLIGHT_STATUS.REFUELING;
          Swal.fire({
            title: 'İKMAL BAŞLADI', text: 'Depo dolduruluyor...', icon: 'info',
            toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
          });
        }
      }
      // B) İKMAL: Tanker ve uçak beraber uçar, enerji seviyesi doldurulur
      else if (tanker.status === FLIGHT_STATUS.REFUELING) {
        tanker.lat = targetPlane.lat;
        tanker.lon = targetPlane.lon;
        tMarker.setLatLng([targetPlane.lat, targetPlane.lon]);
        tMarker.setRotationAngle((targetPlane.heading || 0) - 45);

        targetPlane.energy = Math.min(100, targetPlane.energy + 0.02);
        if (targetPlane.energy >= 100) {
          tanker.status = FLIGHT_STATUS.RETURNING;
          Swal.fire({
            title: 'İKMAL TAMAMLANDI', text: 'Tanker üsse geri dönüyor.', icon: 'success',
            toast: true, position: 'top-end', timer: 3000, showConfirmButton: false
          });
        }
      }
      // C) DÖNÜŞ: İkmal sonrası tanker kalktığı üsse geri döner
      else if (tanker.status === FLIGHT_STATUS.RETURNING) {
        const { nextLat, nextLon, heading, hasArrived } = calculateNextPosition(tanker.lat, tanker.lon, tanker.startBase.lat, tanker.startBase.lon, 0.4);
        tanker.lat = nextLat;
        tanker.lon = nextLon;
        tanker.heading = heading;
        tMarker.setLatLng([nextLat, nextLon]);
        tMarker.setRotationAngle(heading - 45);

        if (hasArrived) {
          props.map.removeLayer(tMarker);
          tankerFlight.value = null;
          tankerMarker.value = null;
        }
      }
    }
  }
};

// EXPOSE: Update metodunu MapEngine'den erişilebilir kılar
defineExpose({ update });
</script>

<template>
  <!-- Mantıksal bir bileşen olduğu için UI elementi barındırmaz -->
</template>