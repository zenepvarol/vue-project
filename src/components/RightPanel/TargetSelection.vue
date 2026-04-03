<template>
  <div class="flight-details">
    <div class="sidebar-header" style="padding: 0 0 15px 0;">
      <h3>Hedef Belirleme Sistemi</h3>
    </div>
    <div class="details-card active-focus" style="border-left: 4px solid #e74c3c;">
      <div class="manual-target-input" style="border-top: none; padding-top: 0;">
        <h4 style="color: #e74c3c; margin-bottom: 5px;">VARILACAK HEDEF</h4>
        <p style="font-size: 11px; opacity: 0.7; margin-bottom: 12px;">Sistem, en yakın boştaki İHA'yı otomatik sevk
          eder.</p>

        <v-autocomplete v-model="destinationAirportId" label="Hedef Seçin" color="error" variant="outlined"
          density="compact" :items="[{ id: 'MANUAL_COORD', name: 'Manuel Giriş' }, ...airports]" 
          :item-title="item => item.id === 'MANUAL_COORD' ? item.name : (item.name && item.id ? `${item.name} (${item.id})` : (item.name || item.id || ''))"
          item-value="id" :custom-filter="(value, query, item) => {
            const q = query.toLowerCase();
            const nm = (item.raw.name || '').toLowerCase();
            const cid = (item.raw.id || '').toLowerCase();
            return nm.includes(q) || cid.includes(q);
          }" class="mb-2" hide-details />

        <div v-if="destinationAirportId === 'MANUAL_COORD'" class="d-flex gap-2 mt-2">
          <v-text-field v-model="destLat" label="Lat" type="number" variant="outlined" density="compact" hide-details
            color="error" />
          <v-text-field v-model="destLon" label="Lon" type="number" variant="outlined" density="compact" hide-details
            color="error" />
        </div>

        <v-btn color="error" block size="default" prepend-icon="mdi-navigation-variant" @click="$emit('assign-mission')"
          class="mt-6" style="text-transform: none; font-weight: bold; margin-top: 45px !important;">
          HEDEFİ ONAYLA VE SEVK ET
        </v-btn>
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
