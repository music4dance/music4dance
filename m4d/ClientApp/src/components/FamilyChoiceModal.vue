<script setup lang="ts">
import { ref } from "vue";

interface Props {
  /**
   * Available dance families for selection (e.g., ["International", "American", "Country"])
   */
  families: string[];
  /**
   * Name of the dance being voted on
   */
  danceName: string;
  /**
   * ID for the modal
   */
  id: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  "families-selected": [families: string[]];
}>();

const selectedFamilies = ref<string[]>([]);

const selectAndClose = (): void => {
  // Emit array of selected families (empty array means vote without family)
  emit("families-selected", [...selectedFamilies.value]);
};

const toggleFamily = (family: string): void => {
  const index = selectedFamilies.value.indexOf(family);
  if (index > -1) {
    selectedFamilies.value.splice(index, 1);
  } else {
    selectedFamilies.value.push(family);
  }
};

const isSelected = (family: string): boolean => {
  return selectedFamilies.value.includes(family);
};

const onShow = (): void => {
  console.log("FamilyChoiceModal shown for:", props.danceName, "families:", props.families);
  // Reset selection when modal opens
  selectedFamilies.value = [];
};
</script>

<template>
  <BModal
    :id="props.id"
    header-bg-variant="primary"
    header-text-variant="light"
    title="Choose Dance Family"
    ok-title="Vote"
    @ok="selectAndClose"
    @show="onShow"
  >
    <p>
      <strong>{{ props.danceName }}</strong> is performed in multiple families. Please select which
      family or families you'd like to vote for, or click Vote to vote without specifying a family:
    </p>
    <BButtonGroup vertical class="w-100">
      <BButton
        v-for="family in props.families"
        :key="family"
        :variant="isSelected(family) ? 'primary' : 'outline-primary'"
        @click="toggleFamily(family)"
      >
        {{ family }}
      </BButton>
    </BButtonGroup>
    <div v-if="selectedFamilies.length === 0" class="text-muted mt-3 small">
      No family selected - vote will apply to {{ props.danceName }} generally
    </div>
    <div v-else class="mt-3 small">Selected: {{ selectedFamilies.join(", ") }}</div>
  </BModal>
</template>
