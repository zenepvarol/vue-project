/**
 * getDistance - Haversine Formülü ile Mesafe Hesaplama
 * İki coğrafi nokta arasındaki kuş uçuşu mesafeyi hesaplar.
 * @param {Object} p1 - Başlangıç noktası {lat, lon}
 * @param {Object} p2 - Hedef noktası {lat, lon}
 * @returns {Number} Kilometre cinsinden mesafe
 */

export const getDistance = (p1, p2) => {
  const R = 6371; // Dünya'nın ortalama yarıçapı (km)

  // Enlem ve boylam farkları radyan cinsine dönüştürülür
  const dLat = (p2.lat - p1.lat) * Math.PI / 180;
  const dLon = (p2.lon - p1.lon) * Math.PI / 180;

  // Haversine Formülü: Küre üzerindeki iki nokta arasındaki en kısa mesafeyi trigonometrik olarak hesaplar.
  const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(p1.lat * Math.PI / 180) * Math.cos(p2.lat * Math.PI / 180) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c; // Kilometre sonucu döner
};

/**
 * calculateNextPosition - Vektörel İlerleme ve Rota Hesaplama
 * İHA'nın hızı ve zaman adımına (moveStep) göre bir sonraki koordinatını ve yönelim açısını (heading) hesaplar.
 */

export const calculateNextPosition = (currentLat, currentLon, targetLat, targetLon, moveStep, currentHeading = 0) => {
  let nextLat = targetLat;
  let nextLon = targetLon;
  let heading = currentHeading;

  // Eğer bir hareket adımı tanımlanmışsa (Simülasyon aktifse)
  if (moveStep > 0) {
    const dx = targetLat - currentLat; // Enlem üzerindeki vektörel fark
    const dy = targetLon - currentLon; // Boylam üzerindeki vektörel fark

    // Mevcut konum ile hedef arasındaki güncel mesafe
    const dist = getDistance({ lat: currentLat, lon: currentLon }, { lat: targetLat, lon: targetLon });

    // Hedefe olan mesafe hareket adımından büyükse: İHA henüz hedefe varmamıştır, vektörel olarak ilerlemesi hesaplanır.
    if (dist > moveStep) {
      nextLat = currentLat + (dx / dist) * moveStep;
      nextLon = currentLon + (dy / dist) * moveStep;

      // İHA'nın bakacağı açı (Heading) radyan/derece dönüşümü ile hesaplanır.
      heading = (Math.atan2(dy, dx) * (180 / Math.PI));
      return { nextLat, nextLon, heading, hasArrived: false };
    } else {
      //Mesafe adım değerinden küçükse: İHA hedefe varmış kabul edilir ve koordinatlar hedefe kilitlenir.
      return { nextLat: targetLat, nextLon: targetLon, heading: currentHeading, hasArrived: true };
    }
  }
  // Hareket yoksa mevcut durumu koru
  return { nextLat, nextLon, heading, hasArrived: false };
};

/**
 * interpolateSlerp - Spherical Linear Interpolation (Slerp)
 * İki coğrafi nokta arasında küresel kavisli bir rota üzerinde ara nokta hesaplar.
 * @param {Object} p1 - Başlangıç noktası {lat, lon}
 * @param {Object} p2 - Bitiş noktası {lat, lon}
 * @param {Number} t - İlerleme oranı (0 ile 1 arası)
 * @returns {Object} {lat, lon}
 */
export const interpolateSlerp = (p1, p2, t) => {
  const degToRad = Math.PI / 180;
  const radToDeg = 180 / Math.PI;

  // Koordinatları radyan cinsine çevir (Trigonometrik hesaplamalar için)
  const lat1 = p1.lat * degToRad;
  const lon1 = p1.lon * degToRad;
  const lat2 = p2.lat * degToRad;
  const lon2 = p2.lon * degToRad;

  // Küresel açısal mesafe hesaplama (İki nokta arasındaki yay açısı)
  const dLon = lon2 - lon1;
  const cosOmega = Math.sin(lat1) * Math.sin(lat2) + Math.cos(lat1) * Math.cos(lat2) * Math.cos(dLon);
  const omega = Math.acos(Math.min(1, Math.max(-1, cosOmega)));

  // Eğer noktalar aynıysa başlangıç noktasını dön
  if (omega === 0) return { lat: p1.lat, lon: p1.lon };

  // Slerp katsayılarını hesapla (İlerleme oranına göre ağırlıklandırma)
  const sinOmega = Math.sin(omega);
  const A = Math.sin((1 - t) * omega) / sinOmega;
  const B = Math.sin(t * omega) / sinOmega;

  // 3D Kartezyen uzayda yeni konumu bul (X, Y, Z vektörleri)
  const x = A * Math.cos(lat1) * Math.cos(lon1) + B * Math.cos(lat2) * Math.cos(lon2);
  const y = A * Math.cos(lat1) * Math.sin(lon1) + B * Math.cos(lat2) * Math.sin(lon2);
  const z = A * Math.sin(lat1) + B * Math.sin(lat2);

  // Vektörleri tekrar Enlem ve Boylam (Geografik) birimine çevir
  const latN = Math.atan2(z, Math.sqrt(x * x + y * y));
  const lonN = Math.atan2(y, x);

  return {
    lat: latN * radToDeg,
    lon: lonN * radToDeg
  };
};
