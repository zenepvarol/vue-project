<template>
  <div class="flight-details">
    <div class="sidebar-header" style="padding: 0 0 15px 0;">
      <h3>Hedef Belirleme Sistemi</h3>
    </div>
    <div class="details-card active-focus" style="border-left: 4px solid #e74c3c;">
      <div class="manual-target-input" style="border-top: none; padding-top: 0;">
        <h4 style="color: #e74c3c;">VARILACAK HEDEF</h4>
        <p style="font-size: 11px; opacity: 0.7; margin-bottom: 15px;">
          Sistem, hedefe en yakın boştaki İHA'yı otomatik olarak sevk edecektir.
        </p>

        <input v-model="destinationAirportId" type="text" placeholder="Yeni Hedef Belirle"
          class="full-width-input" list="airport-list">

        <datalist id="airport-list">
          <option value="MANUAL_COORD"> Manuel Koordinat Girişi</option>
          <option v-for="ap in airports" :key="ap.id" :value="ap.id">{{ ap.name }} ({{ ap.id }})</option>
        </datalist>

        <div v-if="destinationAirportId === 'MANUAL_COORD'" class="input-row"
          style="margin-top: 10px; display: flex; gap: 5px;">
          <input v-model="destLat" type="number" placeholder="Enlem (Lat)" class="coord-input" step="0.000001"
            style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd; color: #333">
          <input v-model="destLon" type="number" placeholder="Boylam (Lon)" class="coord-input" step="0.000001"
            style="flex: 1; padding: 8px; font-size: 12px; border-radius: 4px; border: 1px solid #ddd; color: #333">
        </div>

        <button class="apply-target-btn" @click="$emit('assign-mission')"
          style="margin-top: 20px; background: #e74c3c; color: white; width: 100%; border: none; font-weight: bold; padding: 12px; border-radius: 6px;">
          HEDEFİ ONAYLA VE EN YAKIN BİRİMİ SEVK ET
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  airports: Array
});

const destinationAirportId = defineModel('destinationAirportId', { type: String });
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');

defineEmits(['assign-mission']);
</script>
