/** Pinia kütüphanesi kullanılarak oluşturulan bu depo (store), uçuş verilerinin, kullanıcı
 * tercihlerinin ve global arayüz durumlarının tüm bileşenler arasında senkronize kalmasını sağlar. */
import { defineStore } from 'pinia';
import { ucakService } from '@/api/ucakService';
import { flightHistoryService } from '@/api/flightHistoryService';

export const useFlightStore = defineStore('flight', {
  /** STATE: Uygulamanın belleğinde tutulan reaktif veri tabanıdır.
   * Bu veriler değiştiğinde, onlara bağlı olan tüm bileşenler (UI) otomatik güncellenir. */
  state: () => ({
    searchQuery: '', // Sol paneldeki uçuş arama filtresi
    darkMode: false, // Uygulamanın gece/gündüz teması tercihi
    sidebarOpen: true, // Sol panelin (Flight List) açık/kapalı olma durumu
    activeIcao: null, // Haritada ve sağ panelde odaklanılan uçağın ICAO kodu
    currentFlights: {}, // Havada olan tüm aktif İHA/uçak verilerinin tutulduğu obje
    selectedFlightHistory: [] // Seçili uçağın geçmiş uçuş kayıtları
  }),

  /** GETTERS: Mevcut veriden (state) türetilen hesaplanmış (computed) değerlerdir.
   * Orijinal veriyi bozmadan, ihtiyaca göre filtrelenmiş veya işlenmiş veri döndürür.*/
  getters: {
    //'searchQuery' değerine göre uçuşları 'callsign' üzerinden filtreler.
    filteredFlights: (state) => {
      const query = state.searchQuery.toLowerCase();
      return Object.values(state.currentFlights).filter(f =>
        f.callsign?.toString().toLowerCase().includes(query)
      );
    }
  },

  /** ACTIONS: State üzerindeki verileri güncellemek için kullanılan metodlar. Uygulama içindeki iş mantığını (logic) barındırır. */
  actions: {
    toggleDarkMode() {
      this.darkMode = !this.darkMode;
    },
    toggleSidebar() {
      this.sidebarOpen = !this.sidebarOpen; // Sol panelin açık/kapalı durumunu değiştirir
    },
    // API'den verileri çekip store'u güncelle
    async fetchFlights() {
      try {
        const response = await ucakService.getUcaklar();

        response.data.forEach(f => {
          // EĞER uçak ilk kez eklenmişse (sadece başlangıçta) verileri set et
          if (!this.currentFlights[f.icao24]) {
            this.currentFlights[f.icao24] = {
              ...f,
              isApi: true,
              lat: f.latitude,    // Harita motoru 'lat' bekliyor
              lon: f.longitude,   // Harita motoru 'lon' bekliyor
              velocity: f.speed,  // Backend 'speed' -> Frontend 'velocity'
              energy: f.fuel,     // Backend 'fuel' -> Frontend 'energy'
              callsign: f.icao24, // Backend'de callsign yoksa icao24 kullan
              baroaltitude: f.baroaltitude || 0,
              heading: f.heading || 0,
              distance_from_dep: 0,
              total_mission_dist: 0,
              trip_distance: 0
            };
          }
        });
      } catch (error) {
        console.error("Uçuş verileri çekilemedi:", error);
      }
    },

    // Uçuş geçmişini backend'den (veritabanından) çekmek için kullanılır
    async fetchHistory(icao) {
      if (!icao) return;
      try {
        const response = await flightHistoryService.getHistoryByIcao(icao);
        this.selectedFlightHistory = response.data; // Gelen veriyi arayüzde göstermek üzere state'e yazar
      } catch (error) {
        console.error("Uçuş geçmişi çekilemedi:", error);
      }
    },

    // Tamamlanan bir uçuşu kalıcı olarak kaydetmek için kullanılır
    async saveHistory(flightData) {
      try {
        // ADIM 3: Store'a gelen veriyi API Servisi üzerinden backend'e gönderilmek üzere yola çıkar
        await flightHistoryService.saveFlight(flightData);
        
        // Eğer o an o uçağın detay paneli açıksa, listeyi hemen tazeler
        if (this.activeIcao === flightData.icao) {
          this.fetchHistory(flightData.icao);
        }
      } catch (error) {
        console.error("Uçuş geçmişi kaydedilemedi:", error);
      }
    }
  }
});
