<template>
  <v-container class="pa-4">
    <div style="height: 30px;"></div> <!-- Üst Boşluk -->
    <div class="text-center" style="font-size: 16px !important; font-weight: 900 !important; margin-bottom: 30px !important;">
      Hedef Belirleme Sistemi
    </div>

    <v-row no-gutters justify="center">
      <v-col cols="12" class="d-flex justify-center">
        
        <v-card class="details-card pa-4 elevation-2" style="border-left: 4px solid #e74c3c; width: 250px;">
          <div class="text-subtitle-2 text-error font-weight-bold mb-1">VARILACAK HEDEF</div>
                    <p class="text-caption opacity-70 mb-4">Sistem, en yakın boştaki İHA'yı otomatik sevk eder.</p>

          <!-- v-autocomplete: İçinde arama yapılabilen (searchable) seçim kutusu -->
          <!-- density="compact": Kutuyu daraltır ve yer kazandırır; variant="outlined": Çizgili modern görünüm -->
          <v-autocomplete v-model="destinationAirportId" label="Hedef Seçin" color="error" variant="outlined"
            density="compact" :items="[{ id: 'MANUAL_COORD', name: 'Manuel Giriş' }, ...airports]" 
            :item-title="item => item.id === 'MANUAL_COORD' ? item.name : (item.name && item.id ? `${item.name} (${item.id})` : (item.name || item.id || ''))"
            item-value="id" :custom-filter="(value, query, item) => {
              const q = query.toLowerCase();
              const nm = (item.raw.name || '').toLowerCase();
              const cid = (item.raw.id || '').toLowerCase();
              return nm.includes(q) || cid.includes(q);
            }" class="mb-2" hide-details />

          <!-- Koordinat Girişi: Sadece 'Manuel Giriş' seçildiğinde açılan Lat/Lon alanları -->
          <v-row v-if="destinationAirportId === 'MANUAL_COORD'" dense class="mt-2">
            <v-col cols="6"> <!-- Ekranı tam ortadan bölerek Lat ve Lon kutularını yan yana dizmeyi sağlar -->
              <v-text-field v-model="destLat" label="Lat" type="number" variant="outlined" density="compact" hide-details color="error" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="destLon" label="Lon" type="number" variant="outlined" density="compact" hide-details color="error" />
            </v-col>
          </v-row>

          <!-- v-btn: Ana buton bileşeni; prepend-icon ile butonun soluna navigasyon ikonu yerleştirilir -->
          <v-btn color="error" block size="large" prepend-icon="mdi-navigation-variant" @click="$emit('assign-mission')"
            class="mt-10 font-weight-bold text-none">
            HEDEFİ ONAYLA VE SEVK ET
          </v-btn>
        </v-card>

      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>

import { defineProps, defineEmits } from 'vue';

// PROPS: Dışarıdan gelen 'havalimanları' dizisini bileşen içinde kullanılabilir hale getirir
defineProps({
  airports: Array
});

// MODELS: İki yönlü veri bağlama (v-model) - Harita ve App.vue ile senkronize çalışır.
const destinationAirportId = defineModel('destinationAirportId', { type: String });
const destLat = defineModel('destLat');
const destLon = defineModel('destLon');

// defineEmits: Bileşenden dışarıya (App.vue'ya) sinyal/olay gönderir.
// 'assign-mission': Hedef belirleme butonu tıklandığında tetiklenir.
defineEmits(['assign-mission']);
</script>
