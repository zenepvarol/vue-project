<template>
  <div class="flight-details">
    <div class="sidebar-header">
      <div class="header-top">
        <h3>Uçuş Detayları</h3>
        <button class="close-button" @click="activeIcao = null" title="Kapat">
          <i class="mdi mdi-close" style="font-size: 20px;"></i>
        </button>
      </div>
    </div>

    <div class="details-card active-focus">
      <div class="details-header">
        <div class="header-text">
          <h2>{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
          <span class="model-subtitle">{{ selectedFlight.modeltype || 'İnsansız Hava Aracı' }}</span>
        </div>
        <button class="locate-mini-button" @click="$emit('recenter-map')" title="Uçağa Odaklan">
          <i class="mdi mdi-navigation" style="font-size: 18px;"></i>
        </button>
      </div>

      <div class="details-grid"
        :class="{ 'link-loss-blur': activeFailure?.label.includes('SİNYAL') && isEmergencySimulated }">
        <div class="detail-item full-width">
          <label>
            <i class="mdi mdi-pulse" style="font-size: 14px;"></i> Durum
          </label>
          <span
            :style="{ color: statusColor, fontWeight: 'bold' }">
            {{ statusText }}
          </span>
        </div>

        <div class="detail-item full-width">
          <label>
            <i class="mdi mdi-battery" style="font-size: 14px;"></i> YAKIT (%{{ Math.round(selectedFlight.energy) }})
          </label>
          <div class="progress-bar energy-bar">
            <div class="progress-fill"
              :style="{ width: selectedFlight.energy + '%', backgroundColor: selectedFlight.energy < 20 ? '#e74c3c' : '#2ecc71' }">
            </div>
          </div>
        </div>

        <div class="detail-item full-width" v-if="myFleetIcaos.includes(String(activeIcao))">
          <label>
            <i class="mdi mdi-crosshairs-gps" style="font-size: 14px;"></i> MÜHİMMAT DURUMU
          </label>
          <div class="ammo-container">
            <div v-for="i in 2" :key="i" class="ammo-icon" :class="{ 'used': selectedFlight.ammo < i }">
              <i class="mdi mdi-bomb" style="font-size: 24px;"></i>
            </div>
          </div>
        </div>

        <div class="details-row-inline">
          <div class="detail-item"><label>
              <i class="mdi mdi-gauge" style="font-size: 14px;"></i> Hız
            </label><span>{{ Math.round(selectedFlight.velocity) }} kt</span></div>
          <div class="detail-item"><label>
              <i class="mdi mdi-image-filter-hdr" style="font-size: 14px;"></i> Rakım
            </label><span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span></div>
        </div>

        <div class="detail-item">
          <label>
            <i class="mdi mdi-map-marker" style="font-size: 14px;"></i> Mesafe (Gidilen / Toplam)
          </label>
          <span>{{ Math.round(selectedFlight.distance_from_dep) }} / {{ Math.round(selectedFlight.trip_distance) }}
            km</span>
        </div>

        <div v-if="selectedFlight.trip_distance > 0" class="detail-item progress-container full-width">
          <label>Yolun %{{ Math.round((selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100) }}
            tamamlandı</label>
          <div class="progress-bar">
            <div class="progress-fill"
              :style="{ width: (selectedFlight.distance_from_dep / selectedFlight.trip_distance) * 100 + '%' }">
            </div>
          </div>
        </div>
      </div>

      <div class="action-section">
        <button
          v-if="(animationSteps[activeIcao] > 0 || (myFleetIcaos.includes(String(activeIcao)) && selectedFlight.status !== 'STANDBY')) && !isReturningToStart && !isEmergency && !isEmergencySimulated"
          class="returnhome-button" @click="$emit('return-to-start')">
          <i class="mdi mdi-restore" style="font-size: 16px;"></i> ANA MERKEZE DÖN
        </button>
        <button v-if="isPaused && animationSteps[activeIcao] === 0 && !myFleetIcaos.includes(String(activeIcao))" class="pause-button paused"
          @click="$emit('toggle-pause')">
          <i class="mdi mdi-play" style="font-size: 16px;"></i> KALKIŞ ONAYI VER
        </button>
        <button v-if="!isPaused && !isEmergencySimulated && !isEmergency && !isReturningToStart"
          class="simulate-btn" @click="$emit('trigger-simulated-failure')">
          <i class="mdi mdi-alert" style="font-size: 16px;"></i> ARIZA SİMÜLE ET
        </button>

        <div v-if="isEmergencySimulated" class="emergency-decision-box">
          <div class="emergency-warning-text" :style="{ color: activeFailure?.color || '#e74c3c' }">
            <i class="mdi mdi-alert-octagon pulse-icon" style="font-size: 18px;"></i>
            {{ activeFailure?.label || 'SİSTEM ARIZASI!' }} ({{ emergencyCountdown }}s)
          </div>
          <button class="emergency-button" @click="$emit('handle-manual-emergency')">ACİL İNİŞ YAP</button>
        </div>
      </div>

      <div class="manual-target-input">
        <h4>Operasyonu Güncelle</h4>
        <input v-model="manualAirportId" type="text" placeholder="Yeni Hedef Belirle" class="full-width-input"
          list="airport-list-manual">
        <datalist id="airport-list-manual">
          <option value="MANUAL_COORD"> Manuel Koordinat Girişi</option>
          <option v-for="ap in airports" :key="ap.id" :value="ap.id">{{ ap.name }} ({{ ap.id }})</option>
        </datalist>

        <div v-if="manualAirportId === 'MANUAL_COORD'" class="input-row"
          style="margin-top: 10px; display: flex; gap: 5px;">
          <input v-model="manualLat" type="number" placeholder="Enlem (Lat)" class="coord-input" step="0.000001"
            style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd;">
          <input v-model="manualLon" type="number" placeholder="Boylam (Lon)" class="coord-input" step="0.000001"
            style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd;">
        </div>
        <button class="apply-target-btn" @click="$emit('set-manual-target')" style="margin-top: 10px; width: 100%;">HEDEFE
          YÖNLENDİR </button>
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

defineEmits([
  'recenter-map',
  'return-to-start',
  'toggle-pause',
  'trigger-simulated-failure',
  'handle-manual-emergency',
  'set-manual-target'
]);

const STATUS_CONFIGS = {
  EMERGENCY: { text: 'ACİL İNİŞTE', color: '#e74c3c' },
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
  if (props.selectedFlight.status === 'MISSION_COMPLETE') return 'MISSION_COMPLETE';
  if (props.selectedFlight.status === 'ARRIVED' || props.selectedFlight.status === 'COMPLETED') return 'ARRIVED';
  if (props.selectedFlight.status === 'STANDBY') return 'STANDBY';
  if (props.isPaused) return 'PAUSED';
  return 'ACTIVE';
});

const statusText = computed(() => STATUS_CONFIGS[currentFlightState.value].text);
const statusColor = computed(() => STATUS_CONFIGS[currentFlightState.value].color);
</script>
