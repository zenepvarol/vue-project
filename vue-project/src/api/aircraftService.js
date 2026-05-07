import api from './index';

export const aircraftService = {
  // Bütün uçakları getir
  getAircrafts() {
    return api.get('/Aircraft');
  },

  // Yeni uçak verisi gönder (veya mevcutu güncelle)
  postAircraft(aircraftData) {
    return api.post('/Aircraft', aircraftData);
  }
};
