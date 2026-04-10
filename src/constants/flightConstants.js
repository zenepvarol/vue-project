/**  Uçuş simülasyonu boyunca kullanılan tüm sabit değerler bu dosyada toplanmıştır.
 * Bu sayede merkezi bir yönetim sağlanır ve "magic strings/numbers" kullanımı önlenir.*/

export const FLIGHT_STATUS = {
  STANDBY: 'STANDBY',
  ACTIVE: 'ACTIVE',
  ON_MISSION: 'ON_MISSION',
  RETURNING: 'RETURNING',
  COMPLETED: 'COMPLETED',
  ARRIVED: 'ARRIVED',
  GOING_TO_DEP: 'GOING_TO_DEP',
  GOING_TO_DEST: 'GOING_TO_DEST',
  MISSION_COMPLETE: 'MISSION_COMPLETE',
  EMERGENCY_LANDED: 'EMERGENCY_LANDED',
  APPROACHING: 'APPROACHING',
  REFUELING: 'REFUELING'
};

export const SIM_SETTINGS = {
  // Fizik ve Hareket
  DEFAULT_CRUISE_SPEED: 220,
  DEFAULT_CRUISE_ALT: 10000,
  MANUAL_CRUISE_SPEED: 160,
  DESCENT_DISTANCE_KM: 20,

  // Yakıt ve Tüketim
  FUEL_CONSUMPTION_RATE: 0.0025,
  LOW_FUEL_THRESHOLD: 20,

  // Görev Parametreleri
  DEFAULT_AMMO: 2,
  EXPLOSION_THRESHOLD_KM: 1.0,

  // Zamanlama
  UPDATE_INTERVAL_MS: 10
};
