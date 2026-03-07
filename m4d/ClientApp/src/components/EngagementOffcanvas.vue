<template>
  <BOffcanvas
    v-model="isOpen"
    placement="bottom"
    :backdrop="true"
    :scroll="false"
    title="Exploring music4dance?"
    body-class="engagement-offcanvas-body"
    @hidden="onHidden"
  >
    <!-- Dynamic message content (HTML from server) -->
    <div class="engagement-message" v-html="engagementData?.message"></div>

    <!-- Call-to-action buttons (level-specific order) -->
    <div class="engagement-cta-buttons mt-3 d-flex flex-wrap gap-2">
      <template v-if="engagementData?.level === 1">
        <!-- Level 1: Register, Features, Dismiss -->
        <BButton
          :href="engagementData.ctaUrls.primary"
          variant="primary"
          size="sm"
          class="flex-fill"
        >
          Create Account
        </BButton>
        <BButton
          :href="engagementData.ctaUrls.secondary"
          variant="outline-secondary"
          size="sm"
          class="flex-fill"
        >
          Learn More
        </BButton>
        <BButton variant="outline-secondary" size="sm" class="flex-fill" @click="onDismiss">
          Maybe Later
        </BButton>
      </template>

      <template v-else-if="engagementData?.level === 2">
        <!-- Level 2: Subscribe, Register, Features, Dismiss -->
        <BButton
          :href="engagementData.ctaUrls.tertiary"
          variant="success"
          size="sm"
          class="flex-fill"
        >
          Subscribe
        </BButton>
        <BButton
          :href="engagementData.ctaUrls.primary"
          variant="primary"
          size="sm"
          class="flex-fill"
        >
          Create Account
        </BButton>
        <BButton
          :href="engagementData.ctaUrls.secondary"
          variant="outline-secondary"
          size="sm"
          class="flex-fill"
        >
          Learn More
        </BButton>
        <BButton variant="outline-secondary" size="sm" class="flex-fill" @click="onDismiss">
          Dismiss
        </BButton>
      </template>

      <template v-else-if="engagementData?.level === 3">
        <!-- Level 3: Subscribe (emphasized), Features, Register, Dismiss -->
        <BButton
          :href="engagementData.ctaUrls.tertiary"
          variant="success"
          size="md"
          class="flex-fill fw-bold"
        >
          Subscribe Now
        </BButton>
        <BButton
          :href="engagementData.ctaUrls.secondary"
          variant="outline-primary"
          size="sm"
          class="flex-fill"
        >
          View Features
        </BButton>
        <BButton
          :href="engagementData.ctaUrls.primary"
          variant="outline-secondary"
          size="sm"
          class="flex-fill"
        >
          Free Account
        </BButton>
        <BButton variant="outline-secondary" size="sm" class="flex-fill" @click="onDismiss">
          Dismiss
        </BButton>
      </template>
    </div>
  </BOffcanvas>
</template>

<script setup lang="ts">
import { ref, watch, computed } from "vue";
import type { EngagementLevel } from "@/composables/useEngagementOffcanvas";

interface Props {
  /** Whether offcanvas should be visible */
  modelValue: boolean;
  /** Engagement level data (message, level, CTAs) */
  engagementData: EngagementLevel | null;
}

interface Emits {
  /** Emitted when modal visibility changes */
  (event: "update:modelValue", value: boolean): void;
  /** Emitted when user dismisses the offcanvas */
  (event: "dismiss"): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

// Internal state synced with modelValue (two-way binding)
const isOpen = ref(props.modelValue);

// Watch for external changes to modelValue
watch(
  () => props.modelValue,
  (newValue) => {
    isOpen.value = newValue;
  },
);

// Watch for internal changes to isOpen and emit
watch(isOpen, (newValue) => {
  emit("update:modelValue", newValue);
});

// Dynamic title based on engagement level
const offcanvasTitle = computed(() => {
  if (!props.engagementData) return "";

  switch (props.engagementData.level) {
    case 1:
      return "Exploring music4dance?";
    case 2:
      return "Finding what you need?";
    case 3:
      return "You've discovered a lot!";
    default:
      return "";
  }
});

/**
 * Handle offcanvas dismissal (both close button and "Dismiss" CTA)
 */
function onDismiss(): void {
  isOpen.value = false;
  emit("dismiss");
}

/**
 * Handle offcanvas hidden event (after animation completes)
 */
function onHidden(): void {
  // Always emit dismiss when offcanvas closes (any method: backdrop, X, or dismiss button)
  emit("dismiss");
}
</script>

<style scoped lang="scss">
.engagement-offcanvas-body {
  padding-bottom: 1rem;
}

.engagement-message {
  :deep(h4) {
    font-size: 1.25rem;
    margin-bottom: 0.75rem;
    color: var(--bs-heading-color);
  }

  :deep(p) {
    margin-bottom: 0.5rem;
    color: var(--bs-body-color);
  }

  :deep(strong) {
    font-weight: 600;
  }
}

.engagement-cta-buttons {
  .flex-fill {
    min-width: fit-content;
  }
}

/* Responsive: Stack buttons on very small screens */
@media (max-width: 576px) {
  .engagement-cta-buttons {
    flex-direction: column;

    .flex-fill {
      width: 100%;
    }
  }
}
</style>
