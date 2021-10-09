<template>
  <like-button
    :state="state"
    :authenticated="!!user"
    :title="this.song.title"
    :scale="scale"
    :toggleBehavior="toggleBehavior"
    @click-like="onClick"
  >
  </like-button>
</template>

<script lang="ts">
import LikeButton from "@/components/LikeButton.vue";
import { Song } from "@/model/Song";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: { LikeButton } })
export default class SongLikeButton extends Vue {
  @Prop() private readonly song!: Song;
  @Prop() private readonly user?: string;
  @Prop() private readonly scale!: number;
  @Prop() private readonly toggleBehavior?: boolean;

  private onClick(): void {
    if (this.user) {
      this.$emit("click-like", this.song.id);
    } else {
      window.location.href = `/identity/account/login?returnUrl=${this.redirect}`;
    }
  }

  private get state(): boolean | undefined {
    const user = this.user;
    if (!user) {
      return undefined;
    }
    const modified = this.song.getUserModified(user);
    return modified ? modified.like : undefined;
  }

  private get redirect(): string {
    const location = window.location;
    return `${location.pathname}${location.search}${location.hash}`;
  }
}
</script>
