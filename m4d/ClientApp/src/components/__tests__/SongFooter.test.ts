import { mount } from "@vue/test-utils";
import { describe, expect, test, vi, beforeEach, afterEach } from "vitest";
import { SongListModel } from "@/models/SongListModel";
import { setupTestEnvironment } from "@/helpers/TestHelpers";
import SongFooter from "../SongFooter.vue";

setupTestEnvironment();

const mockGetMenuContext = vi.fn();
vi.mock("@/helpers/GetMenuContext", () => ({
  getMenuContext: () => mockGetMenuContext(),
}));

const stubComponents = {
  BRow: { template: "<div><slot /></div>" },
  BCol: { template: "<div><slot /></div>" },
  BPagination: { template: "<div />" },
};

function createModel(overrides: Partial<SongListModel> = {}): SongListModel {
  return new SongListModel({ count: 100, ...overrides });
}

function mountFooter(model?: SongListModel) {
  return mount(SongFooter, {
    props: { model: model ?? createModel() },
    global: { stubs: stubComponents },
  });
}

function getInput(wrapper: ReturnType<typeof mount>) {
  return wrapper.find("input[aria-label='Page number']");
}

const INITIAL_HREF = "http://localhost/song";

describe("SongFooter.vue", () => {
  beforeEach(() => {
    mockGetMenuContext.mockReturnValue({
      userName: "test-user",
      hasRole: () => false,
    });

    // Mock window.location with a fixed URL so tests are not order-dependent
    Object.defineProperty(window, "location", {
      value: { href: INITIAL_HREF },
      writable: true,
      configurable: true,
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  test("renders page number input with correct attributes", () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    expect(input.exists()).toBe(true);
    expect(input.attributes("type")).toBe("number");
    expect(input.attributes("step")).toBe("1");
    expect(input.attributes("min")).toBe("1");
    expect(input.attributes("max")).toBe("4"); // ceil(100/25)
    expect(input.attributes("aria-label")).toBe("Page number");
  });

  test("displays correct page count", () => {
    const wrapper = mountFooter(createModel({ count: 250 }));
    expect(wrapper.text()).toContain("of 10");
  });

  test("navigates to valid page on Enter", async () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue(3);
    await input.trigger("keydown.enter");

    expect(window.location.href).toContain("page=3");
  });

  test("navigates to valid page on blur", async () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue(2);
    await input.trigger("blur");

    expect(window.location.href).toContain("page=2");
  });

  test("does not navigate when page is unchanged", async () => {
    // Default page is 1, so submitting 1 should not navigate
    const originalHref = window.location.href;
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue(1);
    await input.trigger("blur");

    expect(window.location.href).toBe(originalHref);
  });

  test("clamps page below 1 to 1", async () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue(0);
    await input.trigger("keydown.enter");

    // Page 1 is the current page so no navigation, but value should be clamped
    expect((input.element as HTMLInputElement).value).toBe("1");
  });

  test("clamps page above max to pageCount", async () => {
    const wrapper = mountFooter(createModel({ count: 50 })); // 2 pages
    const input = getInput(wrapper);

    await input.setValue(99);
    await input.trigger("keydown.enter");

    expect(window.location.href).toContain("page=2");
  });

  test("resets to current page on empty/NaN input", async () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue("");
    await input.trigger("blur");

    // Should reset to current page (1) without navigating
    expect((input.element as HTMLInputElement).value).toBe("1");
  });

  test("floors decimal input to whole number", async () => {
    const wrapper = mountFooter();
    const input = getInput(wrapper);

    await input.setValue(2.7);
    await input.trigger("keydown.enter");

    expect(window.location.href).toContain("page=2");
  });

  test("shows New Search link for simple filter", () => {
    const wrapper = mountFooter();
    const links = wrapper.findAll("a");
    const newSearchLink = links.find((l) => l.text() === "New Search");

    expect(newSearchLink).toBeDefined();
    expect(newSearchLink!.attributes("href")).toBe("/song");
  });

  test("shows advanced search link for non-simple filter", () => {
    const model = createModel();
    // Make filter non-simple by setting action to customsearch (isRaw = true)
    model.filter.action = "customsearch";

    const wrapper = mountFooter(model);
    const links = wrapper.findAll("a");
    const newSearchLink = links.find((l) => l.text() === "New Search");
    expect(newSearchLink).toBeDefined();
    expect(newSearchLink!.attributes("href")).toBe("/song/advancedsearchform");
  });

  test("shows export link for diagnostics role", () => {
    mockGetMenuContext.mockReturnValue({
      userName: "admin-user",
      hasRole: (role: string) => role === "showDiagnostics",
    });

    const model = createModel();
    model.filter.dances = "SWZ";

    const wrapper = mount(SongFooter, {
      props: { model },
      global: { stubs: stubComponents },
    });

    const links = wrapper.findAll("a");
    const exportLink = links.find((l) => l.text() === "Export to File");
    expect(exportLink).toBeDefined();
  });

  test("hides export link for non-privileged users", () => {
    const model = createModel();
    model.filter.dances = "SWZ";

    const wrapper = mountFooter(model);

    const links = wrapper.findAll("a");
    const exportLink = links.find((l) => l.text() === "Export to File");
    expect(exportLink).toBeUndefined();
  });

  test("uses href prop for link generation when provided", async () => {
    const wrapper = mount(SongFooter, {
      props: { model: createModel(), href: "/custom/path" },
      global: { stubs: stubComponents },
    });
    const input = getInput(wrapper);

    await input.setValue(3);
    await input.trigger("keydown.enter");

    expect(window.location.href).toContain("/custom/path");
    expect(window.location.href).toContain("page=3");
  });

  test("handles model with page already set", () => {
    const model = createModel({ count: 100 });
    model.filter.page = 3;

    const wrapper = mount(SongFooter, {
      props: { model },
      global: { stubs: stubComponents },
    });
    const input = getInput(wrapper);

    expect((input.element as HTMLInputElement).value).toBe("3");
  });

  test("page count is at least 1 even with 0 songs", () => {
    const wrapper = mountFooter(createModel({ count: 0 }));
    expect(wrapper.text()).toContain("of 1");
  });
});
