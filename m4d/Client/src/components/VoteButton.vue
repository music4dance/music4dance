<template>
  <div class="vote-container">
    <div
      :class="upClass"
      v-b-tooltip.hover.right="upTip"
      @click="upClick"
    ></div>
    <div class="vote-number" v-b-tooltip.hover.right="numberTip">
      {{ danceRating.weight }}
    </div>
    <div
      :class="downClass"
      v-b-tooltip.hover.right="downTip"
      @click="downClick"
    ></div>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import axios from "axios";
import { DanceRating, Song } from "@/model/Song";
import { DanceStats } from "@/model/DanceStats";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { TagList } from "@/model/TagList";
import { Tag } from "@/model/Tag";

declare const environment: DanceEnvironment;

@Component
export default class VoteButton extends Vue {
  @Prop() private readonly song!: Song;
  @Prop() private readonly danceRating!: DanceRating;
  @Prop() private readonly authenticated!: boolean;

  private get upClass(): string[] {
    return this.vote ? ["voted-up"] : ["vote-up"];
  }

  private get downClass(): string[] {
    return this.vote === false ? ["voted-down"] : ["vote-down"];
  }

  private get upTip(): string {
    return this.authenticated
      ? this.vote
        ? `You have voted that this song is danceable to ${this.danceName}`
        : `Click here to vote for this song being danceable ${this.danceName}`
      : "Sign in to vote";
  }

  private get downTip(): string {
    return this.authenticated
      ? this.vote === false
        ? `You have voted that this song is not danceable to ${this.danceName}`
        : `Click here to vote that this song is not danceable to ${this.danceName}`
      : "Sign in to vote";
  }

  private get numberTip(): string {
    return (
      `This song has ${this.danceRating.weight} votes.` +
      `The most popular ${this.danceName} has ${this.maxVote} votes.`
    );
  }

  private get danceName(): string {
    return this.dance.danceName;
  }

  private get dance(): DanceStats {
    return environment.fromId(this.danceRating.danceId)!;
  }

  private get vote(): boolean | undefined {
    return TagList.build(this.song!.currentUserTags).voteFromTags(
      this.danceRating.positiveTag
    );
  }

  private get maxVote(): number {
    return this.dance.maxWeight;
  }

  private async upClick(): Promise<void> {
    if (this.authenticated) {
      if (this.vote === true) {
        this.danceRating.weight -= 1;
        this.removeVote(this.danceRating.positiveTag);
        this.submitVote();
      } else if (this.vote === false) {
        this.danceRating.weight += 2;
        this.removeVote(this.danceRating.negativeTag);
        this.addVote(this.danceRating.positiveTag);
        this.submitVote(true);
      } else {
        this.danceRating.weight += 1;
        this.addVote(this.danceRating.positiveTag);
        this.submitVote(true);
      }
    } else {
      this.login();
    }
  }

  private async downClick(): Promise<void> {
    if (this.authenticated) {
      if (this.vote === true) {
        this.danceRating.weight -= 2;
        this.removeVote(this.danceRating.positiveTag);
        this.addVote(this.danceRating.negativeTag);
        this.submitVote(false);
      } else if (this.vote === false) {
        this.danceRating.weight += 1;
        this.removeVote(this.danceRating.negativeTag);
        this.submitVote();
      } else {
        this.danceRating.weight -= 1;
        this.addVote(this.danceRating.negativeTag);
        this.submitVote(false);
      }
    } else {
      this.login();
    }
  }

  private removeVote(tag: Tag): void {
    this.song.currentUserTags = TagList.build(this.song.currentUserTags).remove(
      tag
    ).tags;
  }

  private addVote(tag: Tag): void {
    this.song.currentUserTags.push(tag);
  }

  private login(): void {
    window.location.href = `/identity/account/login?returnUrl=${this.redirect}`;
  }

  private get redirect(): string {
    const location = window.location;
    return `${location.pathname}${location.search}${location.hash}`;
  }

  private async submitVote(like?: boolean): Promise<void> {
    try {
      //const response =
      await axios.put(`/api/like/${this.song.songId}`, {
        like,
        dance: this.danceRating.danceId,
      });
      // const data = response.data;
    } catch (e) {
      // tslint:disable-next-line:no-console
      console.log(e);
      throw e;
    }
  }
}
</script>

<style lang="scss" scoped>
.vote-container {
  display: inline-block;
}

.voted-up {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-bottom: 0.75em solid black;
}

.voted-down {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-top: 0.75em solid black;
}

.vote-up {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-bottom: 0.75em solid gray;
}

.vote-down {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-top: 0.75em solid gray;
}

.vote-number {
  text-align: center;
}
</style>
