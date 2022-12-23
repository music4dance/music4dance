<template>
  <icon-button
    :state="state"
    :authenticated="authenticated"
    :redirectUrl="redirectUrl"
    signInTip="Log in to like/dislike this song."
    :undefinedTip="undefinedTip"
    :trueTip="trueTip"
    :falseTip="falseTip"
    trueIcon="heart-fill"
    falseIcon="heart"
    trueVariant="danger"
    falseVariant="secondary"
    :scale="scale"
    @click-icon="$emit('click-like')"
  >
  </icon-button>
</template>

<script lang="ts">
import Vue from "vue";
import IconButton from "./IconButton.vue";

export default Vue.extend({
  components: { IconButton },
  props: {
    state: Boolean,
    authenticated: Boolean,
    title: String,
    scale: Number,
    toggleBehavior: Boolean,
  },

  computed: {
    undefinedTip(): string {
      return `Click to add ${this.title} to favorites.`;
    },

    trueTip(): string {
      return (
        `${this.title} is in your favorites, click to ` +
        (this.toggleBehavior ? "move to your blocked list." : "change.")
      );
    },

    falseTip(): string {
      return (
        `${this.title} is in your blocked list, click to ` +
        (this.toggleBehavior ? "remove it." : "change.")
      );
    },

    redirectUrl(): string {
      const location = window.location;
      return `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`;
    },
  },
});
</script>
