/** api/index.js - API Konfigürasyonu ve İstek Denetleyici
 * Giden tüm HTTP isteklerini merkezi olarak yöneten ve kimlik doğrulama 
 * üstbilgilerini (Authorization Header) otomatik olarak enjekte eden birimdir. */
import axios from 'axios';

const api = axios.create({
  baseURL: 'https://localhost:7089/api',
  headers: {
    'Content-Type': 'application/json'
  }
});

/** Request Interceptor: HTTP istekleri sunucuya iletilmeden önce çalıştırılır.
 * İstemci tarafında saklanan JWT (JSON Web Token) bilgisini her isteğe dahil eder. */
api.interceptors.request.use(
  (config) => {
    // Yerel depolama biriminden (localStorage) mevcut oturum anahtarını (token) sorgular.
    const token = localStorage.getItem('token');

    // Geçerli bir token bulunması durumunda, HTTP Authorization üstbilgisine Bearer şemasıyla tanımlanır.
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    // İstek yapılandırması sırasında oluşabilecek hataları yakalar ve geri döndürür.
    return Promise.reject(error);
  }
);

/** Response Interceptor: Sunucudan gelen yanıtları denetler.
 * Özellikle oturum süresi dolduğunda (401 Hatası) kullanıcıyı Login sayfasına yönlendirir. */
api.interceptors.response.use(
  (response) => {
    return response; // Başarılı yanıtları olduğu gibi geri döndürür.
  },
  (error) => {
    if (error.response && error.response.status === 401) {
      // Yerel hafızadaki geçersiz oturum verilerini temizler.
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      localStorage.removeItem('isAuthenticated');

      window.location.href = '/login'; // Kullanıcıyı Login sayfasına yönlendirir.
    }
    return Promise.reject(error);
  }
);

export default api;
