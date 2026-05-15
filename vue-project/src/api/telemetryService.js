/**
 * telemetryService.js - Merkezi Telemetri API İstemcisi
 * Bu servis, frontend ile backend arasındaki gerçek zamanlı uçuş verisi senkronizasyonunu yönetir.
 */
import api from './index';

export const telemetryService = {
  /**
   * Hava aracının anlık telemetri verilerini (konum, hız, enerji vb.) backend sunucusuna iletir.
   * @param {Object} telemetryData - ICAO, Lat, Lon ve uçuş durumlarını içeren telemetri paketi.
   */
  updateTelemetry(telemetryData) {
    return api.post('/Telemetry/update', telemetryData);
  },
  
  /**
   * Görev tamamlandığında veya uçak indiğinde telemetri takibini sonlandırır.
   * @param {string} icao - Takibi sonlandırılacak uçağın benzersiz ICAO kodu.
   */
  endMission(icao) {
    return api.post(`/Telemetry/end/${icao}`);
  },
  
  /**
   * Sistem genelindeki tüm aktif uçuşların listesini çeker. 
   * Bu veriler harita üzerinde 'Remote' uçakların gösterilmesi için kullanılır.
   */
  getActiveFlights() {
    return api.get('/Telemetry/active-flights');
  }
};
