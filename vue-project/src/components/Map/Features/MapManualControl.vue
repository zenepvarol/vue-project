<script setup>
/** MapManualControl.vue - Manuel Rota Yönetimi
 * Kullanıcının koordinat veya havalimanı seçerek manuel uçuş rotası oluşturmasını sağlar. */
import Swal from 'sweetalert2';
import { getDistance } from '@/utils/physics';
import { FLIGHT_STATUS } from '@/constants/flightConstants';

const props = defineProps({
  airports: Array,
  currentFlights: Object,
  mapRoutes: Object // MapRoutes bileşenine erişim (rota çizimi için)
});

const activeIcao = defineModel('activeIcao');
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');
const manualAirportId = defineModel('manualAirportId');
const isManualRouting = defineModel('isManualRouting');
const manualTarget = defineModel('manualTarget');
const isPaused = defineModel('isPaused');

// Manuel hedef tanımlama ve rotayı başlatma
const setManualTarget = () => {
  if (!activeIcao.value) return;
  let target = null;
  const targetAp = props.airports.find(ap => ap.id.toLowerCase() === (manualAirportId.value || '').toLowerCase());

  if (manualAirportId.value === 'MANUAL_COORD' && manualLat.value && manualLon.value) {
    target = { lat: parseFloat(manualLat.value), lon: parseFloat(manualLon.value) };
  } else if (targetAp) {
    target = { lat: targetAp.lat, lon: targetAp.lon };
  }

  if (target) {
    const plane = props.currentFlights[activeIcao.value];
    const dist = getDistance({ lat: plane.lat, lon: plane.lon }, target);

    // Slerp algoritmasının kullanabilmesi için başlangıç konumunu kaydet
    plane.startLat = parseFloat(plane.lat);
    plane.startLon = parseFloat(plane.lon);

    plane.trip_distance = dist;
    plane.total_mission_dist = dist;
    plane.distance_from_dep = 0;
    plane.total_manual_dist = dist;
    plane.status = FLIGHT_STATUS.ACTIVE;
    
    const targetName = manualAirportId.value === 'MANUAL_COORD' ? 'Manuel' : (targetAp?.id || 'Hedef');
    plane.missionDestName = targetName;
    manualTarget.value = { ...target, name: targetName };
    isManualRouting.value = true;
    isPaused.value = false;

    // Rota çizimini tetikle
    props.mapRoutes?.drawMissionRoute(plane, target);

    Swal.fire({
      icon: 'success',
      title: 'Rota Hazır',
      text: `Mesafe: ${Math.round(dist)} km`,
      timer: 1500,
      toast: true,
      position: 'top-end',
      showConfirmButton: false
    });
  }
};

defineExpose({ setManualTarget });
</script>

<template>
  <!-- Mantıksal bileşen -->
</template>
