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

  private get undefinedTip(): string {
    return `Click to like/dislike ${this.title}.`;
  }

  private get trueTip(): string {
    return `You have liked ${this.title}, click to dislike.`;
  }

  private get falseTip(): string {
    return `You have disliked ${this.title}, click to reset.`;
  }

  private get redirectUrl(): string {
    const location = window.location;
    return `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`;
  }
}
</script>
