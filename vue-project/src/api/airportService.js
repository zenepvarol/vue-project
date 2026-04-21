import api from './index';

export const airportService = {
  // Tüm havaalanlarını getir
  getAirports() {
    return api.get('/Airports');
  },

  // Tek bir havaalanı getir
  getAirport(id) {
    return api.get(`/Airports/${id}`);
  },

  // Yeni havaalanı ekle
  postAirport(airportData) {
    return api.post('/Airports', airportData);
  },

  // Havaalanı sil
  deleteAirport(id) {
    return api.delete(`/Airports/${id}`);
  }
};
