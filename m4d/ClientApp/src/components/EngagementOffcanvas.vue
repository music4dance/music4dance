<template>
  <BOffcanvas
    id="engagement-offcanvas"
    v-model="isOpen"
    placement="bottom"
    :backdrop="true"
    :scroll="false"
    no-header
    @hidden="onHidden"
    :data-engagement-element="'offcanvas'"
    :data-engagement-level="engagementData?.level || (isAuthenticated ? 'loggedin' : '1')"
    :data-engagement-user-type="isAuthenticated ? 'authenticated' : 'anonymous'"
    :data-engagement-action="'impression'"
  >
    <!-- Custom header with collapse button (entire header is clickable) -->
    <div
      class="engagement-offcanvas-header d-flex align-items-center border-bottom pb-2 mb-3"
      role="button"
      tabindex="0"
      @click="onCollapse"
      @keydown.enter="onCollapse"
      @keydown.space.prevent="onCollapse"
      aria-label="Collapse"
      style="cursor: pointer"
      data-engagement-action="collapse-click"
    >
      <span class="me-2 text-muted">
        <IBiChevronDown />
      </span>
      <h5 class="mb-0">{{ headerTitle }}</h5>
    </div>

    <!-- Dynamic message (progressive insistence) -->
    <div
      v-if="engagementData?.message"
      class="engagement-message mb-3"
      v-html="engagementData.message"
    ></div>

    <!-- Conditional content based on user type -->
    <div v-if="!isAuthenticated" class="free-account-benefits mb-3">
      <h6>When you've signed up you can:</h6>
      <ul class="list-clean-aligned">
        <li>
          <IBiHandThumbsUp class="text-primary me-2" /><a
            href="https://music4dance.blog/music4dance-help/dance-tags/"
            target="_blank"
            >Vote on dances</a
          >
        </li>
        <li>
          <IBiTagsFill class="text-primary me-2" /><a
            href="https://music4dance.blog/music4dance-help/tag-editing/"
            target="_blank"
            >Tag songs</a
          >
        </li>
        <li>
          <IBiSearch class="text-primary me-2" /><a
            href="https://music4dance.blog/music4dance-help/advanced-search/"
            target="_blank"
            >Search on songs you've tagged</a
          >
        </li>
        <li>
          <IBiFolderFill class="text-primary me-2" /><a
            href="https://music4dance.blog/music4dance-help/saved-searches/"
            target="_blank"
            >Save your searches</a
          >
        </li>
        <li>
          <IBiHeartFill class="text-primary me-2" /><a
            href="https://music4dance.blog/2021/10/15/what-is-the-difference-between-adding-a-song-to-favorites-and-voting-on-a-songs-danceability/"
            target="_blank"
            >Add songs to favorites</a
          >
        </li>
        <li>
          <IBiCurrencyDollar class="text-primary me-2" /><a
            href="https://music4dance.blog/music4dance-help/subscriptions/"
            target="_blank"
            >Purchase a premium subscription to unlock even more features</a
          >
        </li>
      </ul>
    </div>

    <div v-else class="premium-benefits mb-3">
      <h6>Upgrade to Premium Membership</h6>
      <p class="text-muted small">Your membership includes:</p>
      <ul class="list-clean-aligned">
        <li v-for="(benefit, index) in premiumBenefits" :key="index">
          <IBiCheckCircleFill class="text-success me-2" /><span v-html="benefit"></span>
        </li>
        <li v-if="premiumBenefitsMoreText">
          <IBiCheckCircleFill class="text-success me-2" />{{ premiumBenefitsMoreText }}
        </li>
      </ul>
      <p v-if="premiumFeaturesUrl" class="small">
        <a :href="premiumFeaturesUrl" target="_blank">View complete feature list</a>
      </p>
    </div>

    <!-- CTAs (different for anonymous vs logged-in) -->
    <BRow class="g-2">
      <template v-if="!isAuthenticated">
        <!-- Anonymous: Always same 3 buttons -->
        <BCol cols="12" sm="4">
          <BButton
            :href="registerUrl"
            variant="primary"
            size="sm"
            class="w-100"
            data-engagement-action="signup-click"
          >
            Sign Up Free
          </BButton>
        </BCol>
        <BCol cols="12" sm="4">
          <BButton
            :href="loginUrl"
            variant="outline-primary"
            size="sm"
            class="w-100"
            data-engagement-action="signin-click"
          >
            Sign In
          </BButton>
        </BCol>
        <BCol cols="12" sm="4">
          <BButton
            variant="outline-secondary"
            size="sm"
            class="w-100"
            @click="onCollapse"
            data-engagement-action="dismiss-click"
          >
            Maybe Later
          </BButton>
        </BCol>
      </template>

      <template v-else>
        <!-- Logged-in: Premium upgrade -->
        <BCol cols="12" sm="4">
          <BButton
            :href="subscribeUrl"
            variant="success"
            size="sm"
            class="w-100"
            data-engagement-action="subscribe-click"
          >
            Subscribe Now
          </BButton>
        </BCol>
        <BCol cols="12" sm="4">
          <BButton
            :href="premiumFeaturesUrl"
            variant="outline-primary"
            size="sm"
            class="w-100"
            target="_blank"
            data-engagement-action="learnmore-click"
          >
            Learn More
          </BButton>
        </BCol>
        <BCol cols="12" sm="4">
          <BButton
            variant="outline-secondary"
            size="sm"
            class="w-100"
            @click="onCollapse"
            data-engagement-action="dismiss-click"
          >
            Maybe Later
          </BButton>
        </BCol>
      </template>
    </BRow>
  </BOffcanvas>
