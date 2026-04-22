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
          const icao = String(f.icao24).toUpperCase();
          const isSiha = f.isSiha || f.IsSiha || String(f.status).includes('SIHA');
          
          // Uçak var mı yok mu bakmadan verilerini güncelle
          const existingData = this.currentFlights[icao] || {};

          // ÖNEMLİ: Eğer uçak şu an yerel Vue simülasyonu tarafından hareket ettiriliyorsa, 
          // API'den gelen (eski) koordinatların mevcut konumu ezmesini engelle.
          const isSimulatingLocally = ['GOING_TO_DEST', 'GOING_TO_DEP', 'RETURNING', 'MISSION_COMPLETE', 'MANUAL', 'ON_MISSION', 'ACTIVE', 'ARRIVED'].includes(existingData.status);

          this.currentFlights[icao] = {
            ...existingData,
            ...f,
            icao24: icao,
            isApi: true,
            isSiha: isSiha,
            // Sadece simülasyon yoksa koordinatları API'den al
            lat: isSimulatingLocally ? existingData.lat : parseFloat(f.latitude),
            lon: isSimulatingLocally ? existingData.lon : parseFloat(f.longitude),
            homeLat: existingData.homeLat || parseFloat(f.latitude),
            homeLon: existingData.homeLon || parseFloat(f.longitude),
            ammo: isSiha ? (existingData.ammo ?? 2) : 0,
            status: isSimulatingLocally ? existingData.status : (isSiha ? (existingData.status || 'STANDBY') : (f.status || 'ON_MISSION')),
            velocity: isSimulatingLocally ? existingData.velocity : (parseFloat(f.speed) || 0),
            energy: isSimulatingLocally ? existingData.energy : (parseFloat(f.fuel) || 0),
            distance_from_dep: existingData.distance_from_dep || 0,
            total_mission_dist: existingData.total_mission_dist || 0,
            trip_distance: existingData.trip_distance || 0,
            callsign: f.callsign || f.icao24,
            modelType: f.modelType || 'İHA',
            baroaltitude: isSimulatingLocally ? existingData.baroaltitude : (f.baroaltitude || 0),
            heading: isSimulatingLocally ? existingData.heading : (f.heading || 0)
          };
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
