/** Pinia kütüphanesi kullanılarak oluşturulan bu depo (store), uçuş verilerinin, kullanıcı
 * tercihlerinin ve global arayüz durumlarının tüm bileşenler arasında senkronize kalmasını sağlar. */
import { defineStore } from 'pinia';
import { ucakService } from '@/api/ucakService';

export const useFlightStore = defineStore('flight', {
  /** STATE: Uygulamanın belleğinde tutulan reaktif veri tabanıdır.
   * Bu veriler değiştiğinde, onlara bağlı olan tüm bileşenler (UI) otomatik güncellenir. */
  state: () => ({
    searchQuery: '', // Sol paneldeki uçuş arama filtresi
    darkMode: false, // Uygulamanın gece/gündüz teması tercihi
    sidebarOpen: true, // Sol panelin (Flight List) açık/kapalı olma durumu
    activeIcao: null, // Haritada ve sağ panelde odaklanılan uçağın ICAO kodu
    currentFlights: {} // Havada olan tüm aktif İHA/uçak verilerinin tutulduğu obje
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
            velocity: isSimulatingLocally ? existingData.velocity : f.speed,
            energy: isSimulatingLocally ? existingData.energy : f.fuel,
            callsign: f.icao24,
            baroaltitude: isSimulatingLocally ? existingData.baroaltitude : (f.baroaltitude || 0),
            heading: isSimulatingLocally ? existingData.heading : (f.heading || 0)
          };
        });
      } catch (error) {
        console.error("Uçuş verileri çekilemedi:", error);
      }
    }
  }
});
