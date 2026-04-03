<template>
  <div class="flight-details">
    <v-btn icon variant="text" size="small" @click="activeIcao = null" title="Kapat" style="position: absolute; top: 10px; right: 10px; z-index: 10;"><v-icon icon="mdi-close" color="grey-darken-1" /></v-btn>
    <div class="sidebar-header">
      <div class="header-top">
        <h3>Uçuş Detayları</h3>
      </div>
    </div>

    <div class="details-card active-focus">
      <div class="details-header">
        <div class="header-text">
          <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
          <span class="model-subtitle">{{ selectedFlight.modeltype || 'İnsansız Hava Aracı' }}</span>
        </div>
        <v-btn icon variant="elevated" color="primary" size="small" @click="$emit('recenter-map')"
          title="Uçağa Odaklan">
          <v-icon icon="mdi-navigation" style="transform: rotate(45deg);" />
        </v-btn>
      </div>

      <div class="details-grid"
        :class="{ 'link-loss-blur': activeFailure?.label.includes('SİNYAL') && isEmergencySimulated }">
        <div class="detail-item full-width">
          <label>
            <i class="mdi mdi-pulse" style="font-size: 14px;"></i> Durum
          </label>
          <span :style="{ color: statusColor, fontWeight: 'bold' }">
            {{ statusText }}
          </span>
        </div>

        <div class="detail-item full-width"><label><v-icon icon="mdi-battery" size="14" /> YAKIT (%{{
          Math.round(selectedFlight.energy) }})</label>
          <v-progress-linear :model-value="selectedFlight.energy"
            :color="selectedFlight.energy < 20 ? 'error' : 'success'" height="8" rounded />
        </div>

        <div class="detail-item full-width" v-if="myFleetIcaos.includes(String(activeIcao))"><label><v-icon
              icon="mdi-bomb" size="14" /> MÜHİMMAT DURUMU</label>
          <div class="ammo-container d-flex gap-2"><v-icon v-for="i in 2" :key="i"
              :icon="selectedFlight.ammo < i ? 'mdi-bomb-off' : 'mdi-bomb'"
              :color="selectedFlight.ammo < i ? 'grey-darken-1' : 'error'" size="28" /></div>
        </div>

        <div class="details-row-inline d-flex justify-space-between mt-1">
          <div class="detail-item"><label><v-icon icon="mdi-gauge" size="14" /> Hız</label><span>{{
            Math.round(selectedFlight.velocity) }} kt</span></div>
          <div class="detail-item text-right"><label><v-icon icon="mdi-image-filter-hdr" size="14" />
              Rakım</label><span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span></div>
        </div>

        <div class="detail-item mt-1">
          <label><v-icon icon="mdi-map-marker" size="14" /> Mesafe (Gidilen / Toplam)</label>
          <span>{{ Math.round(selectedFlight.distance_from_dep) }} / {{ Math.round(selectedFlight.trip_distance) }}
            km</span>
        </div>

        <div v-if="selectedFlight.trip_distance > 0" class="mt-2">
          <div class="d-flex justify-space-between mb-1"
            style="font-size: 11px; font-weight: bold; color: var(--v-theme-primary);">
            <span>YOLCULUK İLERLEMESİ</span>
            <span>%{{ Math.round((selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100) }}</span>
          </div>
          <v-progress-linear :model-value="(selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100"
            color="primary" height="6" rounded />
        </div>
      </div>

      <div class="action-section d-flex flex-column gap-2 mt-2">
        <v-btn
          v-if="(animationSteps[activeIcao] > 0 || (myFleetIcaos.includes(String(activeIcao)) && selectedFlight.status !== 'STANDBY')) && !isReturningToStart && !isEmergency && !isEmergencySimulated"
          color="error" variant="outlined" block size="small" prepend-icon="mdi-restore"
          @click="$emit('return-to-start')">ANA MERKEZE DÖN</v-btn>

        <v-btn v-if="isPaused && animationSteps[activeIcao] === 0 && !myFleetIcaos.includes(String(activeIcao))"
          color="success" block size="default" prepend-icon="mdi-play" @click="$emit('toggle-pause')">KALKIŞ ONAYI
          VER</v-btn>

        <v-btn v-if="!isPaused && !isEmergencySimulated && !isEmergency && !isReturningToStart" color="warning" block
          size="small" prepend-icon="mdi-alert" @click="$emit('trigger-simulated-failure')">ARIZA SİMÜLE ET</v-btn>

        <div v-if="isEmergencySimulated" class="emergency-decision-box border-error pa-2">
          <div class="emergency-warning-text" :style="{ color: activeFailure?.color || '#e74c3c', fontSize: '12px' }">
            <v-icon icon="mdi-alert-octagon" class="pulse-icon" /> {{ activeFailure?.label || 'SİSTEM ARIZASI!' }} ({{
              emergencyCountdown }}s)
          </div>
          <v-btn color="error" block size="small" @click="$emit('handle-manual-emergency')">ACİL İNİŞ YAP</v-btn>
        </div>
      </div>

      <div class="manual-target-input mt-6">
        <h4 style="margin-bottom: 20px !important;">Operasyonu Güncelle</h4>
        <v-autocomplete v-model="manualAirportId" label="Yeni Hedef Seç" variant="outlined" density="compact"
          :items="[{ id: 'MANUAL_COORD', name: 'Manuel Koordinat Girişi' }, ...airports]"
          :item-title="item => item.id === 'MANUAL_COORD' ? item.name : (item.name && item.id ? `${item.name} (${item.id})` : (item.name || item.id || ''))"
          item-value="id" :custom-filter="(value, query, item) => {
            const q = query.toLowerCase();
            const nm = (item.raw.name || '').toLowerCase();
            const cid = (item.raw.id || '').toLowerCase();
            return nm.includes(q) || cid.includes(q);
          }" hide-details />

        <div v-if="manualAirportId === 'MANUAL_COORD'" class="d-flex gap-1 mt-2">
          <v-text-field v-model="manualLat" label="Lat" type="number" variant="outlined" density="compact"
            hide-details />
          <v-text-field v-model="manualLon" label="Lon" type="number" variant="outlined" density="compact"
            hide-details />
        </div>

        <v-btn color="primary" block size="small" prepend-icon="mdi-arrow-right-bold"
          @click="$emit('set-manual-target')"
          style="text-transform: none; font-weight: bold; margin-top: 15px !important;">
          HEDEFE YÖNLENDİR
        </v-btn>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue';

