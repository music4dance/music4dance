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
import IconButton from "./IconButton.vue";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: { IconButton } })
export default class LikeButton extends Vue {
  @Prop() private readonly state?: boolean;
  @Prop() private readonly authenticated!: boolean;
  @Prop() private readonly title!: string;
  @Prop() private readonly scale!: number;
  @Prop() private readonly toggleBehavior?: boolean;

  private get undefinedTip(): string {
    return `Click to add ${this.title} to favorites.`;
  }

  private get trueTip(): string {
    return (
      `${this.title} is in your favorites, click to ` +
      (this.toggleBehavior ? "move to your blocked list." : "change.")
    );
  }

  private get falseTip(): string {
    return (
      `${this.title} is in your blocked list, click to ` +
      (this.toggleBehavior ? "remove it." : "change.")
    );
  }

  private get redirectUrl(): string {
    const location = window.location;
    return `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`;
  }
}
</script>
