<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, ref } from "vue";

const danceDB = safeDanceDatabase();

const props = defineProps<{
  vote?: boolean;
  rating: DanceRating;
  filterStyleTag?: string; // Style tag from search context
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const dance = computed(() => {
  const id = props.rating.danceId;
  const d = danceDB.fromId(id);
  if (!d) {
    console.log(`Couldn't find dance ${id}`);
  }
  return d;
});

const styleFamilies = computed(() => danceDB.getStyleFamilies(props.rating.danceId));
const hasSingleStyle = computed(() => styleFamilies.value.length === 1);
const hasMultipleStyles = computed(() => styleFamilies.value.length > 1);

// Auto-select style if: 1) only one style exists, or 2) filtered by style in search
const selectedStyle = ref<string | undefined>(
  hasSingleStyle.value ? styleFamilies.value[0] : props.filterStyleTag,
);

const upVote = (): void => {
  danceVote(VoteDirection.Up);
};

const downVote = (): void => {
  danceVote(VoteDirection.Down);
};

const danceVote = (direction: VoteDirection): void => {
  emit("dance-vote", new DanceRatingVote(props.rating.danceId, direction, selectedStyle.value));
};

const maxWeight = computed(() =>
  dance.value ? safeDanceDatabase().getMaxWeight(dance.value.id) : 0,
);
</script>

<template>
  <div v-if="dance" class="my-2 d-flex align-items-center gap-2">
    <DanceVoteButton
      :vote="vote"
      :value="rating.weight"
      :authenticated="true"
      :max-vote="maxWeight"
      :dance-name="dance.name"
      @up-vote="upVote()"
      @down-vote="downVote()"
    />
    <span class="me-2">{{ dance.name }}</span>
    <StyleSelector
      v-if="hasMultipleStyles"
      v-model="selectedStyle"
      :styles="styleFamilies"
      size="sm"
    />
  </div>
</template>
