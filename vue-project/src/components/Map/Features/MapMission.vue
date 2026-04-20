<script setup>
import { getDistance } from '@/utils/physics'; import { triggerExplosion as visualExplosion } from '@/utils/mapVisuals';
import { FLIGHT_STATUS } from '@/constants/flightConstants';

const props = defineProps({
  currentFlights: Object,
  airports: Array,
  myFleetIcaos: Array,
  map: Object,
  mapRoutes: Object
});

const destinationAirportId = defineModel('destinationAirportId');
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');
const activeIcao = defineModel('activeIcao');
const isPaused = defineModel('isPaused');

// Kullanıcının seçtiği hedef rotasına hangarda bekleyen en yakın İHA'yı gönderir
const assignMission = () => {
  if (!destinationAirportId.value) return;
  const arr = destinationAirportId.value.toUpperCase().trim(), arrAp = props.airports.find(ap => ap.id === arr);
  let targetPos = null;

  if (destinationAirportId.value === 'MANUAL_COORD' && destLat.value && destLon.value) {
    targetPos = { lat: parseFloat(destLat.value), lon: parseFloat(destLon.value) };
  } else if (arrAp) {
    targetPos = { lat: arrAp.lat, lon: arrAp.lon };
  }

  if (!targetPos) {
    Swal.fire('Hata', 'Geçerli bir hedef (havalimanı veya koordinat) seçin!', 'error');
    return;
  }

  const availableIcaos = Object.keys(props.currentFlights).filter(icao => {
    const plane = props.currentFlights[icao];
    const isOurSiha = props.myFleetIcaos.includes(String(icao)) || plane.isSiha || plane.IsSiha;
    return isOurSiha && (plane.status === FLIGHT_STATUS.STANDBY || plane.status === FLIGHT_STATUS.ARRIVED);
  });
  
  if (availableIcaos.length === 0) {
    Swal.fire('Hangar Boş', 'Şu an tüm üslerdeki İHA’lar görevde!', 'info');
    return;
  }

  let closestIcao = null;
  let minDistance = Infinity;

  availableIcaos.forEach(icao => {
    const plane = props.currentFlights[icao];
    const dist = getDistance(
      { lat: parseFloat(plane.lat), lon: parseFloat(plane.lon) }, 
      { lat: parseFloat(targetPos.lat), lon: parseFloat(targetPos.lon) }
    );
    if (dist < minDistance) {
      minDistance = dist;
      closestIcao = icao;
    }
  });

  const plane = props.currentFlights[closestIcao];
  
  // Kalkış noktasını (evini) unutmaması için o anki konumunu kesinleştir
  plane.homeLat = parseFloat(plane.lat);
  plane.homeLon = parseFloat(plane.lon);
  
  plane.missionDest = targetPos;
  plane.trip_distance = minDistance;
  plane.total_mission_dist = minDistance;
  plane.distance_from_dep = 0;
  plane.status = FLIGHT_STATUS.GOING_TO_DEST;

  props.mapRoutes?.drawMissionRoute(plane, targetPos); // Görev rotasını çizimi

  activeIcao.value = closestIcao;
  isPaused.value = false;
  
  if (props.map) {
    props.map.setView([plane.lat, plane.lon], 7);
  }

  Swal.fire({
    title: 'Operasyon Başladı',
    html: `<b>${plane.callsign}</b> seçildi.<br>Mesafe: <b>${Math.round(minDistance)} km</b>`,
    icon: 'warning', toast: true, position: 'top-end',
    timer: 4000, showConfirmButton: false, timerProgressBar: true
  });
};

// Hedefe varıldığında patlama efektini tetikler
const triggerExplosion = (lat, lon) => props.map && visualExplosion(lat, lon, props.map);

defineExpose({
  assignMission,
  triggerExplosion
});
</script>

<template>
  <!-- Mantıksal bir bileşen olduğu için UI elementi barındırmaz -->
</template>