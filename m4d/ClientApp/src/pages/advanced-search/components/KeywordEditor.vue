<script setup lang="ts">
import { checkServiceAndWarn } from "@/helpers/DropTarget";
import { KeywordQuery } from "@/models/KeywordQuery";
import { computed, onMounted, ref, type VNodeRef } from "vue";

const props = defineProps({ modelValue: { type: String, required: true } });
const emit = defineEmits(["update:modelValue"]);

const keywordsInput = ref<VNodeRef | null>(null);

const keywordQuery = new KeywordQuery(props.modelValue);

const everywhere = computed(() => keywordQuery.getField("Everywhere"));
const title = computed(() => keywordQuery.getField("Title"));
const artist = computed(() => keywordQuery.getField("Artist"));
const albums = computed(() => keywordQuery.getField("Albums"));

const forceFields = ref(false);

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

const showFields = computed(() => keywordQuery.isLucene || forceFields.value);
const visibleModel = computed(() => (showFields.value ? model : model.slice(0, 1)));

onMounted(() => {
  if (keywordsInput?.value?.$el) {
    const first = keywordsInput.value.$el.getElementsByTagName("input")[0];
    first.focus();
  }
});

const updateModel = async (key: string, value: string) => {
  await checkServiceAndWarn(value);
  emit("update:modelValue", new KeywordQuery(props.modelValue).update(key, value).query);
};
</script>

<template>
  <BFormGroup
    id="search-string-group"
    ref="keywordsInput"
    label="Keywords:"
    label-for="search-string"
    class="mb-3"
  >
    <BInputGroup
      v-for="item in visibleModel"
      :key="item.name"
      :append="showFields ? item.name : undefined"
      class="mb-1"
    >
      <BFormInput
        :id="`search-${item.name.toLowerCase()}`"
        ref="keywordsInput"
        :model-value="item.model.value"
        type="text"
        :placeholder="item.description"
        @update:model-value="updateModel(item.name, $event as string)"
      ></BFormInput>
    </BInputGroup>
    <BButton
      v-if="!showFields"
      squared
      size="sm"
      variant="outline-secondary"
      class="float-end"
      @click="forceFields = true"
      ><IBiCaretDown /> more <IBiCaretDown
    /></BButton>
  </BFormGroup>
</template>
