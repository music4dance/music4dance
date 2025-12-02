import { mount } from "@vue/test-utils";
import { describe, expect, test, vi, beforeEach } from "vitest";
import { PurchaseInfo } from "@/models/Purchase";
import { SpotifyPurchaseInfo } from "@/models/Purchase";
import AddToPlaylistButton from "../AddToPlaylistButton.vue";

interface PlaylistMetadata {
  id: string;
  name: string;
  count?: number;
}

// Mock getMenuContext
vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => ({
    userId: "test-user-123",
    isPremium: true,
    isAuthenticated: true,
    hasRole: (role: string) => role === "canSpotify", // Mock hasRole to return true for canSpotify
    axiosXsrf: {
      get: vi.fn(),
      post: vi.fn(),
    },
    getAccountLink: (page: string) => `/identity/account/${page}`,
  }),
}));

// Mock bootstrap-vue-next toast
vi.mock("bootstrap-vue-next", () => ({
  useToast: () => ({
    create: vi.fn(),
  }),
  BDropdown: { name: "BDropdown", template: "<div><slot /></div>" },
  BDropdownItem: { name: "BDropdownItem", template: "<div><slot /></div>" },
  BDropdownDivider: { name: "BDropdownDivider", template: "<div />" },
  BSpinner: { name: "BSpinner", template: "<div>Loading...</div>" },
}));

describe("AddToPlaylistButton.vue", () => {
  let mockPurchaseInfos: PurchaseInfo[];

  beforeEach(() => {
    // Create mock purchase infos with Spotify
    mockPurchaseInfos = [
      new SpotifyPurchaseInfo({
        songId: "spotify-song-123",
        albumId: "spotify-album-456",
      }),
    ];

    // Clear localStorage before each test
    localStorage.clear();
  });

  test("renders button when song has Spotify track", () => {
    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: mockPurchaseInfos, songId: "test-song-id" },
    });
    expect(wrapper.find(".d-inline-block").exists()).toBe(true);
  });

  test("does not render when song has no Spotify track", () => {
    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: [], songId: "test-song-id" },
    });
    expect(wrapper.find(".d-inline-block").exists()).toBe(false);
  });

  test("applies custom variant and size props", () => {
    const wrapper = mount(AddToPlaylistButton, {
      props: {
        purchaseInfos: mockPurchaseInfos,
        songId: "test-song-id",
        variant: "primary",
        size: "lg",
      },
    });
    expect(wrapper.props().variant).toBe("primary");
    expect(wrapper.props().size).toBe("lg");
  });

  test("shows/hides button text based on showText prop", () => {
    const wrapperWithText = mount(AddToPlaylistButton, {
      props: {
        purchaseInfos: mockPurchaseInfos,
        songId: "test-song-id",
        showText: true,
      },
    });
    // buttonText is a computed property, check the rendered text content
    expect(wrapperWithText.text()).toContain("Add to Playlist");

    const wrapperWithoutText = mount(AddToPlaylistButton, {
      props: {
        purchaseInfos: mockPurchaseInfos,
        songId: "test-song-id",
        showText: false,
      },
    });
    // When showText is false, buttonText should be empty
    expect(wrapperWithoutText.text()).not.toContain("Add to Playlist");
  });

  test("caches playlists in sessionStorage", () => {
    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: mockPurchaseInfos, songId: "test-song-id" },
    });

    const testPlaylists = [
      { id: "playlist1", name: "My Playlist 1", count: 50 },
      { id: "playlist2", name: "My Playlist 2", count: 25 },
    ];

    // Access the saveToCache method through vm
    (wrapper.vm as unknown as { saveToCache: (playlists: PlaylistMetadata[]) => void }).saveToCache(
      testPlaylists,
    );

    const cached = sessionStorage.getItem("spotify_playlists");
    expect(cached).toBeTruthy();

    const parsed = JSON.parse(cached!);
    expect(parsed.playlists).toHaveLength(2);
    expect(parsed.userId).toBe("test-user-123");
    expect(parsed.timestamp).toBeLessThanOrEqual(Date.now());
  });

  test("loads playlists from cache", () => {
    const testPlaylists = [{ id: "playlist1", name: "Cached Playlist", count: 100 }];

    const cacheData = {
      playlists: testPlaylists,
      timestamp: Date.now(),
      userId: "test-user-123",
    };

    sessionStorage.setItem("spotify_playlists", JSON.stringify(cacheData));

    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: mockPurchaseInfos, songId: "test-song-id" },
    });

    const loaded = (
      wrapper.vm as unknown as { loadFromCache: () => PlaylistMetadata[] | null }
    ).loadFromCache();
    expect(loaded).toBeTruthy();
    expect(loaded).toHaveLength(1);
    expect(loaded?.[0]?.name).toBe("Cached Playlist");
  });

  test("invalidates cache for different user", () => {
    const cacheData = {
      playlists: [{ id: "playlist1", name: "Other User Playlist" }],
      timestamp: Date.now(),
      userId: "different-user",
    };

    sessionStorage.setItem("spotify_playlists", JSON.stringify(cacheData));

    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: mockPurchaseInfos, songId: "test-song-id" },
    });

    const loaded = (
      wrapper.vm as unknown as { loadFromCache: () => PlaylistMetadata[] | null }
    ).loadFromCache();
    expect(loaded).toBeNull();
    expect(sessionStorage.getItem("spotify_playlists")).toBeNull();
  });

  test("invalidates expired cache", () => {
    const cacheData = {
      playlists: [{ id: "playlist1", name: "Old Playlist" }],
      timestamp: Date.now() - 1000 * 60 * 20, // 20 minutes ago (expired)
      userId: "test-user-123",
    };

    sessionStorage.setItem("spotify_playlists", JSON.stringify(cacheData));

    const wrapper = mount(AddToPlaylistButton, {
      props: { purchaseInfos: mockPurchaseInfos, songId: "test-song-id" },
    });

    const loaded = (
      wrapper.vm as unknown as { loadFromCache: () => PlaylistMetadata[] | null }
    ).loadFromCache();
    expect(loaded).toBeNull();
  });
});
