<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed } from "vue";

const danceDB = safeDanceDatabase();

const props = defineProps<{
  vote?: boolean;
  rating: DanceRating;
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const dance = computed(() => {
  const id = props.rating.danceId;
  const d = danceDB.fromId(id);
  if (!d) {
    throw new Error(`Couldn't find dance ${id}`);
  }
  return d;
});

const upVote = (): void => {
  danceVote(VoteDirection.Up);
};

const downVote = (): void => {
  danceVote(VoteDirection.Down);
};

const danceVote = (direction: VoteDirection): void => {
  emit("dance-vote", new DanceRatingVote(props.rating.danceId, direction));
};

const maxWeight = computed(() => safeDanceDatabase().getMaxWeight(dance.value.id));
</script>

<template>
  <div class="my-2">
    <DanceVoteButton
      :vote="vote"
      :value="rating.weight"
      :authenticated="true"
      :max-vote="maxWeight"
      :dance-name="dance.name"
      @up-vote="upVote()"
      @down-vote="downVote()"
    />
    <span class="ms-1">{{ dance.name }}</span>
  </div>
</template>
