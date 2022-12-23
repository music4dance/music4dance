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
import { PropType } from "vue";

export default EnvironmentManager.extend({
  components: { DanceVoteButton },
  props: {
    song: { type: Object as PropType<Song>, required: true },
    danceRating: { type: Object as PropType<DanceRating>, required: true },
    authenticated: Boolean,
  },
  computed: {
    dance(): DanceStats {
      return this.environment.fromId(this.danceRating.danceId)!;
    },
    vote(): boolean | undefined {
      return this.song.danceVote(this.danceRating.danceId);
    },
  },
  methods: {
    async upVote(): Promise<void> {
      this.$emit(
        "dance-vote",
        new DanceRatingVote(this.danceRating.danceId, VoteDirection.Up)
      );
    },
    async downVote(): Promise<void> {
      this.$emit(
        "dance-vote",
        new DanceRatingVote(this.danceRating.danceId, VoteDirection.Down)
      );
    },
  },
});
</script>
