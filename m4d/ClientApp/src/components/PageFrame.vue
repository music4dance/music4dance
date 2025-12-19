<script setup lang="ts">
import { MenuContext } from "@/models/MenuContext";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { type BreadCrumbItem } from "@/models/BreadCrumbItem";
import { computed, onMounted } from "vue";
import { useServiceHealth } from "@/composables/useServiceHealth";
import ServiceStatusBanner from "@/components/ServiceStatusBanner.vue";

const menuContext: MenuContext = getMenuContext();
const { healthData, startPolling, stopPolling } = useServiceHealth();

defineProps<{
  id: string;
  title?: string;
  help?: string;
  breadcrumbs?: BreadCrumbItem[];
}>();

const emit = defineEmits(["loaded"]);

// INT-TODO: Rethink dance/tag environmnet loading
const loaded = computed(() => {
  return true;
});

const year = computed(() => {
  return new Date().getFullYear().toString();
});

onMounted(() => {
  startPolling();
  emit("loaded");
});
</script>

<template>
  <div>
    <MainMenu :context="menuContext" />
    <ServiceStatusBanner :health-data="healthData" />
    <nav v-if="breadcrumbs" aria-label="breadcrumb">
      <BBreadcrumb :items="breadcrumbs" style="padding: 0.25rem 0.5rem" />
    </nav>
    <div id="body-content" class="container-fluid body-content">
      <h1 v-if="title">{{ title }}</h1>
      <div v-else class="mt-2" />
      <PageLoader :loaded="loaded">
        <slot />
      </PageLoader>
    </div>
    <div id="footer-content">
      <hr />
      <footer>
        <p>
          &copy; {{ year }} - <a href="https://www.music4dance.net">Music4Dance.net</a> -
          <a href="https://www.music4dance.net/home/sitemap">Site Map</a> -
          <a href="https://www.music4dance.net/home/termsofservice">Terms of Service</a>
          -
          <a href="https://www.music4dance.net/home/privacypolicy">Privacy Policy</a>
          - <a href="https://www.music4dance.net/home/credits">Credits</a> -
          <a href="https://github.com/music4dance/music4dance" target="_blank">Code</a> -
          <a :href="menuContext.helpLink">Help</a>
        </p>
      </footer>
    </div>
  </div>
</template>
