<script setup lang="ts">
import { computed, ref } from "vue";
import type { ServiceHealthResponse } from "@/composables/useServiceHealth";

interface Props {
  healthData: ServiceHealthResponse | null;
}

const props = defineProps<Props>();

const STORAGE_KEY = "serviceStatusBannerCollapsed";

// Load collapse state from session storage, default to expanded (false) for critical failures
const collapsed = ref<boolean>(false);

try {
  const stored = sessionStorage.getItem(STORAGE_KEY);
  if (stored !== null) {
    collapsed.value = stored === "true";
  }
} catch {
  // Ignore storage errors
}

// Save collapse state to session storage
const toggleCollapsed = (value: boolean) => {
  collapsed.value = value;
  try {
    sessionStorage.setItem(STORAGE_KEY, value.toString());
  } catch {
    // Ignore storage errors
  }
};

const showBanner = computed(() => {
  // Show banner if services are unhealthy OR if there's an update message
  return (
    (props.healthData && props.healthData.overallStatus !== "healthy") ||
    (props.healthData?.updateMessage?.trim().length ?? 0) > 0
  );
});

const showUpdateMessage = computed(() => {
  return (props.healthData?.updateMessage?.trim().length ?? 0) > 0;
});

const showServiceWarnings = computed(() => {
  return props.healthData && props.healthData.overallStatus !== "healthy";
});

const unavailableServices = computed(() => {
  return (
    props.healthData?.services.filter(
      (s) => s.status === "unavailable" || s.status === "degraded",
    ) || []
  );
});

const serviceDisplayNames: Record<string, string> = {
  Database: "Database",
  SearchService: "Search",
  GoogleOAuth: "Google Sign-in",
  FacebookOAuth: "Facebook Sign-in",
  SpotifyOAuth: "Spotify",
  EmailService: "Email",
  ReCaptcha: "Bot Protection",
};

const getServiceDisplayName = (serviceName: string): string => {
  return serviceDisplayNames[serviceName] || serviceName;
};

const affectedFeatures = computed(() => {
  const features: string[] = [];
  unavailableServices.value.forEach((service) => {
    switch (service.name) {
      case "SearchService":
        features.push("search results and details", "song data that is shown may be out of date");
        break;
      case "Database":
        features.push(
          "account information including account creation, logging in, and saved searches",
          "user information including attribution of contributions ",
        );
        break;
      case "GoogleOAuth":
      case "FacebookOAuth":
      case "SpotifyOAuth":
        features.push(`${getServiceDisplayName(service.name)}`);
        break;
      case "EmailService":
        features.push("email notifications");
        break;
    }
  });
  return [...new Set(features)]; // Remove duplicates
});

const mainMessage = computed(() => {
  const count = unavailableServices.value.length;
  if (count === 0) return "";
  if (count === 1) {
    const service = unavailableServices.value[0];
    return service ? `${getServiceDisplayName(service.name)} is temporarily unavailable.` : "";
  }
  return `${count} services are temporarily unavailable.`;
});
</script>

<template>
  <div v-if="showBanner">
    <!-- Update Message Banner (Info style, always shown if present) -->
    <BAlert v-if="showUpdateMessage" variant="info" :model-value="true" class="mb-3">
      <div class="d-flex align-items-start">
        <IBiInfoCircleFill class="me-2 mt-1 flex-shrink-0" />
        <div><strong>Update Notice:</strong> {{ healthData?.updateMessage }}</div>
      </div>
    </BAlert>

    <!-- Service Status Banner (Danger style, collapsible) -->
    <BAccordion v-if="showServiceWarnings" class="service-status-banner mb-3">
      <BAccordionItem :visible="!collapsed" @update:model-value="(val) => toggleCollapsed(!val)">
        <template #title>
          <div class="d-flex align-items-center w-100 text-danger">
            <IBiExclamationTriangleFill class="me-2" />
            <strong>{{ mainMessage }}</strong>
          </div>
        </template>
        <BAlert variant="danger" :model-value="true" class="mb-0">
          <p class="mb-2"><strong>Affected features:</strong></p>
          <ul class="mb-2">
            <li v-for="feature in affectedFeatures" :key="feature">
              {{ feature }}
            </li>
          </ul>
          <p class="mb-0">
            <small>
              We're working to restore full functionality. The site will automatically recover when
              services are restored. Content that depends on unavailable services will not be
              displayed.
            </small>
          </p>
        </BAlert>
      </BAccordionItem>
    </BAccordion>
  </div>
</template>

<style scoped>
.service-status-banner {
  position: relative;
  animation: slideDown 0.3s ease-out;
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.service-status-banner ul {
  margin-bottom: 0.5rem;
  padding-left: 1.5rem;
}

.service-status-banner li {
  margin-bottom: 0.25rem;
}
</style>
