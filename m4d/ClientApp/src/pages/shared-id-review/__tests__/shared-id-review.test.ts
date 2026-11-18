import { afterEach, beforeEach, describe, expect, test, vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { loadTestPage, testPageSnapshot } from "@/helpers/TestPageSnapshot";
import App from "../App.vue";
import { emptyModel, model, songA, songB, songC, songD, songE } from "./model";

const { post, navigateTo } = vi.hoisted(() => ({ post: vi.fn(), navigateTo: vi.fn() }));

vi.mock("@/helpers/GetMenuContext", async () => {
  const { MenuContext } = await import("@/models/MenuContext");
  const context = new MenuContext({ userName: "admin", roles: ["dbAdmin"], xsrfToken: "token" });
  Object.defineProperty(context, "axiosXsrf", { get: () => ({ post }) });
  return { getMenuContext: () => context, getAxiosXsrf: () => context.axiosXsrf };
});

vi.mock("../navigate", async (original) => ({
  ...(await original<typeof import("../navigate")>()),
  navigateTo,
}));

function cards(wrapper: VueWrapper) {
  return wrapper.findAll(".card");
}

function lastRequest() {
  const [url, body] = post.mock.calls.at(-1)!;
  return { url, body };
}

describe("shared-id-review page", () => {
  beforeEach(() => {
    post.mockReset();
    navigateTo.mockReset();
    post.mockResolvedValue({ data: { message: "Merged 1, marked 0 not the same, left 1." } });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test("renders the review page", () => {
    testPageSnapshot(App, model);
  });

  test("renders an empty queue", () => {
    const wrapper = loadTestPage(App, emptyModel);

    expect(wrapper.text()).toContain("0 pending pairs in 0 clusters.");
    expect(wrapper.text()).toContain("Run Preview merges");
    expect(wrapper.text()).not.toContain("left for later come last");
    expect(wrapper.find(".apply-button").exists()).toBe(false);
  });

  describe("the page", () => {
    test("says how the queue is sorted, and shows the last Apply's message", () => {
      const wrapper = loadTestPage(App, model);

      expect(wrapper.find("#queue-summary").text()).toContain("51 pending pairs in 45 clusters.");
      expect(wrapper.find("#queue-summary").text()).toContain("the 4 clusters still pending");
      expect(wrapper.find("#queue-summary").text()).toContain(
        "The 3 clusters left for later come last.",
      );
      expect(wrapper.text()).toContain("Merged 1, marked 0 not the same, left 2.");
    });

    test("says what the last preview found to merge, and links to it", () => {
      const wrapper = loadTestPage(App, model);
      const summary = wrapper.find("#queue-summary");

      expect(summary.text()).toContain(
        "812 clusters with matching titles and artists, 140 that differ only by feat. and 25 " +
          "with artists credited differently",
      );
      expect(summary.find('a[href="/SharedIds/Preview"]').exists()).toBe(true);
    });

    test("the background tasks post to the SharedIds routes with the antiforgery token", () => {
      const wrapper = loadTestPage(App, model);
      const actions = wrapper.findAll("form").map((f) => f.attributes("action"));

      expect(actions).toEqual(
        expect.arrayContaining([
          "/SharedIds/Seed",
          "/SharedIds/MergeMatching",
          "/SharedIds/MergeFeaturing",
          "/SharedIds/MergeCredits",
          "/SharedIds/PreviewMergeMatching",
          "/Song/BulkEdit",
        ]),
      );
      expect(
        wrapper.find('form[action="/SharedIds/Seed"] input[name="__RequestVerificationToken"]')
          .element,
      ).toHaveProperty("value", "token");
    });

    test("a background task asks first, and doesn't post when cancelled", async () => {
      const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
      const wrapper = loadTestPage(App, model);
      const event = new Event("submit", { cancelable: true });

      wrapper.find('form[action="/SharedIds/Seed"]').element.dispatchEvent(event);

      expect(confirm).toHaveBeenCalled();
      expect(event.defaultPrevented).toBe(true);
    });

    test("a page link reloads that page of the queue", async () => {
      const wrapper = loadTestPage(App, model);

      await wrapper.find('.pagination [aria-posinset="3"]').trigger("click");

      expect(navigateTo).toHaveBeenCalledWith("/SharedIds?page=3");
    });
  });

  describe("a cluster", () => {
    test("shows its badges", () => {
      const wrapper = loadTestPage(App, model);
      const [feat, rejected] = cards(wrapper);

      expect(feat!.find(".card-header").text()).toContain("Differ only by feat.");
      expect(feat!.find(".card-header").text()).toContain("Cleans up feat.");
      expect(rejected!.find(".card-header").text()).toContain("Has a rejected pair");
      expect(rejected!.find(".card-header").text()).not.toContain("Close match");
      expect(rejected!.find(".card-header").text()).toContain("Lengths 0:42 apart");
      expect(feat!.find(".card-header").text()).not.toContain("apart");
    });

    test("starts on its default action", () => {
      const wrapper = loadTestPage(App, model);

      expect((wrapper.find("#cluster-0-action-Merge").element as HTMLInputElement).checked).toBe(
        true,
      );
      expect((wrapper.find("#cluster-1-action-Leave").element as HTMLInputElement).checked).toBe(
        true,
      );
    });

    test("whose songs changed says to reload and has no choice", () => {
      const wrapper = loadTestPage(App, model);
      const stale = cards(wrapper)[2]!;

      expect(stale.text()).toContain("These songs changed while the page loaded.");
      expect(stale.find('input[type="radio"]').exists()).toBe(false);
    });

    test("without a preview says so", () => {
      const wrapper = loadTestPage(App, model);

      expect(cards(wrapper)[1]!.text()).toContain("The merge couldn't be previewed.");
    });

    test("says it's split when a song is put in another group", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;
      expect(card.find(".split-note").exists()).toBe(false);

      await card.find('select[aria-label="Group for song 2"]').setValue("B");

      expect(card.find(".split-note").exists()).toBe(true);
      expect(card.find(".card-header").text()).toContain("Changed");
      expect(wrapper.find(".changed-count").text()).toBe("1 changed from their defaults");
    });

    test("disables the name radios of a song that's left out", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;

      await card.find('select[aria-label="Group for song 2"]').setValue("");

      const radio = card.find(`#cluster-0-title-${songB}`).element as HTMLInputElement;
      expect(radio.disabled).toBe(true);
    });

    test("removing an album strikes it out and disables its ids", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;

      await card.find('input[aria-label="Remove First Album from song 1"]').setValue(true);

      const album = card.find(".shared-id-album");
      expect(album.classes()).toContain("text-decoration-line-through");
      const id = album.find('input[aria-label="Remove S:sp1 from song 1"]')
        .element as HTMLInputElement;
      expect(id.disabled).toBe(true);
      expect(id.checked).toBe(true);
    });

    test("links the ids that have a track page and marks the shared ones", () => {
      const wrapper = loadTestPage(App, model);
      const album = cards(wrapper)[0]!.find(".shared-id-album");

      expect(album.find('a[href="https://open.spotify.com/track/sp1"]').text()).toBe("S:sp1");
      expect(album.find("mark").text()).toBe("R:USRC1");
    });

    test("Merge and edit posts the cluster's songs to BulkEdit", () => {
      const wrapper = loadTestPage(App, model);
      const form = cards(wrapper)[0]!.find('form[action="/Song/BulkEdit"]');

      expect(form.findAll('input[name="selectedSongs"]').map((i) => i.attributes("value"))).toEqual(
        [songA, songB],
      );
    });
  });

  describe("deleting a song", () => {
    test("marks its column and disables its group and boxes", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;

      await card.find('input[aria-label="Delete song 2"]').setValue(true);

      const header = card.find('input[aria-label="Delete song 2"]').element.closest("th")!;
      expect(header.classList).toContain("table-danger");
      expect(
        card.find('select[aria-label="Group for song 2"]').attributes("disabled"),
      ).toBeDefined();
      const album = card.find('input[aria-label="Remove Second Album from song 2"]')
        .element as HTMLInputElement;
      expect(album.disabled).toBe(true);
      expect((card.find(`#cluster-0-title-${songB}`).element as HTMLInputElement).disabled).toBe(
        true,
      );
    });

    test("asks first on Apply, and doesn't post when cancelled", async () => {
      const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
      const wrapper = loadTestPage(App, model);
      await cards(wrapper)[0]!.find('input[aria-label="Delete song 2"]').setValue(true);

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      expect(confirm).toHaveBeenCalledWith("Apply will delete 1 songs. Delete them?");
      expect(post).not.toHaveBeenCalled();
    });

    test("is sent with the cluster's choice once confirmed", async () => {
      vi.spyOn(window, "confirm").mockReturnValue(true);
      const wrapper = loadTestPage(App, model);
      await cards(wrapper)[0]!.find('input[aria-label="Delete song 2"]').setValue(true);

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      const [feat] = lastRequest().body.choices;
      expect(feat.deletions).toEqual([songB]);
      expect(feat.songs).toEqual([
        { songId: songA, group: "A" },
        { songId: songB, group: "" },
      ]);
    });
  });

  describe("Look up on Spotify and iTunes", () => {
    test("posts the cluster's ids and puts what each service says in their tooltips", async () => {
      post.mockResolvedValueOnce({
        data: [
          {
            id: "S:sp1",
            found: true,
            title: "Probe",
            artist: "Probe Artist",
            album: "Spotify Album",
            length: 185,
            isrc: "USRC1",
          },
          { id: "I:111", found: false },
        ],
      });
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;
      const tips = (id: string) =>
        card
          .findAll(".shared-id-track")
          .filter((t) => t.text() === id)
          .map((t) => t.find(".track-id"))
          .map((t) => t.attributes("title"));
      expect(tips("S:sp1")[0]).toBeUndefined();

      await card.find(".look-up-button").trigger("click");
      await flushPromises();

      expect(lastRequest()).toEqual({
        url: "/api/sharedids/tracks",
        body: { ids: ["S:sp1", "I:111"] },
      });
      expect(tips("S:sp1")).toEqual(["Probe — Probe Artist\nSpotify Album\n3:05 · R:USRC1"]);
      expect(tips("I:111")).toEqual(["Not found"]);
      const missing = card.findAll(".shared-id-track").find((t) => t.text() === "I:111")!;
      expect(missing.find(".text-danger").exists()).toBe(true);
      expect(card.find(".look-up-button").text()).toBe("Look up again");
    });

    test("a failure is shown on the card", async () => {
      post.mockRejectedValueOnce(new Error("Request failed with status code 500"));
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;

      await card.find(".look-up-button").trigger("click");
      await flushPromises();

      expect(card.text()).toContain("Lookup failed: Request failed with status code 500");
      expect(card.find(".look-up-button").attributes("disabled")).toBeUndefined();
    });
  });

  describe("Sort out ids", () => {
    const owners = {
      ids: [
        { id: "S:sp9", status: "Owned", reason: 'The song is "Alpha" by One (3:05)', song: songC },
      ],
      removals: [
        { songId: songD, album: 0 },
        { songId: songE, album: 1 },
      ],
      tracks: [{ id: "S:sp9", found: true, title: "Alpha", artist: "One", length: 185 }],
    };

    test("on a card, posts its songs and checks the proposed removals", async () => {
      post.mockResolvedValueOnce({ data: owners });
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[1]!;
      expect(cards(wrapper)[0]!.find(".sort-out-button").exists()).toBe(false);

      await card.find(".sort-out-button").trigger("click");
      await flushPromises();

      expect(lastRequest()).toEqual({
        url: "/api/sharedids/owners",
        body: { songIds: [songC, songD, songE] },
      });
      expect(card.find(".owners").text()).toContain("S:sp9 belongs to song 1");
      expect(card.find('input[aria-label="Remove B1 from song 2"]').element).toHaveProperty(
        "checked",
        true,
      );
      expect(card.find('input[aria-label="Remove A1 from song 1"]').element).toHaveProperty(
        "checked",
        false,
      );
      expect(card.find("#cluster-1-action-Leave").element).toHaveProperty("checked", true);
    });

    test("on the page, sorts out each cluster that's probably different songs", async () => {
      post.mockResolvedValueOnce({ data: owners });
      const wrapper = loadTestPage(App, model);
      const button = wrapper.find(".sort-out-page-button");
      expect(button.text()).toBe("Sort out ids on this page (1)");

      await button.trigger("click");
      await flushPromises();

      expect(post).toHaveBeenCalledTimes(1);
      expect(lastRequest().url).toBe("/api/sharedids/owners");
      expect(wrapper.find(".sorting-progress").text()).toBe(
        "Sorted out the ids of 1 clusters. Check the proposals, then Apply.",
      );
      expect(cards(wrapper)[1]!.find(".owners").exists()).toBe(true);
    });

    test("a failure on the page is counted", async () => {
      post.mockRejectedValueOnce(new Error("Request failed with status code 500"));
      const wrapper = loadTestPage(App, model);

      await wrapper.find(".sort-out-page-button").trigger("click");
      await flushPromises();

      expect(wrapper.find(".sorting-progress").text()).toContain("1 failed");
    });

    test("a failure on a card is shown on it", async () => {
      post.mockRejectedValueOnce(new Error("Request failed with status code 500"));
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[1]!;

      await card.find(".sort-out-button").trigger("click");
      await flushPromises();

      expect(card.text()).toContain("Sort out ids failed: Request failed with status code 500");
    });
  });

  describe("Apply", () => {
    test("posts every cluster with songs, then reloads the page with the result", async () => {
      const wrapper = loadTestPage(App, model);

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      const { url, body } = lastRequest();
      expect(url).toBe("/api/sharedids/apply");
      expect(body.page).toBe(2);
      expect(body.choices.map((c: { action: string }) => c.action)).toEqual(["Merge", "Leave"]);
      expect(navigateTo).toHaveBeenCalledWith(
        "/SharedIds?page=2&message=Merged+1%2C+marked+0+not+the+same%2C+left+1.",
      );
    });

    test("sends the reviewer's choices", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;
      await card.find(`#cluster-0-title-${songB}`).setValue(true);
      await card.find('input[aria-label="Remove I:111 from song 2"]').setValue(true);
      await wrapper.find("#cluster-1-action-Reject").setValue(true);

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      const [feat, rejected] = lastRequest().body.choices;
      expect(feat.titleFrom).toBe(songB);
      expect(feat.artistFrom).toBeNull();
      expect(feat.removals).toEqual([{ songId: songB, album: 2, id: "I:111" }]);
      expect(rejected.action).toBe("Reject");
      expect(rejected.candidateIds).toEqual([21, 22]);
    });

    test("sends what's typed over the If merged names", async () => {
      const wrapper = loadTestPage(App, model);
      const card = cards(wrapper)[0]!;
      const box = card.find("#cluster-0-title-typed");
      expect(box.element).toHaveProperty("value", "Probe");

      await box.setValue("Typed Title");
      expect(card.find(".typed-note").text()).toContain("Typed over: Probe");

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      const [feat] = lastRequest().body.choices;
      expect(feat.title).toBe("Typed Title");
      expect(feat.artist).toBeNull();
    });

    test("sends a tempo typed over the merge's", async () => {
      const wrapper = loadTestPage(App, model);
      const box = cards(wrapper)[0]!.find("#cluster-0-tempo-typed");
      expect(box.attributes("type")).toBe("number");
      expect(box.element).toHaveProperty("value", "120");

      await box.setValue("132");
      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      const [feat] = lastRequest().body.choices;
      expect([feat.tempoFrom, feat.tempo]).toEqual([null, 132]);
    });

    test("a failure is shown and the page stays", async () => {
      post.mockRejectedValue(new Error("Request failed with status code 500"));
      const wrapper = loadTestPage(App, model);

      await wrapper.find(".apply-button").trigger("click");
      await flushPromises();

      expect(wrapper.text()).toContain("Apply failed: Request failed with status code 500");
      expect(navigateTo).not.toHaveBeenCalled();
      expect(wrapper.find(".apply-button").attributes("disabled")).toBeUndefined();
    });
  });
});
