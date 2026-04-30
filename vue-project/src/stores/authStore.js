import { defineStore } from 'pinia'
import { ref } from 'vue'

/** authStore.js - Yetkilendirme Deposu (Pinia)
 * Uygulama genelindeki kullanıcı giriş durumu ve kimlik doğrulama işlemlerini merkezi olarak yönetir. */
export const useAuthStore = defineStore('auth', () => {
  // Uygulama başladığında tarayıcı belleğindeki (localStorage) mevcut oturum bilgilerini yükler
  const isAuthenticated = ref(localStorage.getItem('isAuthenticated') === 'true')
  const user = ref(JSON.parse(localStorage.getItem('user') || 'null'))

  /** login: Kullanıcı girişi işlemini gerçekleştirir.
   * Backend API üzerinden kullanıcı bilgilerini kontrol eder ve oturum başlatır. */
  async function login(username, password) {
    try {
      const response = await fetch('https://localhost:7089/api/Users/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password })
      })

      if (response.ok) {
        const data = await response.json()
        isAuthenticated.value = true
        user.value = data
        
        // Oturumu kalıcı hale getirmek için bilgileri tarayıcı hafızasına kaydeder
        localStorage.setItem('isAuthenticated', 'true')
        localStorage.setItem('user', JSON.stringify(user.value))
        return true
      }
      return false
    } catch (error) {
      // API bağlantı sorunlarını konsola yazdırır
      console.error('Backend bağlantı hatası:', error)
      return false
    }
  }

  /** logout: Mevcut oturumu sonlandırır ve tüm kullanıcı verilerini temizler. */
  function logout() {
    isAuthenticated.value = false
    user.value = null
    // Tarayıcı hafızasındaki oturum bilgilerini siler
    localStorage.removeItem('isAuthenticated')
    localStorage.removeItem('user')
  }

  return {
    isAuthenticated,
    user,
    login,
    logout
  }
})
