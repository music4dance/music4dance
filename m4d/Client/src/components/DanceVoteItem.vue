<template>
  <div class="my-2">
    <dance-vote-button
      :vote="vote"
      :value="rating.weight"
      :authenticated="true"
      :maxVote="dance.maxWeight"
      :danceName="dance.name"
      @up-vote="upVote()"
      @down-vote="downVote()"
    >
    </dance-vote-button>
    <span class="ml-1">{{ dance.name }}</span>
  </div>
</template>

<script lang="ts">
import { DanceRatingVote, VoteDirection } from "@/DanceRatingDelta";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceRating } from "@/model/DanceRating";
import { DanceStats } from "@/model/DanceStats";
import { Component, Mixins, Prop } from "vue-property-decorator";
import DanceVoteButton from "./DanceVoteButton.vue";

@Component({ components: { DanceVoteButton } })
export default class DanceVoteItem extends Mixins(EnvironmentManager) {
  @Prop() private readonly vote!: boolean | null;
  @Prop() private readonly rating!: DanceRating;

  private get dance(): DanceStats {
    const id = this.rating.danceId;
    const d = this.environment.fromId(id);
    if (!d) {
      throw new Error(`Couldn't find dance ${id}`);
    }
    return d;
  }

  private upVote(): void {
    this.danceVote(VoteDirection.Up);
  }

  private downVote(): void {
    this.danceVote(VoteDirection.Down);
  }

  private danceVote(direction: VoteDirection): void {
    this.$emit(
      "dance-vote",
      new DanceRatingVote(this.rating.danceId, direction)
    );
  }
}
</script>
