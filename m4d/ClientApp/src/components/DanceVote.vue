<script setup lang="ts">
import { DanceRatingVote, VoteDirection } from "@/models/DanceRatingDelta";
import { DanceRating } from "@/models/DanceRating";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { computed, ref, useId } from "vue";

const danceDB = safeDanceDatabase();
const uniqueId = useId();

const props = defineProps<{
  vote?: boolean;
  danceRating: DanceRating;
  authenticated?: boolean;
  filterFamilyTag?: string; // Family tag from search context (e.g., "American", "International")
}>();

const emit = defineEmits<{
  "dance-vote": [vote: DanceRatingVote];
}>();

const showFamilyChoiceModal = ref<boolean>(false);

const danceId = computed(() => props.danceRating.danceId);
const dance = computed(() => danceDB.fromId(danceId.value)!);

const styleFamilies = computed(() => danceDB.getStyleFamilies(danceId.value));
const hasSingleStyle = computed(() => styleFamilies.value.length === 1);
const hasMultipleStyles = computed(() => styleFamilies.value.length > 1);

console.log("DanceVote initialized:", {
  danceId: danceId.value,
  filterFamilyTag: props.filterFamilyTag,
  styleFamilies: styleFamilies.value,
  hasSingleStyle: hasSingleStyle.value,
  hasMultipleStyles: hasMultipleStyles.value,
});

const pendingVote = ref<VoteDirection | undefined>(undefined);
const modalId = `family-choice-modal-${uniqueId}`;

const handleVote = (direction: VoteDirection): void => {
  console.log("Handling vote", direction);
  console.log("State:", {
    hasSingleStyle: hasSingleStyle.value,
    hasMultipleStyles: hasMultipleStyles.value,
    filterFamilyTag: props.filterFamilyTag,
    styleFamilies: styleFamilies.value,
    modalId: modalId,
  });
  // If single family or has filter family tag, vote immediately
  if (hasSingleStyle.value) {
    console.log("Single family detected:", styleFamilies.value[0]);
    const family = styleFamilies.value[0];
    if (family) emitVote(direction, [family]);
  } else if (props.filterFamilyTag) {
    console.log("Using filter family tag:", props.filterFamilyTag);
    emitVote(direction, [props.filterFamilyTag]);
  } else if (hasMultipleStyles.value) {
    console.log("Multiple families detected, showing family choice modal:", modalId);
    // Show modal to select families
    pendingVote.value = direction;
    showFamilyChoiceModal.value = true;
  } else {
    console.log("No family choice needed");
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
  console.log("Emitting dance-vote event:", { danceId: danceId.value, direction, familyTags });
  emit("dance-vote", new DanceRatingVote(danceId.value, direction, familyTags));
};

const upVote = () => handleVote(VoteDirection.Up);
const downVote = () => handleVote(VoteDirection.Down);
const maxWeight = computed(() => safeDanceDatabase().getMaxWeight(danceId.value));
</script>

<template>
  <div class="d-flex align-items-center gap-2">
    <DanceVoteButton
      :vote="vote"
      :value="danceRating.weight"
      :authenticated="!!authenticated"
      :dance-name="dance.name"
      :max-vote="maxWeight"
      @up-vote="upVote"
      @down-vote="downVote"
    />
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
