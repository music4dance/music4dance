<template>
  <a
    href="#"
    @click.prevent="onClick"
    v-b-tooltip.hover.right="likeTip"
    role="button"
  >
    <b-icon-heart-fill
      variant="danger"
      v-if="state"
      :font-scale="scale"
    ></b-icon-heart-fill>
    <b-iconstack v-else-if="state === false" :font-scale="scale">
      <b-icon
        stacked
        icon="heart-fill"
        variant="secondary"
        scale="0.75"
        shift-v="-1"
      ></b-icon>
      <b-icon stacked icon="x-circle" variant="danger"></b-icon>
    </b-iconstack>
    <b-icon-heart variant="secondary" :font-scale="scale" v-else></b-icon-heart>
  </a>
</template>

<script lang="ts">
import "reflect-metadata";
import axios from "axios";
import { Component, Prop, Vue } from "vue-property-decorator";
import { Song, ModifiedRecord } from "@/model/Song";

interface LikeModel {
  dance?: string;
  like?: boolean;
}

@Component
export default class LikeButton extends Vue {
  @Prop() private readonly song!: Song;
  @Prop() private readonly userName!: string;
  @Prop() private readonly scale!: string;

  private get state(): boolean | undefined {
    const modified = this.userModified;
    return modified ? modified.like : undefined;
  }

  private get userModified(): ModifiedRecord | undefined {
    return this.song.getUserModified(this.userName);
  }

  private get nextState(): boolean | undefined {
    return this.rotateLike(this.state);
  }

  private get likeTip(): string {
    if (!this.userName) {
      return "Log in to like/dislike this song.";
    }

    const modified = this.userModified;
    const title = this.song.title;
    if (!modified || modified.like === undefined) {
      return `Click to like/dislike ${title}.`;
    } else if (modified.like) {
      return `You have liked ${title}, click to dislike.`;
    } else {
      return `You have disliked ${title}, click to reset.`;
    }
  }

  private setNextState(): void {
    let modified = this.userModified;
    if (modified) {
      modified.like = this.rotateLike(modified.like);
    } else {
      modified = new ModifiedRecord({ userName: this.userName, like: true });
      this.song.modifiedBy.push(modified);
    }
  }

  private async onClick(): Promise<void> {
    if (this.userName) {
      await this.clickLike();
    } else {
      window.location.href = `/identity/account/login?returnUrl=${this.redirect}`;
    }
  }

  private async clickLike(): Promise<void> {
    try {
      const newState = this.nextState;
      await axios.put(`/api/like/${this.song.songId}`, {
        like: newState,
      });
      this.setNextState();
    } catch (e) {
      // tslint:disable-next-line:no-console
      console.log(e);
      throw e;
    }
  }

  private get redirect(): string {
    const location = window.location;
    return `${location.pathname}${location.search}${location.hash}`;
  }

  private rotateLike(like?: boolean): boolean | undefined {
    switch (like) {
      case true:
        return false;
      case false:
        return undefined;
      default:
        return true;
    }
  }
}
</script>
