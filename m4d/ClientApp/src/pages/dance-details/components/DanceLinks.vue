<script setup lang="ts">
import { jsonCompare } from "@/helpers/ObjectHelpers";
import { DanceLink } from "@/models/DanceLink";
import { computed, ref } from "vue";

const props = defineProps<{ danceId: string; editing: boolean }>();
const model = defineModel<DanceLink[]>({ required: true });
const cloneLinks = (value: DanceLink[]) => value.map((l) => new DanceLink(l));
const initialLinks = ref(cloneLinks(model.value));
const onAdd = (): void => {
  model.value = [...cloneLinks(model.value), new DanceLink({ danceId: props.danceId })];
};
const onDelete = (id: string): void => {
  if (initialLinks.value) {
    model.value = cloneLinks(model.value).filter((l) => l.id !== id);
  }
};
const commit = (): void => {
  initialLinks.value = cloneLinks(model.value);
};
const isModified = computed(() => !jsonCompare(model.value, initialLinks.value));
defineExpose({ commit, isModified });
</script>

<template>
  <div id="references">
    <h2>References:</h2>
    <div v-for="(link, index) in model" :key="index">
      <EditableLink
        v-if="model[index]"
        v-model="model[index]"
        :editing="editing"
        @delete="onDelete($event)"
      />
    </div>
    <BButton v-if="editing" block variant="outline-primary" class="mt-2" @click="onAdd"
      >Add Reference</BButton
    >
  </div>
</template>
