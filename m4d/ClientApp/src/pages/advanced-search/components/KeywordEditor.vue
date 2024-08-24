<script setup lang="ts">
import { useDropTarget } from "@/composables/useDropTarget";
import { KeywordQuery } from "@/models/KeywordQuery";
import type { BFormInput } from "bootstrap-vue-next";
import { computed } from "vue";

const props = defineProps({ modelValue: { type: String, required: true } });
const advanced = defineModel<boolean>("advanced");
const emit = defineEmits(["update:modelValue"]);

const { checkServiceAndWarn } = useDropTarget();

const keywordQuery = computed(() => new KeywordQuery(props.modelValue));

const everywhere = computed(() => keywordQuery.value.getField("Everywhere"));
const title = computed(() => keywordQuery.value.getField("Title"));
const artist = computed(() => keywordQuery.value.getField("Artist"));
const albums = computed(() => keywordQuery.value.getField("Albums"));

const model = [
  {
    name: "Everywhere",
    model: everywhere,
    description: "Enter artist, title, etc. OR a Spotify or Apple share link",
  },
  { name: "Title", model: title, description: "Enter part of a title" },
  { name: "Artist", model: artist, description: "Enter part of an artist's name" },
  { name: "Albums", model: albums, description: "Enter part of an album name" },
];

const showFields = computed(() => keywordQuery.value.isLucene || advanced.value);
const visibleModel = computed(() => (showFields.value ? model : model.slice(0, 1)));

const updateModel = async (key: string, value: string) => {
  await checkServiceAndWarn(value);
  emit("update:modelValue", new KeywordQuery(props.modelValue).update(key, value).query);
};
</script>

<template>
  <BFormGroup id="search-string-group" label="Keywords:" label-for="search-string" class="mb-3">
    <BInputGroup
      v-for="(item, index) in visibleModel"
      :key="item.name"
      :append="showFields ? item.name : undefined"
      class="mb-1"
    >
      <BFormInput
        :id="`search-${item.name.toLowerCase()}`"
        :model-value="item.model.value"
        type="text"
        :placeholder="item.description"
        :autofocus="index === 0"
        debounce="100"
        :autocomplete="index === 0 ? 'off' : 'false'"
        :list="index === 0 ? 'auto-complete' : undefined"
        @update:model-value="updateModel(item.name, $event as string)"
      />
      <SuggestionList v-if="index === 0" id="auto-complete" :search="everywhere" />
    </BInputGroup>
    <BButton
      v-if="!showFields"
      squared
      size="sm"
      variant="outline-secondary"
      class="float-end"
      @click="advanced = true"
      ><IBiCaretDown /> more <IBiCaretDown
    /></BButton>
  </BFormGroup>
</template>
