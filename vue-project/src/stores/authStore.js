import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/api/index' // Merkezi API konfigürasyon birimi sisteme dahil edilir.

/** authStore.js - Uygulama genelindeki kimlik doğrulama durumunu, kullanıcı verilerini ve 
 * erişim anahtarlarını (JWT) merkezi bir yapıda yönetir. */
export const useAuthStore = defineStore('auth', () => {
  // Uygulama başlatıldığında yerel depolama biriminden (localStorage) mevcut oturum verileri yüklenir.
  const isAuthenticated = ref(localStorage.getItem('isAuthenticated') === 'true')
  const user = ref(JSON.parse(localStorage.getItem('user') || 'null'))
  const token = ref(localStorage.getItem('token') || null)

  /** login: Kullanıcı kimlik doğrulama sürecini başlatır ve yönetir.
   * @param {string} username - Kullanıcı adı
   * @param {string} password - Şifre
   */
  async function login(username, password) {
    try {
      // Kimlik bilgileri, POST metodu ile ilgili API uç noktasına (Endpoint) iletilir.
      const response = await api.post('/Users/login', { username, password });

      if (response.status === 200) {
        const data = response.data;
        isAuthenticated.value = true;
        user.value = { id: data.id, username: data.username, role: data.role };
        token.value = data.token; // Sunucu tarafından dönen JWT anahtarı belleğe alınır.

        // Oturumun sürekliliği için veriler yerel depolama biriminde (Persistence) saklanır.
        localStorage.setItem('isAuthenticated', 'true');
        localStorage.setItem('user', JSON.stringify(user.value));
        localStorage.setItem('token', data.token);
        return true;
      }
      return false;
    } catch (error) {
      // Hatalı kimlik doğrulama veya sunucu bağlantı hataları yakalanır.
      console.error('Kimlik doğrulama hatası:', error.response?.data?.message || error.message);
      return false;
    }
  }

  /** logout: Mevcut oturumu sonlandırır ve tüm güvenlik verilerini sistemden temizler. */
  function logout() {
    isAuthenticated.value = false;
    user.value = null;
    token.value = null;

    // Yerel depolama birimindeki tüm oturum kayıtları temizlenir.
    localStorage.removeItem('isAuthenticated');
    localStorage.removeItem('user');
    localStorage.removeItem('token');
  }

  return {
    isAuthenticated,
    user,
    token,
    login,
    logout
  }
})
