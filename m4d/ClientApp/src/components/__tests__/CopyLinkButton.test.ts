import { describe, test, expect, beforeEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createBootstrap } from "bootstrap-vue-next";
import CopyLinkButton from "../CopyLinkButton.vue";

// useToast()'s create() requires a mounted <BApp> (the real page host wraps every page in one -
// see vite.config.ts's AutoEndpoints - but the lightweight `mount()` harness here doesn't); mock
// just that export so the confirmation/failure toast calls don't throw, while every other
// bootstrap-vue-next export (BButton included) stays real.
vi.mock("bootstrap-vue-next", async (importOriginal) => ({
  ...(await importOriginal<typeof import("bootstrap-vue-next")>()),
  useToast: () => ({ create: vi.fn() }),
}));

function mountButton(props: Record<string, unknown> = {}) {
  return mount(CopyLinkButton, {
    props,
    global: { plugins: [createBootstrap()] },
  });
}

describe("CopyLinkButton.vue", () => {
  let writeText: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    window.history.replaceState(null, "", "/Home/Tempi?styles=waltz");
    writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });
  });

  test("defaults to copying the current page's address", async () => {
    const wrapper = mountButton();

    await wrapper.find("button").trigger("click");

    expect(writeText).toHaveBeenCalledWith(`${window.location.origin}/Home/Tempi?styles=waltz`);
  });

  test("copies the `url` prop instead, when given", async () => {
    const wrapper = mountButton({ url: "/song/filtersearch?filter=v2-Advanced" });

    await wrapper.find("button").trigger("click");

    expect(writeText).toHaveBeenCalledWith("/song/filtersearch?filter=v2-Advanced");
  });

  test("renders the given label", () => {
    const wrapper = mountButton({ label: "Copy Link to This Search" });

    expect(wrapper.text()).toContain("Copy Link to This Search");
  });

  test("defaults to the 'Copy Link' label", () => {
    const wrapper = mountButton();

    expect(wrapper.text()).toContain("Copy Link");
  });

  test("doesn't throw when the clipboard write fails", async () => {
    writeText.mockRejectedValue(new Error("denied"));
    const wrapper = mountButton();

    await expect(wrapper.find("button").trigger("click")).resolves.not.toThrow();
  });
});
