<template>
  <div class="sidebar left" :class="{ collapsed: !store.sidebarOpen }"
    style="display: flex; flex-direction: column; max-height: 100vh;">
    <SearchHeader />
    <FlightList @focus-flight="$emit('focus-flight', $event)" />
  </div>
</template>

<script setup>
import { useFlightStore } from '@/stores/flightStore';
import SearchHeader from './LeftPanel/SearchHeader.vue';
import FlightList from './LeftPanel/FlightList.vue';

const store = useFlightStore();
defineEmits(['focus-flight']);
</script>
