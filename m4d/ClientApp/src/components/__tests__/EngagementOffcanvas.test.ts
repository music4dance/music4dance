import { describe, it, expect, beforeEach } from "vitest";
import { mount, VueWrapper } from "@vue/test-utils";
import EngagementOffcanvas from "@/components/EngagementOffcanvas.vue";
import type { EngagementConfig } from "@/models/EngagementConfig";
import type { EngagementLevel } from "@/composables/useEngagementOffcanvas";

describe("EngagementOffcanvas.vue", () => {
  let wrapper: VueWrapper<any>;

  const testConfig: EngagementConfig = {
    enabled: true,
    firstShowPageCount: 2,
    repeatInterval: 5,
    sessionDismissalTimeout: 30,
    messages: {
      level1: "<p>Create a <strong>free account</strong> to save your searches.</p>",
      level2: "<p><strong>Subscribe</strong> to help keep it running.</p>",
      level3: "<p><strong>Thank you</strong> for using music4dance!</p>",
      loggedInUpgrade: "<p>Upgrade to <strong>Premium membership</strong>.</p>",
    },
    premiumBenefits: {
      items: [
        "Ad-free experience",
        "Spotify playlist integration",
        "Bonus content access",
        "Priority email support",
      ],
      moreText: "...and more!",
      completeListUrl: "https://music4dance.blog/subscriptions/",
    },
    ctaUrls: {
      register: "/identity/account/register",
      login: "/identity/account/login",
      subscribe: "/home/contribute",
      features: "https://music4dance.blog/features/",
    },
  };

  const level1Data: EngagementLevel = {
    level: 1,
    message: testConfig.messages.level1,
  };

  // Stub BOffcanvas to render its content (default slot)
  const globalStubs = {
    BOffcanvas: {
      template: '<div class="b-offcanvas-stub"><slot /></div>',
      props: ["modelValue", "placement", "backdrop", "scroll", "noHeader"],
    },
    BButton: {
      template: '<button class="b-button-stub"><slot /></button>',
      props: ["variant", "href"],
    },
  };

  describe("Anonymous User Rendering", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
          isAuthenticated: false,
          config: testConfig,
        },
        global: {
          stubs: globalStubs,
        },
      });
    });

    it("should render the offcanvas", () => {
      expect(wrapper.exists()).toBe(true);
    });

    it("should display the clickable header with down arrow", () => {
      const header = wrapper.find(".engagement-offcanvas-header");
      expect(header.exists()).toBe(true);
      expect(header.text()).toContain("Exploring music4dance?");
    });

    it("should display free account benefits for anonymous users", () => {
      const benefits = wrapper.find(".free-account-benefits");
      expect(benefits.exists()).toBe(true);
      expect(benefits.text()).toContain("When you've signed up you can:");
    });

    it("should display all 6 benefit items with links", () => {
      const benefits = wrapper.findAll(".free-account-benefits li");
      expect(benefits.length).toBe(6);
      if (benefits[0]) expect(benefits[0].text()).toContain("Vote on dances");
      if (benefits[1]) expect(benefits[1].text()).toContain("Tag songs");
      if (benefits[5]) expect(benefits[5].text()).toContain("Purchase a premium subscription");
    });

    it("should render 3 CTAs for anonymous users", () => {
      // Look for actual <a> or <button> elements (stubbed BButton renders as button with class)
      const buttons = wrapper.findAll(
        "button.b-button-stub, a[href*='/identity'], a[href*='/home']",
      );
      expect(buttons.length).toBeGreaterThanOrEqual(3);
    });

    it("should not show premium benefits for anonymous users", () => {
      const premiumBenefits = wrapper.find(".premium-benefits");
      expect(premiumBenefits.exists()).toBe(false);
    });
  });

  describe("Logged-In User Rendering", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: {
            level: 1,
            message: testConfig.messages.loggedInUpgrade || "",
          },
          isAuthenticated: true,
          config: testConfig,
        },
        global: {
          stubs: globalStubs,
        },
      });
    });

    it("should display premium benefits for logged-in users", () => {
      const premiumBenefits = wrapper.find(".premium-benefits");
      expect(premiumBenefits.exists()).toBe(true);
      expect(premiumBenefits.text()).toContain("Upgrade to Premium Membership");
    });

    it("should not show free account benefits for logged-in users", () => {
      const freeBenefits = wrapper.find(".free-account-benefits");
      expect(freeBenefits.exists()).toBe(false);
    });

    it("should render premium benefit items", () => {
      const benefits = wrapper.findAll(".premium-benefits ul li");
      // Should have at least the configured items (may have +1 for moreText)
      expect(benefits.length).toBeGreaterThanOrEqual(testConfig.premiumBenefits?.items.length || 0);
    });
  });

  describe("Header Interaction", () => {
    beforeEach(() => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
          isAuthenticated: false,
          config: testConfig,
        },
        global: {
          stubs: globalStubs,
        },
      });
    });

    it("should emit collapse when header is clicked", async () => {
      const header = wrapper.find(".engagement-offcanvas-header");
      await header.trigger("click");

      expect(wrapper.emitted("collapse")).toBeTruthy();
    });

    it("should emit collapse on Enter key", async () => {
      const header = wrapper.find(".engagement-offcanvas-header");
      await header.trigger("keydown.enter");

      expect(wrapper.emitted("collapse")).toBeTruthy();
    });

    it("should have clickable styling on header", () => {
      const header = wrapper.find(".engagement-offcanvas-header");
      expect(header.attributes("style")).toContain("cursor: pointer");
      expect(header.attributes("role")).toBe("button");
    });
  });

  describe("Model Value Updates", () => {
    it("should emit update:modelValue when BOffcanvas hidden event fires", async () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: level1Data,
          isAuthenticated: false,
          config: testConfig,
        },
        global: {
          stubs: globalStubs,
        },
      });

      // Trigger collapse which updates internal isOpen ref
      const header = wrapper.find(".engagement-offcanvas-header");
      await header.trigger("click");

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")?.[0]).toEqual([false]);
    });
  });

  describe("Edge Cases", () => {
    it("should not crash when engagementData is null", () => {
      wrapper = mount(EngagementOffcanvas, {
        props: {
          modelValue: true,
          engagementData: null,
          isAuthenticated: false,
          config: testConfig,
        },
        global: {
          stubs: globalStubs,
        },
      });

      expect(wrapper.exists()).toBe(true);
    });
  });
});
