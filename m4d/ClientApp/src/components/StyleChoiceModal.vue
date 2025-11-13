<script setup lang="ts">
import { ref } from "vue";

interface Props {
  /**
   * Available style families for selection (e.g., ["International", "American", "Country"])
   */
  styles: string[];
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
  "style-selected": [style: string];
}>();

const selectedStyle = ref<string | undefined>(undefined);

const selectAndClose = (): void => {
  if (selectedStyle.value) {
    emit("style-selected", selectedStyle.value);
  }
};

const onShow = (): void => {
  console.log("StyleChoiceModal shown for:", props.danceName, "styles:", props.styles);
  // Reset selection when modal opens
  selectedStyle.value = undefined;
};
</script>

<template>
  <BModal
    :id="props.id"
    header-bg-variant="primary"
    header-text-variant="light"
    title="Choose Dance Style"
    ok-title="Vote"
    :ok-disabled="!selectedStyle"
    @ok="selectAndClose"
    @show="onShow"
  >
    <p>
      <strong>{{ props.danceName }}</strong> is performed in multiple styles. Please select which
      style you'd like to vote for:
    </p>
    <StyleSelector v-model="selectedStyle" :styles="props.styles" size="md" />
  </BModal>
</template>
