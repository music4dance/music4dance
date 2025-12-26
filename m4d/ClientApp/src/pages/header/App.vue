<script setup lang="ts">
import { MenuContext } from "@/models/MenuContext";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { useServiceHealth } from "@/composables/useServiceHealth";
import { onMounted } from "vue";

const context: MenuContext = getMenuContext();
const { healthData, startPolling } = useServiceHealth();

onMounted(() => {
  startPolling();
});
</script>

<template>
  <header>
    <MainMenu :context="context" />
    <ServiceStatusBanner :health-data="healthData" />
  </header>
  <body />
</template>
