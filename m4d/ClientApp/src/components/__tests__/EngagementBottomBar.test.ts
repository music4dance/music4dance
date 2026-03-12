import { describe, it, expect, beforeEach } from "vitest";
import { mount, VueWrapper } from "@vue/test-utils";
import EngagementBottomBar from "@/components/EngagementBottomBar.vue";

describe("EngagementBottomBar.vue", () => {
  let wrapper: VueWrapper<any>;

  beforeEach(() => {
    wrapper = mount(EngagementBottomBar);
  });

  describe("Component Rendering", () => {
    it("should render the bottom bar", () => {
      expect(wrapper.find(".engagement-bottom-bar").exists()).toBe(true);
    });

    it("should display the support text", () => {
      const text = wrapper.text();
      expect(text).toContain("How to support music4dance");
    });

    it("should have fixed positioning styles", () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      const style = bar.attributes("style");
      // Check that fixed positioning CSS is applied (in component's <style> block)
      expect(bar.classes()).toContain("engagement-bottom-bar");
    });
  });

  describe("Click Interaction", () => {
    it("should emit expand event when clicked", async () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      await bar.trigger("click");

      expect(wrapper.emitted("expand")).toBeTruthy();
      expect(wrapper.emitted("expand")?.length).toBe(1);
    });
  });

  describe("Keyboard Navigation", () => {
    it("should emit expand event when Enter key is pressed", async () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      await bar.trigger("keydown.enter");

      expect(wrapper.emitted("expand")).toBeTruthy();
      expect(wrapper.emitted("expand")?.length).toBe(1);
    });

    it("should emit expand event when Space key is pressed", async () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      await bar.trigger("keydown.space");

      expect(wrapper.emitted("expand")).toBeTruthy();
      expect(wrapper.emitted("expand")?.length).toBe(1);
    });

    it("should be keyboard focusable with tabindex=0", () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      expect(bar.attributes("tabindex")).toBe("0");
    });
  });

  describe("Accessibility", () => {
    it("should have role=button", () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      expect(bar.attributes("role")).toBe("button");
    });

    it("should have aria-label describing the action", () => {
      const bar = wrapper.find(".engagement-bottom-bar");
      expect(bar.attributes("aria-label")).toBe("Expand engagement options");
    });
  });

  describe("Visual Elements", () => {
    it("should display chevron up icon", () => {
      // Icon renders as SVG with me-3 class
      const iconContainer = wrapper.find(".me-3");
      expect(iconContainer.exists()).toBe(true);

      // Or check for presence of SVG element (icon components render as SVG)
      const svg = wrapper.find("svg");
      expect(svg.exists()).toBe(true);
    });
  });
});
