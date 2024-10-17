<script setup lang="ts">
import { useDropTarget } from "@/composables/useDropTarget";
import axios from "axios";
import { ref, watch } from "vue";
import { type Size } from "bootstrap-vue-next";

interface Suggestion {
  value: string;
  data: string;
}

interface SuggestionEntry {
  query: string;
  suggestions: Suggestion[];
}

const { checkServiceAndWarn } = useDropTarget();

const props = withDefaults(
  defineProps<{
    id: string;
    hideSuggestions?: boolean;
    hideButton?: boolean;
    placeholder?: string;
    label?: string;
    size?: Size;
    autofocus?: boolean;
  }>(),
  {
    size: "md",
    placeholder: "Search...",
    label: undefined,
    hideSuggestions: false,
    hideButton: false,
  },
);

const model = defineModel<string>();

const emit = defineEmits<{
  search: [value?: string];
}>();

const suggestions = ref<string[]>([]);

const listId = `${props.id}-suggestions`;

watch(
  () => model.value,
  (s?: string) => {
    if (!s || s.length < 2) {
      return;
    }
    axios
      .get(`/api/suggestion/${s}`)
      .then((response) => {
        const list = response.data as SuggestionEntry;
        suggestions.value = list.suggestions.map((s) => s.value);
      })
      .catch((error) => {
        // eslint-disable-next-line no-console
        console.log(error);
      });
  },
);

</script>

<template>
  <label v-if="label" class="visually-hidden" :for="id">{{ label }}</label>
  <BFormInput
    :id="id"
    v-model="model"
    :size="size"
    :list="hideSuggestions ? undefined : listId"
    :autocomplete="hideSuggestions ? 'on' : 'off'"
    :placeholder="placeholder"
    debounce="100"
    :autofocus="autofocus"
    @keyup.enter="() => emit('search', model)"
    @input="checkServiceAndWarn($event.target.value)"
  />
  <datalist :id="listId">
    <option v-for="suggestion in suggestions" :key="suggestion">
      {{ suggestion }}
    </option>
  </datalist>
  <BButton
    v-if="!hideButton"
    variant="outline-secondary"
    :size="size"
    @click="() => emit('search', model)"
  >
    <IBiSearch />
  </BButton>
</template>
