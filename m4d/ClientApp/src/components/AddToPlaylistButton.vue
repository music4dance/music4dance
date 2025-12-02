<script setup lang="ts">
import { ref, computed } from "vue";
import { useToast } from "bootstrap-vue-next";
import { PurchaseInfo, ServiceType } from "@/models/Purchase";
import { getMenuContext } from "@/helpers/GetMenuContext";
import PremiumRequiredModal from "./PremiumRequiredModal.vue";

interface PlaylistMetadata {
  id: string;
  name: string;
  description?: string;
  link?: string;
  reference?: string;
  count?: number;
}

interface AddToPlaylistRequest {
  songId: string;
  playlistId: string;
}

interface AddToPlaylistResult {
  success: boolean;
  message: string;
  snapshotId?: string;
}

const props = withDefaults(
  defineProps<{
    purchaseInfos: PurchaseInfo[];
    variant?: string;
    size?: string;
    showText?: boolean;
  }>(),
  {
    variant: "outline-primary",
    size: "sm",
    showText: true,
  },
);

const { create: createToast } = useToast();
const menuContext = getMenuContext();

const playlists = ref<PlaylistMetadata[]>([]);
const loading = ref(false);
const playlistsCached = ref(false);
const showPremiumModal = ref(false);

const spotifyInfo = computed(() =>
  props.purchaseInfos.find((p) => p.service === ServiceType.Spotify),
);

const hasSpotifyTrack = computed(() => spotifyInfo.value !== undefined);

const buttonText = computed(() => (props.showText ? "Add to Playlist" : ""));

const CACHE_KEY = "spotify_playlists";
const CACHE_DURATION = 1000 * 60 * 15; // 15 minutes

interface CachedPlaylists {
  playlists: PlaylistMetadata[];
  timestamp: number;
  userId: string;
}

const loadFromCache = (): PlaylistMetadata[] | null => {
  try {
    const cached = localStorage.getItem(CACHE_KEY);
    if (!cached) return null;

    const data: CachedPlaylists = JSON.parse(cached);
    const age = Date.now() - data.timestamp;

    // Invalidate if wrong user or too old
    if (data.userId !== menuContext.userId || age > CACHE_DURATION) {
      localStorage.removeItem(CACHE_KEY);
      return null;
    }

    return data.playlists;
  } catch (error) {
    console.error("Error loading playlists from cache:", error);
    localStorage.removeItem(CACHE_KEY);
    return null;
  }
};

const saveToCache = (playlistsToSave: PlaylistMetadata[]) => {
  try {
    if (!menuContext.userId) return;

    const data: CachedPlaylists = {
      playlists: playlistsToSave,
      timestamp: Date.now(),
      userId: menuContext.userId,
    };
    localStorage.setItem(CACHE_KEY, JSON.stringify(data));
  } catch (error) {
    console.error("Error saving playlists to cache:", error);
  }
};

const fetchPlaylists = async (forceRefresh = false) => {
  // Check cache first
  if (!forceRefresh && playlistsCached.value && playlists.value.length > 0) {
    return;
  }

  if (!forceRefresh) {
    const cached = loadFromCache();
    if (cached) {
      playlists.value = cached;
      playlistsCached.value = true;
      return;
    }
  }

  loading.value = true;
  try {
    const response = await menuContext.axiosXsrf.get<PlaylistMetadata[]>(
      "/api/spotify/playlist/user",
    );

    playlists.value = response.data;
    playlistsCached.value = true;
    saveToCache(response.data);
  } catch (error: any) {
    await handleError(error, "fetching playlists");
  } finally {
    loading.value = false;
  }
};

const addToPlaylist = async (playlistId: string, playlistName: string) => {
  if (!spotifyInfo.value) return;

  loading.value = true;
  try {
    const request: AddToPlaylistRequest = {
      songId: spotifyInfo.value.songId,
      playlistId,
    };

    const response = await menuContext.axiosXsrf.post<AddToPlaylistResult>(
      "/api/spotify/playlist/add",
      request,
    );

    if (response.data.success) {
      createToast({
        props: {
          title: "Success",
          body: `Song added to "${playlistName}"`,
          variant: "success",
        },
      });
    } else {
      createToast({
        props: {
          title: "Error",
          body: response.data.message || "Failed to add song to playlist",
          variant: "danger",
        },
      });
    }
  } catch (error: any) {
    await handleError(error, `adding song to playlist`);
  } finally {
    loading.value = false;
  }
};

const handleError = async (error: any, context: string) => {
  const status = error.response?.status;
  const data = error.response?.data;

  switch (status) {
    case 401: // Unauthorized
      window.location.href = menuContext.getAccountLink("login");
      break;

    case 402: // Payment Required (not premium)
      showPremiumModal.value = true;
      break;

    case 403: // Forbidden (no Spotify OAuth)
      createToast({
        props: {
          title: "Spotify Account Required",
          body: "Please connect your Spotify account to use this feature.",
          variant: "warning",
        },
      });
      setTimeout(() => {
        window.location.href = `/identity/account/manage/externallogins?returnUrl=${window.location.pathname}`;
      }, 2000);
      break;

    case 404: // Not found (song not on Spotify)
      createToast({
        props: {
          title: "Not Available",
          body: data?.message || "This song is not available on Spotify",
          variant: "warning",
        },
      });
      break;

    default:
      createToast({
        props: {
          title: "Error",
          body: data?.message || `Unable to ${context}. Please try again later.`,
          variant: "danger",
        },
      });
      console.error(`Error ${context}:`, error);
  }
};

const onDropdownShow = async () => {
  if (!playlistsCached.value) {
    await fetchPlaylists();
  }
};

const refreshPlaylists = async () => {
  await fetchPlaylists(true);
  createToast({
    props: {
      title: "Playlists Refreshed",
      body: "Your playlists have been reloaded",
      variant: "info",
    },
  });
};
</script>

<template>
  <div v-if="hasSpotifyTrack" class="d-inline-block">
    <BDropdown
      :variant="variant"
      :size="size"
      text="Add to Playlist"
      :disabled="loading"
      @show="onDropdownShow"
    >
      <template #button-content>
        <IBiSpotify v-if="!loading" aria-hidden="true" />
        <BSpinner v-else small aria-hidden="true" />
        {{ buttonText }}
      </template>

      <BDropdownItem v-if="loading" disabled>
        <BSpinner small /> Loading playlists...
      </BDropdownItem>

      <template v-else-if="playlists.length > 0">
        <BDropdownItem
          v-for="playlist in playlists"
          :key="playlist.id"
          @click="addToPlaylist(playlist.id, playlist.name)"
        >
          {{ playlist.name }}
          <span v-if="playlist.count !== undefined" class="text-muted">
            ({{ playlist.count }} songs)
          </span>
        </BDropdownItem>

        <BDropdownDivider />
        <BDropdownItem @click="refreshPlaylists">
          <IBiArrowClockwise aria-hidden="true" /> Refresh Playlists
        </BDropdownItem>
      </template>

      <BDropdownItem v-else disabled> No playlists found </BDropdownItem>
    </BDropdown>

    <PremiumRequiredModal v-model="showPremiumModal" feature-name="Add to Spotify Playlist" />
  </div>
</template>
