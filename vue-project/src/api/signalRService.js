/**
 * signalRService.js - Real-Time Bildirim Servisi
 * Bu servis, backend'deki SignalR Hub ile sürekli ve güvenli bir bağlantı kurarak,
 * tüm uçak olaylarının (durum değişikliklerinin) anlık olarak dinlenmesini sağlar.
 */
import * as signalR from '@microsoft/signalr';

class SignalRService {
  constructor() {
    this.connection = null;
    this.callbacks = new Set();
  }

  /**
   * SignalR Hub bağlantısını başlatır ve otomatik yeniden bağlanma mantığını kurar.
   */
  startConnection() {
    // Eğer bağlantı zaten varsa veya kuruluyorsa tekrar başlatma
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('https://localhost:7089/hubs/notification', {
        accessTokenFactory: () => localStorage.getItem('token') || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Yeniden bağlanma süreleri
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    // Dinleyiciyi kaydet
    this.connection.on('ReceiveNotification', (type, title, text, icao) => {
      this.callbacks.forEach(cb => {
        try {
          cb({ type, title, text, icao });
        } catch (err) {
          console.error('SignalR callback çalıştırılırken hata oluştu:', err);
        }
      });
    });

    // Bağlantıyı başlat
    this.connection.start()
      .then(() => {
        console.log('>> SignalR Bildirim Hub bağlantısı başarıyla kuruldu.');
      })
      .catch(err => {
        console.error('SignalR Hub bağlantı hatası:', err);
        // Hata durumunda 5 saniye sonra tekrar dene
        setTimeout(() => this.startConnection(), 5000);
      });

    // Bağlantı koptuğunda ve tekrar kurulduğunda loglama yap
    this.connection.onclose((error) => {
      console.warn('>> SignalR bağlantısı kapandı:', error);
    });

    this.connection.onreconnecting((error) => {
      console.warn('>> SignalR yeniden bağlanmaya çalışıyor:', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('>> SignalR yeniden bağlandı. ConnectionId:', connectionId);
    });
  }

  /**
   * Yeni bir bildirim dinleyici callback fonksiyonu kaydeder.
   * @param {Function} callback - ({ type, title, text, icao }) parametrelerini alan fonksiyon.
   */
  onNotificationReceived(callback) {
    if (typeof callback === 'function') {
      this.callbacks.add(callback);
    }
    // Callback'i kaldırmak için bir fonksiyon döner
    return () => {
      this.callbacks.delete(callback);
    };
  }
}

export const signalRService = new SignalRService();