const props = defineProps({
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

const activeIcao = defineModel('activeIcao');
const manualAirportId = defineModel('manualAirportId', { type: String });
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');

defineEmits(['recenter-map', 'return-to-start', 'toggle-pause', 'trigger-simulated-failure', 'handle-manual-emergency', 'set-manual-target']);

const STATUS_CONFIGS = {
  EMERGENCY: { text: 'ACİL İNİŞTE', color: '#e74c3c' },
  EMERGENCY_LANDED: { text: 'ACİL İNİŞ YAPTI', color: '#e67e22' },
  RETURNING: { text: 'ANA MERKEZE DÖNÜLÜYOR', color: '#3498db' },
  MISSION_COMPLETE: { text: 'HEDEF İMHA EDİLDİ', color: '#2ecc71' },
  ARRIVED: { text: 'HEDEFE VARILDI', color: '#2ecc71' },
  STANDBY: { text: 'HANGARDA BEKLEMEDE', color: '#95a5a6' },
  PAUSED: { text: 'DURDURULDU / BEKLEMEDE', color: '#f39c12' },
  ACTIVE: { text: 'GÖREVDE', color: '#2ecc71' }
};

const currentFlightState = computed(() => {
  if (props.isEmergency) return 'EMERGENCY';
  if (props.isReturningToStart) return 'RETURNING';
  if (props.selectedFlight.status === 'EMERGENCY_LANDED') return 'EMERGENCY_LANDED';
  if (props.selectedFlight.status === 'MISSION_COMPLETE') return 'MISSION_COMPLETE';
  if (props.selectedFlight.status === 'ARRIVED' || props.selectedFlight.status === 'COMPLETED') return 'ARRIVED';
  if (props.selectedFlight.status === 'STANDBY') return 'STANDBY';
  if (props.isPaused) return 'PAUSED';
  return 'ACTIVE';
});

const statusText = computed(() => STATUS_CONFIGS[currentFlightState.value].text);
const statusColor = computed(() => STATUS_CONFIGS[currentFlightState.value].color);
</script>
