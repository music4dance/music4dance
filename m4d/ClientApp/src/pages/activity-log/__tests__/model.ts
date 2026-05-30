/**
 * Test fixture for activity-log page.
 * Represents a realistic but minimal ActivityLogPageModel payload.
 */
export const model = {
  entries: [
    {
      id: 5001,
      date: "2024-11-20T14:30:00+00:00",
      userName: "alice",
      action: "Login",
      details: "Logged in via Spotify",
    },
    {
      id: 5002,
      date: "2024-11-19T09:15:00+00:00",
      userName: "bob",
      action: "EditSong",
      details: "Updated tempo for song abc123",
    },
    {
      id: 5003,
      date: "2024-11-18T11:00:00+00:00",
      userName: null,
      action: "AnonymousSearch",
      details: "Searched for Waltz",
    },
  ],
  page: 1,
  totalPages: 5,
};

export const singlePageModel = {
  entries: [
    {
      id: 5001,
      date: "2024-11-20T14:30:00+00:00",
      userName: "alice",
      action: "Login",
      details: "Logged in via Spotify",
    },
  ],
  page: 1,
  totalPages: 1,
};
