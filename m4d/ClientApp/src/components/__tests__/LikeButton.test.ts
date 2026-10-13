import { describe, test, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { createBootstrap } from "bootstrap-vue-next";
import LikeButton from "../LikeButton.vue";

// SongLikeButton always binds :state, passing undefined for "no opinion" - pass it explicitly
// here too, since Vue casts an omitted boolean prop to false (the blocked-list state).
function mountButton(props: Record<string, unknown> = {}) {
  return mount(LikeButton, {
    props: {
      id: "like-button-s1",
      title: "My Song",
      authenticated: true,
      state: undefined,
      ...props,
    },
    global: { plugins: [createBootstrap()] },
  });
}

const label = (wrapper: ReturnType<typeof mountButton>) =>
  wrapper.find('a[role="button"]').attributes("aria-label");

describe("LikeButton.vue", () => {
  test("labels each like state", () => {
    expect(label(mountButton())).toBe("Click to add My Song to favorites.");
    expect(label(mountButton({ state: true }))).toBe(
      "My Song is in your favorites, click to move to your blocked list.",
    );
    expect(label(mountButton({ state: false }))).toBe(
      "My Song is in your blocked list, click to remove it.",
    );
    expect(label(mountButton({ authenticated: false }))).toBe("Log in to like/dislike this song.");
  });

  test("updates its label when the like state changes", async () => {
    const wrapper = mountButton();

    await wrapper.setProps({ state: true });

    expect(label(wrapper)).toBe(
      "My Song is in your favorites, click to move to your blocked list.",
    );
  });
});
