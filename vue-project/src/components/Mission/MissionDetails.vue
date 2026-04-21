<template>
  <div class="flight-details">
    <v-btn icon variant="text" size="small" @click="activeIcao = null" class="position-absolute"
      style="top: 10px; right: 10px; z-index: 10;">
      <v-icon icon="mdi-close" color="grey-darken-1" />
    </v-btn>

    <div class="sidebar-header" style="padding: 0 0 15px 0;"> <!-- Üst başlık alanı -->
      <h3 class="text-subtitle-1 font-weight-bold">Uçuş Detayları</h3>
    </div>

    <div class="details-card active-focus">
      <div class="details-header d-flex justify-space-between align-center mb-4"> <!-- İsim ve odaklanma butonunun olduğu alan -->
        <div class="header-text">
          <h2 style="color: #e74c3c; font-size: 1.1rem; margin: 0;">{{ selectedFlight.callsign || 'Bilinmiyor' }}</h2>
          <span class="model-subtitle text-caption" style="color: #666; font-size: 0.75rem;">
            {{ selectedFlight.modelType || 'İnsansız Hava Aracı' }}
          </span>
        </div>
        <v-btn icon variant="elevated" color="primary" size="small" @click="$emit('recenter-map')" title="Uçağa Odaklan">
          <v-icon icon="mdi-navigation" style="transform: rotate(45deg);" />
        </v-btn>
      </div>

      <!-- details-grid: Teknik verilerin toplandığı alan -->
      <div class="details-grid" :class="{ 'link-loss-blur': activeFailure?.label.includes('SİNYAL') && isEmergencySimulated }">
        <div class="detail-item full-width mb-3">
          <label style="font-size: 11px; opacity: 0.7; color: #555;">
            <v-icon icon="mdi-pulse" size="14" /> Durum
          </label>
          <span :style="{ color: statusColor, fontWeight: 'bold' }" class="text-subtitle-2">
            {{ statusText }}
          </span>
        </div>

        <!-- Yakıt Göstergesi -->
        <div class="detail-item full-width mb-3">
          <label style="font-size: 11px; font-weight: bold; margin-bottom: 4px; display: block; color: #555;">
            <v-icon icon="mdi-battery" size="14" /> YAKIT (%{{ Math.round(selectedFlight.energy) }})
          </label>
          <v-progress-linear :model-value="selectedFlight.energy"
            :color="selectedFlight.energy < 20 ? 'error' : 'success'" height="8" rounded />
        </div>

        <div class="detail-item full-width mb-3" v-if="myFleetIcaos.includes(String(activeIcao)) || selectedFlight.isSiha">
          <label style="font-size: 11px; font-weight: bold; margin-bottom: 8px; display: block; color: #555;">
            <v-icon icon="mdi-bomb" size="14" /> MÜHİMMAT DURUMU
          </label>
          <div class="d-flex gap-2">
            <v-icon v-for="i in 2" :key="i" :icon="selectedFlight.ammo < i ? 'mdi-bomb-off' : 'mdi-bomb'"
              :color="selectedFlight.ammo < i ? 'grey-darken-1' : 'error'" size="28" />
          </div>
        </div>

        <!-- Hız ve Rakım  -->
        <v-row no-gutters class="mb-3">
          <v-col cols="6">
            <div class="detail-item">
              <label style="font-size: 11px; color: #555;"><v-icon icon="mdi-gauge" size="14" /> Hız</label>
              <div class="text-body-2 font-weight-bold">{{ Math.round(selectedFlight.velocity) }} kt</div>
            </div>
          </v-col>
          <v-col cols="6" class="text-right">
            <div class="detail-item">
              <label style="font-size: 11px; color: #555;"><v-icon icon="mdi-image-filter-hdr" size="14" /> Rakım</label>
              <div class="text-body-2 font-weight-bold">{{ Math.round(selectedFlight.baroaltitude) }} ft</div>
            </div>
          </v-col>
        </v-row>

        <!-- Mesafe Verisi: Normal uçuşta Gidilen/Toplam, Dönüşte ise Kalan mesafe gösterir -->
        <div class="detail-item mb-3" v-if="isReturningToStart || (selectedFlight.total_mission_dist || selectedFlight.trip_distance) > 0">
          <label style="font-size: 11px; color: #555;">
            <v-icon :icon="isReturningToStart ? 'mdi-home-import-outline' : 'mdi-map-marker'" size="14" /> 
            {{ isReturningToStart ? 'Ana Merkeze Uzaklık' : 'Mesafe (Gidilen / Toplam) ' }}
          </label>
          <span class="text-body-2 font-weight-bold">
            <template v-if="isReturningToStart">
              {{ Math.max(0, Math.round((selectedFlight.trip_distance || 0) - (selectedFlight.distance_from_dep || 0))) }} km
            </template>
            <template v-else>
              {{ Math.round(selectedFlight.distance_from_dep || 0) }} / {{ Math.round(selectedFlight.total_mission_dist || selectedFlight.trip_distance || 0) }} km
            </template>
          </span>
        </div>

        <!-- Yolculuk İlerlemesi: Orijinal hedefe veya mevcut rotaya göre progress bar -->
        <div v-if="(selectedFlight.total_mission_dist || selectedFlight.trip_distance) > 0" class="mt-2 text-primary">
          <div class="d-flex justify-space-between mb-1" style="font-size: 11px; font-weight: bold;">
            <span>{{ isReturningToStart ? 'DÖNÜŞ İLERLEMESİ' : 'YOLCULUK İLERLEMESİ' }}</span>
            <span v-if="isReturningToStart">
              %{{ Math.min(100, Math.round(((selectedFlight.distance_from_dep || 0) / (selectedFlight.trip_distance || 1)) * 100)) }}
            </span>
            <span v-else>
              %{{ Math.min(100, Math.round(((selectedFlight.distance_from_dep || 0) / (selectedFlight.total_mission_dist || selectedFlight.trip_distance || 1)) * 100)) }}
            </span>
          </div>
          <v-progress-linear 
            :model-value="Math.min(100, isReturningToStart 
              ? ((selectedFlight.distance_from_dep || 0) / (selectedFlight.trip_distance || 1)) * 100 
              : ((selectedFlight.distance_from_dep || 0) / (selectedFlight.total_mission_dist || selectedFlight.trip_distance || 1)) * 100)"
            color="primary" height="6" rounded class="no-transition-progress" />
        </div>
      </div>

      <div class="action-section d-flex flex-column gap-2 mt-4">
        <v-btn
          v-if="(animationSteps[activeIcao] > 0 || ((myFleetIcaos.includes(String(activeIcao)) || selectedFlight.isSiha) && selectedFlight.status !== 'STANDBY')) && !isReturningToStart && !isEmergency && !isEmergencySimulated"
          color="error" variant="outlined" block size="default" prepend-icon="mdi-restore" @click="$emit('return-to-start')">
          ANA MERKEZE DÖN
        </v-btn>

        <v-btn v-if="isPaused && animationSteps[activeIcao] === 0 && !myFleetIcaos.includes(String(activeIcao))"
          color="success" block size="default" prepend-icon="mdi-play" @click="$emit('toggle-pause')">
          KALKIŞ ONAYI VER
        </v-btn>

        <v-btn v-if="!isPaused && !isEmergencySimulated && !isEmergency && !isReturningToStart" color="warning" block
          size="default" prepend-icon="mdi-alert" @click="$emit('trigger-simulated-failure')">
          ARIZA SİMÜLE ET
        </v-btn>

        <!-- Acil Durum Alanı -->
        <div v-if="isEmergencySimulated" class="pa-2 rounded-lg" style="border: 2px solid #e74c3c;">
          <div class="text-caption text-center font-weight-bold mb-2 blink-text" style="color: #e74c3c;">
            <v-icon icon="mdi-alert-octagon" /> {{ activeFailure?.label || 'SİSTEM ARIZASI!' }} ({{ emergencyCountdown }}s)
          </div>
          <v-btn color="error" block size="default" @click="$emit('handle-manual-emergency')">ACİL İNİŞ YAP</v-btn>
        </div>
      </div>

      <div class="manual-target-input mt-8 pt-4" style="border-top: 1px solid rgba(0,0,0,0.1);">
        <h4 style="margin-bottom: 20px !important; font-size: 0.95rem; font-weight: bold;">Operasyonu Güncelle</h4>
        <v-autocomplete v-model="manualAirportId" label="Yeni Hedef Seç" variant="outlined" density="compact"
          :items="[{ id: 'MANUAL_COORD', name: 'Manuel Koordinat Girişi' }, ...airports]"
          :item-title="item => item.id === 'MANUAL_COORD' ? item.name : (item.name && item.id ? `${item.name} (${item.id})` : (item.name || item.id || ''))"
          item-value="id" :custom-filter="(value, query, item) => {
            const q = query.toLowerCase();
            const nm = (item.raw.name || '').toLowerCase();
            const cid = (item.raw.id || '').toLowerCase();
            return nm.includes(q) || cid.includes(q);
          }" hide-details class="mb-2" />

        <v-row v-if="manualAirportId === 'MANUAL_COORD'" dense class="mt-2 mb-2">
          <v-col cols="6"><v-text-field v-model="manualLat" label="Lat" type="number" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="6"><v-text-field v-model="manualLon" label="Lon" type="number" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>

        <v-btn color="primary" block size="default" prepend-icon="mdi-arrow-right-bold" @click="$emit('set-manual-target')"
          class="font-weight-bold text-none" style="margin-top: 10px !important;">
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
