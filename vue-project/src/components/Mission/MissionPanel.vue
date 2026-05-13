<template>
  <!-- v-sheet: Sağ panel konteyneri. Sadece uçak seçiliyse VEYA kullanıcı Admin ise gösterilir -->
  <v-sheet v-if="authStore.user?.role?.toLowerCase() === 'admin' || selectedFlight" component="aside" class="sidebar right pt-16" style="overflow-y: auto; max-height: 100vh;">
    
    <MissionTargeting v-if="!selectedFlight" v-model:destinationAirportId="destinationAirportId" v-model:destLat="destLat"
      v-model:destLon="destLon" :airports="airports" @assign-mission="$emit('assign-mission')" />

    <!-- Uçuş Detayları: Haritadan araç seçilince aktif olur -->
    <MissionDetails v-else v-model:activeIcao="activeIcao" v-model:manualAirportId="manualAirportId"
      v-model:manualLat="manualLat" v-model:manualLon="manualLon" :selectedFlight="selectedFlight" :airports="airports"
      :activeFailure="activeFailure" :isEmergencySimulated="isEmergencySimulated" :isEmergency="isEmergency"
      :isReturningToStart="isReturningToStart" :isPaused="isPaused" :myFleetIcaos="myFleetIcaos"
      :animationSteps="animationSteps" :emergencyCountdown="emergencyCountdown" @recenter-map="$emit('recenter-map')"
      @return-to-start="$emit('return-to-start')" @toggle-pause="$emit('toggle-pause')"
      @trigger-simulated-failure="$emit('trigger-simulated-failure')"
      @handle-manual-emergency="$emit('handle-manual-emergency')" @set-manual-target="$emit('set-manual-target')" />
  </v-sheet>
</template>

<script setup>
/** MissionPanel.vue - Sağ Panel Ana Bileşeni
 * Hedef Seçimi ve Uçuş Detayları bileşenlerini yönetir. */
import MissionTargeting from './MissionTargeting.vue';
import MissionDetails from './MissionDetails.vue';
import { useAuthStore } from '@/stores/authStore';

const authStore = useAuthStore();

// PROPS: Üst bileşenden gelen veriler
defineProps({
  selectedFlight: Object,
  airports: Array,
  activeFailure: Object,
  isEmergencySimulated: Boolean,
  isEmergency: Boolean,
  isReturningToStart: Boolean,
  isPaused: Boolean,
  myFleetIcaos: Array,
  animationSteps: Object,
  emergencyCountdown: Number
});

// MODELS: Çift yönlü veri bağlama (v-model)
const activeIcao = defineModel('activeIcao');
const destinationAirportId = defineModel('destinationAirportId', { type: String });
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');
const manualAirportId = defineModel('manualAirportId', { type: String });
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');

// EMITS: Harita fonksiyonlarını tetikler (App.vue'ya iletir)
defineEmits([
  'assign-mission',
  'recenter-map',
  'return-to-start',
  'toggle-pause',
  'trigger-simulated-failure',
  'handle-manual-emergency',
  'set-manual-target'
]);
</script>
