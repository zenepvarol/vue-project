import api from './index';

export const telemetryService = {
  // Uçağın canlı koordinatlarını Backend'deki RAM'e gönderir
  updateTelemetry(telemetryData) {
    return api.post('/Telemetry/update', telemetryData);
  },
  
  // Uçuş bittiğinde veya uçak indiğinde telemetriden kaydı siler
  endMission(icao) {
    return api.post(`/Telemetry/end/${icao}`);
  },
  
  // Viewer (İzleyici) rolü için sistemdeki tüm aktif uçuşların listesini döndürür
  getActiveFlights() {
    return api.get('/Telemetry/active-flights');
  }
};
