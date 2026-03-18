<template>
  <div class="sidebar right" style="overflow-y: auto; max-height: 100vh;">
    <div v-if="!selectedFlight" class="flight-details">
      <div class="sidebar-header" style="padding: 0 0 15px 0;">
        <h3>Görev Kontrol Merkezi</h3>
      </div>
      <div class="details-card active-focus" style="border-left: 4px solid #e74c3c;">
        <div class="manual-target-input" style="border-top: none; padding-top: 0;">
          <h4 style="color: #e74c3c;">VARILACAK HEDEF</h4>
          <p style="font-size: 11px; opacity: 0.7; margin-bottom: 15px;">
            Sistem, hedefe en yakın boştaki İHA'yı otomatik olarak sevk edecektir.
          </p>

          <input v-model="destinationAirportId" type="text" placeholder="Havalimanı Kodu Girin (Örn: LTBI)"
            class="full-width-input" list="airport-list">

          <datalist id="airport-list">
            <option v-for="ap in airports" :key="ap.id" :value="ap.id">{{ ap.name }} ({{ ap.city }})</option>
          </datalist>

          <button class="apply-target-btn" @click="$emit('assign-mission')"
            style="margin-top: 20px; background: #e74c3c; color: white; width: 100%; border: none; font-weight: bold; padding: 12px; border-radius: 6px;">
            HEDEFİ ONAYLA VE EN YAKIN BİRİMİ SEVK ET
          </button>
        </div>
      </div>
    </div>

    <div v-else class="flight-details">
      <div class="sidebar-header">
        <div class="header-top">
          <h3>Uçuş Detayları</h3>
          <button class="close-button" @click="activeIcao = null" title="Kapat">
            <X :size="20" />
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
            <Navigation :size="18" />
          </button>
        </div>

        <div class="details-grid"
          :class="{ 'link-loss-blur': activeFailure?.label.includes('SİNYAL') && isEmergencySimulated }">
          <div class="detail-item full-width">
            <label>
              <Activity :size="14" /> Durum
            </label>
            <span
              :style="{ color: isEmergency ? '#e74c3c' : (isReturningToStart ? '#3498db' : (isPaused ? '#f39c12' : '#2ecc71')), fontWeight: 'bold' }">
              {{ isEmergency ? 'ACİL İNİŞTE' : (isReturningToStart ? 'ANA MERKEZE DÖNÜLÜYOR' : (isPaused ? 'DURDURULDU / BEKLEMEDE' : 'GÖREVDE')) }}
            </span>
          </div>

          <div class="detail-item full-width">
            <label>
              <Battery :size="14" /> YAKIT (%{{ Math.round(selectedFlight.energy) }})
            </label>
            <div class="progress-bar energy-bar">
              <div class="progress-fill"
                :style="{ width: selectedFlight.energy + '%', backgroundColor: selectedFlight.energy < 20 ? '#e74c3c' : '#2ecc71' }">
              </div>
            </div>
          </div>

          <div class="detail-item full-width" v-if="myFleetIcaos.includes(String(activeIcao))">
            <label>
              <Crosshair :size="14" /> MÜHİMMAT DURUMU
            </label>
            <div class="ammo-container">
              <div v-for="i in 2" :key="i" class="ammo-icon" :class="{ 'used': selectedFlight.ammo < i }">
                <Bomb :size="24" />
              </div>
            </div>
          </div>

          <div class="details-row-inline">
            <div class="detail-item"><label>
                <Gauge :size="14" /> Hız
              </label><span>{{ Math.round(selectedFlight.velocity) }} kt</span></div>
            <div class="detail-item"><label>
                <Mountain :size="14" /> Rakım
              </label><span>{{ Math.round(selectedFlight.baroaltitude) }} ft</span></div>
          </div>

          <div class="detail-item">
            <label>
              <MapPin :size="14" /> Mesafe (Gidilen / Toplam)
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
            <RotateCcw :size="16" /> ANA MERKEZE DÖN
          </button>
          <button v-if="isPaused && animationSteps[activeIcao] === 0" class="pause-button paused"
            @click="$emit('toggle-pause')">
            <Play :size="16" /> KALKIŞ ONAYI VER
          </button>
          <button v-if="!isPaused && !isEmergencySimulated && !isEmergency && !isReturningToStart"
            class="simulate-btn" @click="$emit('trigger-simulated-failure')">
            <AlertTriangle :size="16" /> ARIZA SİMÜLE ET
          </button>

          <div v-if="isEmergencySimulated" class="emergency-decision-box">
            <div class="emergency-warning-text" :style="{ color: activeFailure?.color || '#e74c3c' }">
              <AlertOctagon :size="18" class="pulse-icon" />
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
  </div>
</template>

<script setup>
import {
  Gauge, Mountain, MapPin, Navigation, AlertOctagon, Play, RotateCcw,
  X, Activity, AlertTriangle, Bomb, Crosshair, Battery
} from 'lucide-vue-next';

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

const activeIcao = defineModel('activeIcao');
const destinationAirportId = defineModel('destinationAirportId', { type: String });
const manualAirportId = defineModel('manualAirportId', { type: String });
const manualLat = defineModel('manualLat');
const manualLon = defineModel('manualLon');

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
