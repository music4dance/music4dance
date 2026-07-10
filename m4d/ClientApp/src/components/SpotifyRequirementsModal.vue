<script setup lang="ts">
import { computed } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";

interface Props {
  featureName: string;
  isAuthenticated?: boolean;
  isPremium?: boolean;
  hasSpotifyOAuth?: boolean;
  // True when the user previously connected Spotify but their connection has since
  // expired/been revoked (e.g. Spotify's refresh-token expiration), rather than never
  // having connected at all - changes the copy from "connect" to "reconnect".
  reauthRequired?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  isAuthenticated: false,
  isPremium: false,
  hasSpotifyOAuth: false,
  reauthRequired: false,
});

const modelValue = defineModel<boolean>({ default: false });
const menuContext = getMenuContext();

// Compute return URL for links
const returnUrl = computed(() => encodeURIComponent(window.location.pathname));

// "Manage external logins" only lets you add a login that isn't linked yet - for a rejected
// refresh token the Spotify login is already linked, so re-running the OAuth challenge from
// the sign-in page (which re-authorizes an already-linked provider and refreshes the tokens)
// is the only thing that actually fixes it.
const spotifyConnectHref = computed(() =>
  props.reauthRequired
    ? `/Identity/Account/Login?provider=Spotify&returnUrl=${returnUrl.value}&reason=expired`
    : `/identity/account/manage/externallogins?returnUrl=${returnUrl.value}`,
);

const canUseFeature = computed(
  () => props.isAuthenticated && props.isPremium && props.hasSpotifyOAuth,
);

const nextStep = computed(() => {
  if (!props.isAuthenticated) return "sign-in";
  if (!props.isPremium) return "premium";
  if (!props.hasSpotifyOAuth) return "spotify";
  return "ready";
});
</script>

<template>
  <BModal v-model="modelValue" :title="`${featureName} Requirements`" ok-only ok-title="Close">
    <div v-if="!canUseFeature">
      <div v-if="reauthRequired" class="alert alert-warning">
        Your Spotify connection has expired. Please reconnect your Spotify account to continue.
      </div>

      <p>
        To use <strong>{{ featureName }}</strong
        >, you must meet the following requirements:
      </p>

      <ul class="list-unstyled">
        <li class="mb-3 d-flex align-items-start">
          <div class="requirement-icon">
            <IBiCheckCircleFill
              v-if="isAuthenticated"
              class="icon-check text-success"
              aria-label="Complete"
            />
            <IBiCircle v-else class="icon-check text-muted" aria-label="Incomplete" />
          </div>
          <div class="requirement-content">
            <strong>Be signed in</strong>
            <div v-if="!isAuthenticated" class="mt-1">
              <a :href="menuContext.getAccountLink('login')" class="btn btn-sm btn-primary"
                >Sign In</a
              >
              <span class="ms-2 text-muted">
                &mdash;
                <a
                  href="https://music4dance.blog/music4dance-help/account-management/"
                  target="_blank"
                  rel="noopener"
                  >Help</a
                >
              </span>
            </div>
          </div>
        </li>

        <li class="mb-3 d-flex align-items-start">
          <div class="requirement-icon">
            <IBiCheckCircleFill
              v-if="isPremium"
              class="icon-check text-success"
              aria-label="Complete"
            />
            <IBiCircle v-else class="icon-check text-muted" aria-label="Incomplete" />
          </div>
          <div class="requirement-content">
            <strong>Have a premium subscription</strong>
            <div v-if="!isPremium" class="mt-1">
              <a href="/home/contribute" class="btn btn-sm btn-primary">Sign Up for Premium</a>
              <span class="ms-2 text-muted">
                &mdash;
                <a
                  href="https://music4dance.blog/music4dance-help/subscriptions/"
                  target="_blank"
                  rel="noopener"
                  >Help</a
                >
              </span>
            </div>
          </div>
        </li>

        <li class="mb-3 d-flex align-items-start">
          <div class="requirement-icon">
            <IBiCheckCircleFill
              v-if="hasSpotifyOAuth"
              class="icon-check text-success"
              aria-label="Complete"
            />
            <IBiCircle v-else class="icon-check text-muted" aria-label="Incomplete" />
          </div>
          <div class="requirement-content">
            <strong>Have associated a Spotify account with your music4dance account</strong>
            <div v-if="!hasSpotifyOAuth && isAuthenticated" class="mt-1">
              <a
                :href="spotifyConnectHref"
                class="btn btn-sm btn-primary"
                >{{ reauthRequired ? "Reconnect Spotify Account" : "Connect Spotify Account" }}</a
              >
              <span class="ms-2 text-muted">
                &mdash;
                <a
                  href="https://music4dance.blog/music4dance-help/account-management/#add-external-account"
                  target="_blank"
                  rel="noopener"
                  >Help</a
                >
              </span>
            </div>
            <div v-else-if="!hasSpotifyOAuth" class="mt-1 text-muted">
              (Sign in first to connect your Spotify account)
            </div>
          </div>
        </li>
      </ul>

      <div v-if="nextStep === 'sign-in'" class="alert alert-info mt-3">
        <strong>Next step:</strong> Sign in to your music4dance account to continue.
      </div>
      <div v-else-if="nextStep === 'premium'" class="alert alert-info mt-3">
        <strong>Next step:</strong> Upgrade to a premium subscription to unlock Spotify features.
      </div>
      <div v-else-if="nextStep === 'spotify'" class="alert alert-info mt-3">
        <strong>Next step:</strong>
        {{
          reauthRequired
            ? "Reconnect your Spotify account to enable playlist management."
            : "Connect your Spotify account to enable playlist management."
        }}
      </div>
    </div>

    <div v-else class="alert alert-success">
      <IBiCheckCircleFill class="icon-check me-2" />
      <strong>All requirements met!</strong> You can now use {{ featureName }}.
    </div>

    <hr />

    <h6>Benefits of Premium Membership:</h6>
    <ul>
      <li>Add songs directly to your Spotify playlists</li>
      <li>Create custom playlists with up to 1000 songs</li>
      <li>Advanced search and filtering capabilities</li>
      <li>Priority support</li>
      <li>Support the continued development of music4dance.net</li>
    </ul>

    <p class="text-muted mt-3">
      For more information, visit our
      <a
        href="https://music4dance.blog/music4dance-help/spotify-playlist/"
        target="_blank"
        rel="noopener"
        >help documentation</a
      >.
    </p>
  </BModal>
</template>

<style scoped>
.icon-check {
  font-size: 1.2rem;
  vertical-align: text-bottom;
}

.requirement-icon {
  flex-shrink: 0;
  width: 2rem;
  padding-top: 0.125rem;
}

.requirement-content {
  flex: 1;
  min-width: 0;
}
</style>
