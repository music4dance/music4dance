/**
 * Test fixture for searches page.
 * Represents a realistic but minimal SearchesPageModel payload.
 */
export const model = {
  searches: [
    {
      id: 1001,
      userName: "alice",
      query: "index-CHA--Cha+Cha",
      description: "Cha Cha",
      searchUrl: "/Song/Index?filter=index-CHA--Cha+Cha",
      searchPageUrl: "/Song/Index?filter=index-CHA--Cha+Cha---.-2",
      mostRecentPage: 2,
      count: 42,
      created: "2023-03-15T10:00:00",
      modified: "2024-11-20T14:30:00",
      spotify: null,
      deleteUrl: "/Searches/Delete/1001?user=alice",
    },
    {
      id: 1002,
      userName: "alice",
      query: "index-WLT",
      description: "Waltz",
      searchUrl: "/Song/Index?filter=index-WLT",
      searchPageUrl: null,
      mostRecentPage: null,
      count: 15,
      created: "2023-06-01T08:00:00",
      modified: "2024-10-05T09:15:00",
      spotify: "3cEYpjA9oz9GiPac4AsH4n",
      deleteUrl: "/Searches/Delete/1002?user=alice",
    },
    {
      id: 1003,
      userName: "alice",
      query: "index--.-.-.-bpm120",
      description: "120 bpm",
      searchUrl: "/Song/Index?filter=index--.-.-.-bpm120",
      searchPageUrl: null,
      mostRecentPage: null,
      count: 7,
      created: "2024-01-10T12:00:00",
      modified: "2024-01-10T12:00:00",
      spotify: null,
      deleteUrl: "/Searches/Delete/1003?user=alice",
    },
  ],
  page: 1,
  totalPages: 3,
  sort: null,
  showDetails: false,
  spotifyOnly: false,
  user: "alice",
  isAdmin: false,
  canDeleteAll: true,
  basicSearchUrl: "/Song/Index?user=alice",
  advancedSearchUrl: "/Song/AdvancedSearchForm?filter=index",
  deleteAllUrl: "/Searches/DeleteAll",
};

export const adminModel = {
  ...model,
  isAdmin: true,
  showDetails: true,
};

export const allUsersModel = {
  ...model,
  user: "all",
  canDeleteAll: false,
  deleteAllUrl: null,
};
