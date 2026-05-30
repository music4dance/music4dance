/**
 * Test fixture for the playlist index page.
 * Represents a realistic but minimal PlayListPageModel payload for SongsFromSpotify.
 */
export const model = {
  playLists: [
    {
      id: "spotify-abc123",
      user: "alice",
      type: 2,
      name: "Waltz Songs",
      description: "A waltz playlist",
      data1: "WAL:Dance",
      data2: "song1|song2",
      created: "2024-01-15T00:00:00",
      updated: "2024-06-01T00:00:00",
      deleted: false,
    },
    {
      id: "spotify-def456",
      user: "bob",
      type: 2,
      name: "Tango Songs",
      description: "A tango playlist",
      data1: "TAN:Dance",
      data2: null,
      created: "2024-02-20T00:00:00",
      updated: null,
      deleted: false,
    },
    {
      id: "spotify-ghi789",
      user: "alice",
      type: 2,
      name: "Old Playlist",
      description: "This one was deleted",
      data1: null,
      data2: null,
      created: "2023-05-10T00:00:00",
      updated: "2023-06-01T00:00:00",
      deleted: true,
    },
  ],
  type: 2,
  filteredUser: null,
  data1Name: "Tags",
  data2Name: "SongIds",
};

export const modelWithFilteredUser = {
  ...model,
  filteredUser: "alice",
};
