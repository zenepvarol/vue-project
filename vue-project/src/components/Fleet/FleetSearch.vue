<template>
  <!-- v-sheet: Bileşenin ana kapsayıcısı. Sayfa arka planına uyumlu, şeffaf bir temel oluşturur. -->
  <v-sheet class="sidebar-header px-0 pt-4 pb-2" color="transparent">
    <!-- 1. Satır: Başlık ve Karanlık Mod Butonu -->
    <!-- no-gutters: Gereksiz sütun boşluklarını siler.
         justify="center": Tüm içeriği yatayda merkeze toplar.
         px-4: Sol-sağdan 16px iç boşluk verir.
         mb-1: Altına 4px dış boşluk bırakır. -->
    <v-row no-gutters justify="center" class="px-4 mb-1">
      <!-- cols="12": Tam genişlik kapla (satırın tamamını kullan).
           style="max-width: 280px;": Genişliği 280px ile sınırla.
           d-flex: İçindeki elemanları (yazı ve buton) yan yana diz.
           justify-space-between: Yazıyı en sola, butonu en sağa it.
           align-center: Yazı ve butonu dikeyde aynı hizada tut. -->
      <v-col cols="12" style="max-width: 280px;" class="d-flex justify-space-between align-center">
        <div class="text-subtitle-1 font-weight-bold">Uçuş Listesi</div>
        <div class="d-flex align-center gap-1">
          <!-- v-btn: Karanlık/Aydınlık modu değiştiren buton. -->
          <v-btn icon variant="text" size="small" @click="store.toggleDarkMode" id="dark-mode-toggle">
            <v-icon :color="store.darkMode ? 'yellow-darken-2' : 'indigo-darken-1'"
              :icon="store.darkMode ? 'mdi-weather-sunny' : 'mdi-moon-waxing-crescent'" />
            <v-tooltip activator="parent" location="bottom">
              {{ store.darkMode ? 'Aydınlık Mod' : 'Karanlık Mod' }}
            </v-tooltip>
          </v-btn>

          <!-- Logout Butonu -->
          <v-btn icon variant="text" size="small" color="error" @click="handleLogout" id="logout-btn">
            <v-icon icon="mdi-logout" />
            <v-tooltip activator="parent" location="bottom">Çıkış Yap</v-tooltip>
          </v-btn>
        </div>
      </v-col>
    </v-row>

    <!-- 2. Satır: Arama Çubuğu (Input) -->
    <!-- justify="center": Arama çubuğunu da başlıkla aynı hizada tutmak için ortalar. -->
    <v-row no-gutters justify="center" class="px-4">
      <v-col cols="12" style="max-width: 280px;"> <!-- Arama çubuğunu da başlık kutusuyla aynı boyda tutar. -->
        <!-- v-text-field: Arama giriş alanı. -->
        <!-- variant="outlined": Kenarlıklı modern görünüm.
             density="compact": Daha az yer kaplayan, ince tasarım.
             hide-details: Altındaki hata mesajı boşluğunu gizler.
             prepend-inner-icon: Sol tarafa mercek ikonu. -->
        <v-text-field v-model="store.searchQuery" placeholder="Uçuş ara (Callsign)..." prepend-inner-icon="mdi-magnify"
          variant="outlined" density="compact" hide-details color="primary" bg-color="surface-light" />
      </v-col>
    </v-row>
  </v-sheet>

  <v-divider /> <!-- Header ile altındaki listeyi ayıran ince çizgi. -->
</template>

<script setup>
// useFlightStore: Uçuş verilerini ve ayarları (dark mode, arama sorgusu vs.) yöneten depoyu içeri aktarır.
import { useFlightStore } from '@/stores/flightStore';
import { useAuthStore } from '@/stores/authStore';
import { useRouter } from 'vue-router';

// 'store' değişkeni üzerinden tüm uçuş verilerine ve fonksiyonlarına (toggleDarkMode gibi) erişiriz.
const store = useFlightStore();
const authStore = useAuthStore();
const router = useRouter();

const handleLogout = () => {
  authStore.logout();
  router.push('/login');
};
</script>