</template>

<script setup lang="ts">
import { ref, watch, computed } from "vue";
import type { EngagementLevel } from "@/composables/useEngagementOffcanvas";
import type { EngagementConfig } from "@/models/EngagementConfig";

interface Props {
  /** Whether offcanvas should be visible */
  modelValue: boolean;
  /** Engagement level data (message, level) */
  engagementData: EngagementLevel | null;
  /** Whether user is authenticated */
  isAuthenticated: boolean;
  /** Engagement configuration from server */
  config: EngagementConfig;
}

interface Emits {
  /** Emitted when modal visibility changes */
  (event: "update:modelValue", value: boolean): void;
  /** Emitted when user collapses the offcanvas */
  (event: "collapse"): void;
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

// Dynamic header title based on user type and level
const headerTitle = computed(() => {
  if (!props.isAuthenticated) {
    // Anonymous users - progressive messaging
    if (!props.engagementData) return "Exploring music4dance?";

    switch (props.engagementData.level) {
      case 1:
        return "Exploring music4dance?";
      case 2:
        return "Still searching for music?";
      case 3:
        return "Finding everything you need?";
      default:
        return "Exploring music4dance?";
    }
  } else {
    // Logged-in users - premium upgrade
    return "Upgrade to Premium";
  }
});

// Extract CTA URLs from config
const registerUrl = computed(() => props.config.ctaUrls.register);
const loginUrl = computed(() => props.config.ctaUrls.login || "/identity/account/login");
const subscribeUrl = computed(() => props.config.ctaUrls.subscribe);
const premiumFeaturesUrl = computed(() => props.config.ctaUrls.features);

// Extract premium benefits from config
const premiumBenefits = computed(() => props.config.premiumBenefits?.items || []);
const premiumBenefitsMoreText = computed(() => props.config.premiumBenefits?.moreText);

/**
 * Handle offcanvas collapse (down arrow or "Maybe Later" CTA)
 */
function onCollapse(): void {
  isOpen.value = false;
  emit("collapse");
}

/**
 * Handle offcanvas hidden event (after animation completes)
 */
function onHidden(): void {
  // Always emit collapse when offcanvas closes (any method: backdrop, down arrow, or Maybe Later button)
  emit("collapse");
}
</script>

<style scoped lang="scss">
/* Set offcanvas container height to 75vh max */
:global(#engagement-offcanvas) {
  height: auto !important;
  max-height: 75vh !important;
}

.engagement-offcanvas-header {
  button {
    &:hover {
      color: var(--bs-primary) !important;
    }
  }
}

.list-clean-aligned {
  list-style: none;
  padding-left: 0;

  li {
    display: flex;
    align-items: center;
    margin-bottom: 0.5rem;
  }
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
</style>
