<template>
  <!-- v-container: Listenin tamamını sarmalayan ana kutu.
       px-8: Yanlardan 32px boşluk vererek içeriği merkeze yaklaştırır. / py-2: Üst ve alttan 8px boşluk bırakır.
       flex-grow-1: Panelde kalan tüm dikey boşluğu kaplamasını sağlar. / overflow-y-auto: Liste uzadığında scroll  çıkartır. -->
  <v-container fluid class="px-3 py-2 flex-grow-1 overflow-y-auto" style="scrollbar-width: thin;">
    <!-- v-row: Kartları yan yana dizen satır. / no-gutters: Vuetify'ın varsayılan geniş kolon boşluklarını iptal eder. -->
    <v-row no-gutters>
      <!-- v-col cols="6": Sayfayı 2'ye böler (12lik sistemin yarısı). Her satıra tam 2 kart sığdırır.
           pa-0: Dış boşlukları sıfırlar. / style="padding: 2px;": Kartların birbirine ortada yakın durmasını sağlar. -->
      <v-col v-for="f in store.filteredFlights" :key="f.icao24" cols="6" class="pa-0" style="padding: 2px;">
        <!-- v-card: Uçuş bilgilerini barındıran mini kart bileşeni. / variant: Seçili uçuşa göre arka plan rengini (tonal/flat) değiştirir. -->
        <v-card class="fleet-mini-card" density="compact" :variant="store.activeIcao === f.icao24 ? 'tonal' : 'flat'"
          :color="store.activeIcao === f.icao24 ? 'primary' : ''" @click="$emit('focus-flight', f)">
          <!-- v-card-text: Kartın iç içeriği için standart 8px-16px arası padding sağlar. -->
          <v-card-text class="pa-2">
            <!-- Üst Başlık: Uçuş kodu ve küçük durum ışığı (yeşil/kırmızı). -->
            <div class="d-flex justify-space-between align-center mb-1" style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">
              <span class="text-caption font-weight-bold" style="font-size: 11px; overflow: hidden; text-overflow: ellipsis;">{{ f.callsign || 'İHA' }}</span>
              <v-icon :color="f.energy < 20 ? 'error' : 'success'" size="8" class="ms-1">mdi-circle</v-icon>
            </div>

            <!-- Hız (kt) ve İrtifa (ft) bilgilerini yan yana dizen alan. -->
            <div class="d-flex justify-space-between mb-2 text-caption" style="font-size: 10px; opacity: 0.8; white-space: nowrap;">
              <span style="white-space: nowrap; display: inline-flex; align-items: center;"><v-icon icon="mdi-gauge" size="12" class="me-1" />{{ Math.round(f.velocity) }} kt</span>
              <span style="white-space: nowrap; display: inline-flex; align-items: center;"><v-icon icon="mdi-image-filter-hdr" size="12" class="me-1" />{{ Math.round(f.baroaltitude) }} ft</span>
            </div>

            <!-- v-progress-linear: Enerji/Batarya seviyesini gösteren renkli ince bar. -->
            <v-progress-linear :model-value="f.energy" :color="f.energy < 20 ? 'error' : 'success'" height="3" rounded />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup>
import { useFlightStore } from '@/stores/flightStore';

const store = useFlightStore();
defineEmits(['focus-flight']);
</script>