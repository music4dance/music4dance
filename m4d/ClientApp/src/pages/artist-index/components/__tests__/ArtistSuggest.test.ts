import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { BFormInput } from "bootstrap-vue-next";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import ArtistSuggest from "../ArtistSuggest.vue";

setupTestEnvironment();

const { get } = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => ({}),
  getAxiosXsrf: () => ({ get }),
}));

const artists = (...names: [string, number][]) => ({
  data: { query: "q", artists: names.map(([name, songs]) => ({ name, songs })) },
});

function mountSuggest(includeAll = false) {
  return mount(ArtistSuggest, {
    props: { id: "artist-search", includeAll, modelValue: "" },
    global: { components: { BFormInput } },
  });
}

// Drive the model directly - that's what the watch listens to - then run out the fetch debounce.
async function type(wrapper: ReturnType<typeof mountSuggest>, value: string) {
  await wrapper.setProps({ modelValue: value });
  await vi.advanceTimersByTimeAsync(250);
  await flushPromises();
}

const options = (wrapper: ReturnType<typeof mountSuggest>) =>
  wrapper.findAll("datalist option").map((o) => [o.attributes("value"), o.text()]);

beforeEach(() => {
  get.mockReset();
  vi.useFakeTimers();
});

afterEach(() => vi.useRealTimers());

describe("ArtistSuggest", () => {
  it("suggests artists with their song counts", async () => {
    get.mockResolvedValue(artists(["Kenny Rogers", 12], ["Kenny Loggins", 1]));
    const wrapper = mountSuggest();

    await type(wrapper, "kenny");

    expect(get).toHaveBeenCalledWith("/api/suggestion/artist", {
      params: { q: "kenny", all: false },
    });
    expect(options(wrapper)).toEqual([
      ["Kenny Rogers", "12 songs"],
      ["Kenny Loggins", "1 song"],
    ]);
  });

  it("asks for nothing until there are two characters to go on", async () => {
    const wrapper = mountSuggest();

    await type(wrapper, "k");

    expect(get).not.toHaveBeenCalled();
    expect(options(wrapper)).toEqual([]);
  });

  it("passes the page's song-count filter through", async () => {
    get.mockResolvedValue(artists(["Kenny Rogers", 12]));
    const wrapper = mountSuggest(true);

    await type(wrapper, "kenny");

    expect(get).toHaveBeenCalledWith("/api/suggestion/artist", {
      params: { q: "kenny", all: true },
    });
  });

  it("ignores a stale response that lands after a newer one", async () => {
    let resolveFirst: (value: unknown) => void = () => {};
    get.mockReturnValueOnce(new Promise((resolve) => (resolveFirst = resolve)));
    get.mockResolvedValueOnce(artists(["Dolly Parton", 30]));

    const wrapper = mountSuggest();
    await type(wrapper, "do");
    await type(wrapper, "dolly");

    resolveFirst(artists(["Donna Summer", 8]));
    await flushPromises();

    expect(options(wrapper)).toEqual([["Dolly Parton", "30 songs"]]);
  });

  // Debouncing the input itself would hold the model back, so searching straight after typing
  // would submit the previous value. Only the lookup is debounced.
  it("updates the model as soon as it's typed, without waiting for the lookup", async () => {
    get.mockResolvedValue(artists(["Kenny Rogers", 12]));
    const wrapper = mountSuggest();

    await wrapper.find("input").setValue("kenny");

    expect(wrapper.emitted("update:modelValue")).toEqual([["kenny"]]);
    expect(get).not.toHaveBeenCalled();
  });

  it("shows nothing when the lookup fails", async () => {
    get.mockResolvedValueOnce(artists(["Kenny Rogers", 12]));
    const wrapper = mountSuggest();
    await type(wrapper, "kenny");
    expect(options(wrapper)).toHaveLength(1);

    get.mockRejectedValueOnce(new Error("offline"));
    await type(wrapper, "kenny r");

    expect(options(wrapper)).toEqual([]);
  });
});
