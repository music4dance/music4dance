<template>
  <dance-vote-button
    :vote="vote"
    :value="danceRating.weight"
    :authenticated="authenticated"
    :danceName="dance.name"
    :maxVote="dance.maxWeight"
    @up-vote="upVote"
    @down-vote="downVote"
  >
  </dance-vote-button>
</template>

<script lang="ts">
import DanceVoteButton from "@/components/DanceVoteButton.vue";
import { DanceRatingVote, VoteDirection } from "@/DanceRatingDelta";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { DanceRating } from "@/model/DanceRating";
import { DanceStats } from "@/model/DanceStats";
import { Song } from "@/model/Song";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({ components: { DanceVoteButton } })
export default class DanceVote extends Mixins(EnvironmentManager) {
  @Prop() private readonly song!: Song;
  @Prop() private readonly danceRating!: DanceRating;
  @Prop() private readonly authenticated!: boolean;

  private get dance(): DanceStats {
    return this.environment.fromId(this.danceRating.danceId)!;
  }

  private get vote(): boolean | undefined {
    return this.song.danceVote(this.danceRating.danceId);
  }

  private async upVote(): Promise<void> {
    this.$emit(
      "dance-vote",
      new DanceRatingVote(this.danceRating.danceId, VoteDirection.Up)
    );
  }

  private async downVote(): Promise<void> {
    this.$emit(
      "dance-vote",
      new DanceRatingVote(this.danceRating.danceId, VoteDirection.Down)
    );
  }
}
</script>
