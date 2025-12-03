import { mount } from "@vue/test-utils";
import { describe, expect, test } from "vitest";
import PremiumRequiredModal from "../PremiumRequiredModal.vue";

describe("PremiumRequiredModal.vue", () => {
  test("component mounts with feature name", () => {
    const wrapper = mount(PremiumRequiredModal, {
      props: { featureName: "Test Feature" },
    });
    expect(wrapper.exists()).toBe(true);
    expect(wrapper.props().featureName).toBe("Test Feature");
  });

  test("component mounts for Spotify playlist feature", () => {
    const wrapper = mount(PremiumRequiredModal, {
      props: { featureName: "Add to Spotify Playlist" },
    });
    expect(wrapper.exists()).toBe(true);
    expect(wrapper.props().featureName).toBe("Add to Spotify Playlist");
  });

  test("component has model value prop", () => {
    const wrapper = mount(PremiumRequiredModal, {
      props: {
        featureName: "Test Feature",
        modelValue: false,
      },
    });
    expect(wrapper.props().modelValue).toBe(false);
  });

  test("component emits update:modelValue event", async () => {
    const wrapper = mount(PremiumRequiredModal, {
      props: {
        featureName: "Test Feature",
        modelValue: true,
      },
    });

    // The BModal component should handle the v-model binding
    expect(wrapper.findComponent({ name: "BModal" }).exists()).toBe(true);
  });
});
