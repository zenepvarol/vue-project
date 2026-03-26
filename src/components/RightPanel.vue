<template>
  <div class="sidebar right" style="overflow-y: auto; max-height: 100vh;">
    <TargetSelection
      v-if="!selectedFlight"
      v-model:destinationAirportId="destinationAirportId"
      v-model:destLat="destLat"
      v-model:destLon="destLon"
      :airports="airports"
      @assign-mission="$emit('assign-mission')"
    />

    <FlightDetails
      v-else
      v-model:activeIcao="activeIcao"
      v-model:manualAirportId="manualAirportId"
      v-model:manualLat="manualLat"
      v-model:manualLon="manualLon"
      :selectedFlight="selectedFlight"
      :airports="airports"
      :activeFailure="activeFailure"
      :isEmergencySimulated="isEmergencySimulated"
      :isEmergency="isEmergency"
      :isReturningToStart="isReturningToStart"
      :isPaused="isPaused"
      :myFleetIcaos="myFleetIcaos"
      :animationSteps="animationSteps"
      :emergencyCountdown="emergencyCountdown"
      @recenter-map="$emit('recenter-map')"
      @return-to-start="$emit('return-to-start')"
      @toggle-pause="$emit('toggle-pause')"
      @trigger-simulated-failure="$emit('trigger-simulated-failure')"
      @handle-manual-emergency="$emit('handle-manual-emergency')"
      @set-manual-target="$emit('set-manual-target')"
    />
  </div>
</template>

<script setup>
/**
 * RightPanel.vue - Sağ Kontrol Paneli Ana Bileşeni
 * Bu bileşen, seçili uçuş durumuna göre Hedef Belirleme (TargetSelection) 
 * ve Uçuş Detayları (FlightDetails) arasında dinamik geçiş yapar.
 */
import TargetSelection from './RightPanel/TargetSelection.vue';
import FlightDetails from './RightPanel/FlightDetails.vue';

/**
 * PROPS: Üst bileşenden (App.vue) gelen statik ve dinamik veriler.
 * Bu veriler sadece okuma amaçlıdır (One-way data binding).
 */
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

/**
 * MODELS: Çift yönlü veri bağlama (Two-way binding).
 * Bu değişkenler değiştiğinde App.vue üzerindeki ana veriler de güncellenir.
 */
const activeIcao = defineModel('activeIcao');
const destinationAirportId = defineModel('destinationAirportId', { type: String });
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');
const manualAirportId = defineModel('manualAirportId', { type: String });
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');

/**
 * EMITS: Alt bileşenlerden gelen olayları (events) App.vue'ya iletir. Bu sayede harita üzerindeki fonksiyonlar sağ panelden tetiklenebilir.
 */
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
