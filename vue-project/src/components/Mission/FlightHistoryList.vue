<script setup>
import { useFlightStore } from '@/stores/flightStore';
import { computed } from 'vue';

const store = useFlightStore(); // Merkezi uçuş verisi deposuna (Pinia Store) bağlanır.

const formatDate = (dateStr) => {
  if (!dateStr) return "-";
  const date = new Date(dateStr); // Zamanı yerel TR formatına çevirir.
  return date.toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  });
};

const history = computed(() => store.selectedFlightHistory); // Store'daki uçuş geçmişi verisini takip eder.
</script>

<template>
  <v-card class="mt-6 border rounded-lg bg-white" elevation="1">
    <v-card-title class="d-flex justify-space-between align-center bg-grey-lighten-5 pa-2 border-b">
      <div class="d-flex align-center">
        <span style="font-size: 12px;" class="font-weight-black text-grey-darken-2">UÇUŞ GEÇMİŞİ</span>
      </div>
      <!-- Sağ üstteki kayıt sayısı -->
      <v-chip size="x-small" variant="text" class="text-grey font-weight-bold">
        {{ history.length }} Kayıt
      </v-chip>
    </v-card-title>

    <v-card-text class="pa-0">
      <!-- Veri yoksa gösterilecek bilgilendirme metni -->
      <div v-if="history.length === 0" class="pa-10 text-center text-grey">
        <div class="text-caption">Henüz bir uçuş verisi kaydedilmedi.</div>
      </div>

      <!-- Kayıt listesi: Kaydırılabilir, Vuetify standart tablosu -->
      <v-table v-else density="compact" fixed-header hover height="300px" class="bg-white">
        <thead>
          <tr>
            <th style="font-size: 12px !important;" class="font-weight-black text-grey-darken-1 bg-grey-lighten-4">ZAMAN</th>
            <th style="font-size: 12px !important;" class="font-weight-black text-grey-darken-1 bg-grey-lighten-4">ROTA</th>
            <th style="font-size: 12px !important;" class="font-weight-black text-grey-darken-1 bg-grey-lighten-4 text-right">MESAFE</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in history" :key="item.id">
            <td class="text-caption text-grey-darken-1">{{ formatDate(item.flightDate) }}</td>
            <td>
              <div class="d-flex align-center py-2">
                <span class="text-body-2">{{ item.departure }}</span>
                <v-icon icon="mdi-arrow-right-thin" size="small" color="grey-lighten-1" class="mx-1" />
                <span class="text-body-2 font-weight-bold text-primary">{{ item.arrival }}</span>
              </div>
            </td>
            <td class="text-right">
              <span class="text-caption font-weight-bold text-grey-darken-2">
                {{ item.distance }} km
              </span>
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card-text>
  </v-card>
</template>

<style scoped>
/* Vuetify v-table başlık yükseklik ve renk özelleştirmesi */
:deep(.v-table th) {
  background-color: #f8fafc !important;
  height: 32px !important;
}
</style>
