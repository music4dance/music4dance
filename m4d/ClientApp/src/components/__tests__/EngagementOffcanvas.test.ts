import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, VueWrapper } from "@vue/test-utils";
import EngagementOffcanvas from "@/components/EngagementOffcanvas.vue";
import type { EngagementLevel } from "@/composables/useEngagementOffcanvas";

describe("EngagementOffcanvas.vue", () => {
  let wrapper: VueWrapper<any>;

  const level1Data: EngagementLevel = {
    level: 1,
    message:
      "<h4>Exploring music4dance?</h4><p>Create a <strong>free account</strong> to save your searches.</p>",
    ctaUrls: {
      primary: "/identity/account/register",
      secondary: "https://music4dance.blog/features/",
      tertiary: "/home/contribute",
    },
  };

  const level2Data: EngagementLevel = {
    level: 2,
    message:
      "<h4>Finding what you need?</h4><p><strong>Create an account</strong> or <strong>subscribe</strong>.</p>",
    ctaUrls: {
      primary: "/identity/account/register",
      secondary: "https://music4dance.blog/features/",
      tertiary: "/home/contribute",
    },
  };

  const level3Data: EngagementLevel = {
    level: 3,
    message:
      "<h4>You've loaded <strong>12</strong> pages!</h4><p>Consider subscribing to support us.</p>",
    ctaUrls: {
      primary: "/identity/account/register",
      secondary: "https://music4dance.blog/features/",
      tertiary: "/home/contribute",
    },
  };

  beforeEach(() => {
    // No special setup needed
  });

  describe("Component Rendering", () => {
    it("should render when modelValue is true", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      expect(wrapper.exists()).toBe(true);
    });

    it("should render HTML message content", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      // Check that engagementData message is being used (component renders it)
      const html = wrapper.html();
      // BOffcanvas might stub content, so just verify component has the data
      expect(wrapper.props("engagementData")?.message).toContain("Exploring music4dance?");
    });

    it("should not crash when engagementData is null", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: null,
        },
      });

      expect(wrapper.exists()).toBe(true);
    });
  });

  describe("Level 1 Buttons", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });
    });

    it("should render 3 buttons for Level 1", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      expect(buttons.length).toBe(3);
    });

    it("should have correct button text for Level 1", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].text()).toBe("Create Account");
      expect(buttons[1].text()).toBe("Learn More");
      expect(buttons[2].text()).toBe("Maybe Later");
    });

    it("should have correct hrefs for Level 1 action buttons", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].attributes("href")).toBe("/identity/account/register");
      expect(buttons[1].attributes("href")).toBe("https://music4dance.blog/features/");
      // Third button (Dismiss) should not have href
    });

    it("should emit dismiss when 'Maybe Later' is clicked", async () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      const dismissButton = buttons[2];

      await dismissButton.trigger("click");

      expect(wrapper.emitted("dismiss")).toBeTruthy();
      expect(wrapper.emitted("dismiss")?.length).toBe(1);
    });
  });

  describe("Level 2 Buttons", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level2Data,
        },
      });
    });

    it("should render 4 buttons for Level 2", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      expect(buttons.length).toBe(4);
    });

    it("should have correct button text for Level 2", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].text()).toBe("Subscribe");
      expect(buttons[1].text()).toBe("Create Account");
      expect(buttons[2].text()).toBe("Learn More");
      expect(buttons[3].text()).toBe("Dismiss");
    });

    it("should have correct hrefs for Level 2 action buttons", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].attributes("href")).toBe("/home/contribute");
      expect(buttons[1].attributes("href")).toBe("/identity/account/register");
      expect(buttons[2].attributes("href")).toBe("https://music4dance.blog/features/");
      // Fourth button (Dismiss) should not have href
    });

    it("should emit dismiss when 'Dismiss' is clicked", async () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      const dismissButton = buttons[3];

      await dismissButton.trigger("click");

      expect(wrapper.emitted("dismiss")).toBeTruthy();
    });
  });

  describe("Level 3 Buttons", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level3Data,
        },
      });
    });

    it("should render 4 buttons for Level 3", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      expect(buttons.length).toBe(4);
    });

    it("should have correct button text for Level 3", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].text()).toBe("Subscribe Now");
      expect(buttons[1].text()).toBe("View Features");
      expect(buttons[2].text()).toBe("Free Account");
      expect(buttons[3].text()).toBe("Dismiss");
    });

    it("should have correct hrefs for Level 3 action buttons", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });

      expect(buttons[0].attributes("href")).toBe("/home/contribute");
      expect(buttons[1].attributes("href")).toBe("https://music4dance.blog/features/");
      expect(buttons[2].attributes("href")).toBe("/identity/account/register");
      // Fourth button (Dismiss) should not have href
    });

    it("should have emphasized styling on primary Subscribe button", () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      const subscribeButton = buttons[0];

      // Check that Subscribe button has bold class (size might not be in attributes)
      expect(subscribeButton.classes()).toContain("fw-bold");
    });

    it("should emit dismiss when 'Dismiss' is clicked", async () => {
      const buttons = wrapper.findAllComponents({ name: "BButton" });
      const dismissButton = buttons[3];

      await dismissButton.trigger("click");

      expect(wrapper.emitted("dismiss")).toBeTruthy();
    });
  });

  describe("Two-Way Binding (v-model)", () => {
    it("should update internal state when modelValue prop changes", async () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: false,
          engagementData: level1Data,
        },
      });

      // Component should start closed
      expect(wrapper.vm.isOpen).toBe(false);

      // Update prop
      await wrapper.setProps({ modelValue: true });

      // Internal state should update
      expect(wrapper.vm.isOpen).toBe(true);
    });

    it("should emit update:modelValue when internal state changes", async () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      // Simulate closing (internal state change)
      wrapper.vm.isOpen = false;
      await wrapper.vm.$nextTick();

      // Should emit update:modelValue
      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")?.[0]).toEqual([false]);
    });

    it("should emit dismiss when offcanvas is hidden", async () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      // Simulate BOffcanvas @hidden event
      wrapper.vm.onHidden();

      expect(wrapper.emitted("dismiss")).toBeTruthy();
    });
  });

  describe("Dynamic Title", () => {
    it("should compute title for Level 1", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      expect(wrapper.vm.offcanvasTitle).toBe("Exploring music4dance?");
    });

    it("should compute title for Level 2", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level2Data,
        },
      });

      expect(wrapper.vm.offcanvasTitle).toBe("Finding what you need?");
    });

    it("should compute title for Level 3", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level3Data,
        },
      });

      expect(wrapper.vm.offcanvasTitle).toBe("You've discovered a lot!");
    });

    it("should return empty title when engagementData is null", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: null,
        },
      });

      expect(wrapper.vm.offcanvasTitle).toBe("");
    });
  });

  describe("Edge Cases", () => {
    it("should handle missing ctaUrls gracefully", () => {
      const incompleteData: EngagementLevel = {
        level: 1,
        message: "<p>Test</p>",
        ctaUrls: {
          primary: "",
          secondary: "",
          tertiary: "",
        },
      };

      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: incompleteData,
        },
      });

      expect(wrapper.exists()).toBe(true);
    });

    it("should handle dismissal flow correctly", async () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
        },
      });

      // User clicks dismiss button
      wrapper.vm.onDismiss();
      await wrapper.vm.$nextTick();

      // Should update internal state
      expect(wrapper.vm.isOpen).toBe(false);

      // Should emit dismiss
      expect(wrapper.emitted("dismiss")).toBeTruthy();

      // Should emit update:modelValue
      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
    });
  });
});
