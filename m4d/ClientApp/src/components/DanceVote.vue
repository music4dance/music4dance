<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed } from "vue";

const danceDB = safeDanceDatabase();

const props = defineProps<{
  vote?: boolean;
  danceRating: DanceRating;
  authenticated?: boolean;
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const danceId = computed(() => props.danceRating.danceId);
const dance = computed(() => danceDB.fromId(danceId.value)!);

const upVote = () => emit("dance-vote", new DanceRatingVote(danceId.value, VoteDirection.Up));
const downVote = () => emit("dance-vote", new DanceRatingVote(danceId.value, VoteDirection.Down));
const maxWeight = computed(() => safeDanceDatabase().getMaxWeight(danceId.value));
</script>

<template>
  <DanceVoteButton
    :vote="vote"
    :value="danceRating.weight"
    :authenticated="!!authenticated"
    :dance-name="dance.name"
    :max-vote="maxWeight"
    @up-vote="upVote"
    @down-vote="downVote"
  />
</template>
