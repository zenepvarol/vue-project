<template>
  <div class="login-container">
    <v-container fluid class="fill-height">
      <v-row align="center" justify="center" class="login-row">
        <v-col cols="12" sm="8" md="4" lg="3" xl="2">
          <v-card class="login-card elevation-12" rounded="xl">
            <v-card-text class="pa-8">
              <div class="text-center mb-8">
                <h1 class="text-h5 font-weight-bold primary--text mb-1">SİSTEM GİRİŞİ</h1>
                <div class="text-caption text-medium-emphasis">Yönetim Paneli</div>
              </div>

              <v-form @submit.prevent="handleLogin" ref="form">
                <v-text-field
                  v-model="username"
                  label="Kullanıcı Adı"
                  prepend-inner-icon="mdi-account-outline"
                  variant="outlined"
                  rounded="lg"
                  required
                  :rules="[v => !!v || 'Kullanıcı adı gerekli']"
                  class="mb-4"
                ></v-text-field>

                <v-text-field
                  v-model="password"
                  label="Şifre"
                  prepend-inner-icon="mdi-lock-outline"
                  :append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'"
                  @click:append-inner="showPassword = !showPassword"
                  :type="showPassword ? 'text' : 'password'"
                  variant="outlined"
                  rounded="lg"
                  required
                  :rules="[v => !!v || 'Şifre gerekli']"
                  class="mb-6"
                ></v-text-field>

                <v-btn
                  type="submit"
                  color="primary"
                  block
                  size="x-large"
                  rounded="lg"
                  :loading="loading"
                  elevation="4"
                  class="login-btn py-4"
                >
                  GİRİŞ YAP
                </v-btn>
              </v-form>

              <div v-if="error" class="mt-6">
                <v-alert type="error" variant="tonal" rounded="lg" density="compact" closable @click:close="error = ''">
                  {{ error }}
                </v-alert>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '@/stores/authStore';

const router = useRouter();
const authStore = useAuthStore();

const username = ref('');
const password = ref('');
const showPassword = ref(false);
const loading = ref(false);
const error = ref('');
const form = ref(null);

const handleLogin = async () => {
  const { valid } = await form.value.validate();
  if (!valid) return;

  loading.value = true;
  error.value = '';

  try {
    const success = await authStore.login(username.value, password.value);
    if (success) {
      router.push('/');
    } else {
      error.value = 'Hatalı kullanıcı adı veya şifre!';
    }
  } catch (err) {
    error.value = 'Sunucuya bağlanılamadı!';
  } finally {
    loading.value = false;
  }
};
</script>

<style scoped>
.login-container {
  min-height: 100vh;
  width: 100%;
  background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  padding: 0;
  position: absolute;
  top: 0;
  left: 0;
}
.login-row {
  transform: translateY(-10%);
  width: 100%;
}

.login-card {
  background: white !important;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.login-btn {
  font-weight: 600;
  letter-spacing: 1px;
  text-transform: none;
  font-size: 1.1rem;
}

:deep(.v-theme--dark) .login-card {
  background: #1e293b !important;
}

.opacity-70 {
  opacity: 0.7;
}
</style>
