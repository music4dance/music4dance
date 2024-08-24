<script setup lang="ts">
import { DanceLink } from "@/models/DanceLink";

defineProps<{ editing: boolean }>();
const model = defineModel<DanceLink>({ required: true });
const emit = defineEmits<{
  delete: [link: string];
}>();
const deleteLink = (): void => {
  emit("delete", model.value.id);
};
</script>

<template>
  <BRow v-if="editing">
    <BCol sm="auto">
      <BCloseButton @click="deleteLink()" />
    </BCol>
    <BCol sm="2">
      <BFormInput v-model="model.description" />
    </BCol>
    <BCol sm="9">
      <BFormInput v-model="model.link" />
    </BCol>
  </BRow>
  <div v-else>
    <b>{{ model.description }}: </b><span />
    <a :href="model.link" target="_blank">{{ model.link }} <IBiArrowUpRight /></a>
  </div>
</template>
