<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, ref, useId } from "vue";

const danceDB = safeDanceDatabase();
const uniqueId = useId();

const props = defineProps<{
  vote?: boolean;
  rating: DanceRating;
  filterFamilyTag?: string; // Family tag from search context
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const showFamilyChoiceModal = ref<boolean>(false);

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

const pendingVote = ref<VoteDirection | undefined>(undefined);
const modalId = `family-choice-modal-${uniqueId}`;

const handleVote = (direction: VoteDirection): void => {
  console.log("DanceVoteItem handleVote:", {
    direction,
    danceId: props.rating.danceId,
    filterFamilyTag: props.filterFamilyTag,
    hasSingleStyle: hasSingleStyle.value,
    hasMultipleStyles: hasMultipleStyles.value,
    styleFamilies: styleFamilies.value,
    modalId: modalId,
  });

  // If single family or has filter family tag, vote immediately
  if (hasSingleStyle.value) {
    console.log("Single family path");
    const family = styleFamilies.value[0];
    if (family) emitVote(direction, [family]);
  } else if (props.filterFamilyTag) {
    console.log("Filter family tag path:", props.filterFamilyTag);
    emitVote(direction, [props.filterFamilyTag]);
  } else if (hasMultipleStyles.value) {
    console.log("Multiple families detected, showing family choice modal:", modalId);
    // Show modal to select families
    pendingVote.value = direction;
    showFamilyChoiceModal.value = true;
  } else {
    console.log("No family tag needed path");
    // No family tag needed
    emitVote(direction, undefined);
  }
};

const onFamiliesSelected = (families: string[]): void => {
  if (pendingVote.value !== undefined) {
    const direction = pendingVote.value;
    pendingVote.value = undefined;
    // Emit single vote with array of families (empty array means vote without family)
    emitVote(direction, families.length > 0 ? families : undefined);
  }
};

const emitVote = (direction: VoteDirection, familyTags?: string[]): void => {
  console.log("Emitting dance-vote event:", {
    danceId: props.rating.danceId,
    direction,
    familyTags,
  });
  emit("dance-vote", new DanceRatingVote(props.rating.danceId, direction, familyTags));
};

const upVote = (): void => {
  handleVote(VoteDirection.Up);
};

const downVote = (): void => {
  handleVote(VoteDirection.Down);
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
    <FamilyChoiceModal
      v-if="hasMultipleStyles"
      :id="modalId"
      v-model="showFamilyChoiceModal"
      :families="styleFamilies"
      :dance-name="dance.name"
      @families-selected="onFamiliesSelected"
    />
  </div>
</template>
