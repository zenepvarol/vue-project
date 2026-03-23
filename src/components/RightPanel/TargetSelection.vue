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
</template>

<script setup>
defineProps({
  airports: Array
});

const destinationAirportId = defineModel('destinationAirportId', { type: String });

defineEmits(['assign-mission']);
</script>
