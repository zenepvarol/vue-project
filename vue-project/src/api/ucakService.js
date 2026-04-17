import api from './index';

export const ucakService = {
  // Bütün uçakları getir
  getUcaklar() {
    return api.get('/Ucak');
  },

  // Yeni uçak verisi gönder (veya mevcutu güncelle)
  postUcak(ucakData) {
    return api.post('/Ucak', ucakData);
  }
};
