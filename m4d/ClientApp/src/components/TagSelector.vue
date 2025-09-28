<script setup lang="ts">
import { type ListOption } from "@/models/ListOption";
import type { ColorVariant } from "bootstrap-vue-next";
import { computed, ref } from "vue";
import { useFocusTrap } from "@vueuse/integrations/useFocusTrap";

const dropdown = ref<HTMLElement | null>(null);
const searchInput = ref<HTMLElement | null>(null);
const { activate, deactivate } = useFocusTrap(dropdown, { initialFocus: setInputFocus });

const model = defineModel<string[]>();
const tagChars = /[^\p{L}\d()'&/ ]/gmu;
const props = defineProps<{
  options: ListOption[];
  searchLabel: string;
  chooseLabel: string;
  emptyLabel: string;
  variant: string;
  showInitialList?: boolean;
  addCategories?: string[];
}>();

const search = ref("");

const addableOptions = computed(() => {
  const categories = props.addCategories;
  const s = search.value.trim();
  return categories
    ? categories.map((cat) => ({
        text: `+${s}`,
        value: `${s}:${cat}`,
      }))
    : [];
});

const criteria = computed(() => {
  return search.value.trim().toLowerCase();
});

const availableOptions = computed(() => {
  const prefix = `${criteria.value}:`;
  const exactMatches = criteria.value
    ? filterSelected(props.options.filter((opt) => opt.value.toLowerCase().startsWith(prefix)))
    : [];
  const filteredOptions = filterSelected(
    addableOptions.value.filter(
      (opt) => !exactMatches.find((m) => m.value.toLocaleLowerCase() === opt.value.toLowerCase()),
    ),
  );

  let options = filterSelected(props.options);
  if (criteria.value) {
    // Show only options that match criteria
    options = options.filter((opt) => opt.text.toLowerCase().indexOf(criteria.value) > -1);
  }

  options = filterOptions(options, [...exactMatches, ...filteredOptions]);

  const prefixOptions = buildPrefixOptions(options);
  const remainingOptions = buildRemainingOptions(options);
  // Show all options available

  return [...exactMatches, ...filteredOptions, ...prefixOptions, ...remainingOptions];
});

const mapText = computed(() => {
  return new Map(props.options.map((opt) => [opt.value, opt.text]));
});

const searchDesc = computed(() => {
  if (criteria.value && availableOptions.value.length === 0) {
    return "There are no tags matching your search criteria";
  }
  return "";
});

const showList = computed(() => {
  return props.showInitialList || !!criteria.value;
});

const safeVariant = computed(() => {
  return (props.variant ? props.variant.toLowerCase() : "primary") as ColorVariant;
});

function buildPrefixOptions(options: ListOption[]): ListOption[] {
  return options
    .filter((opt) => opt.value.toLowerCase().startsWith(criteria.value))
    .sort((a, b) => a.value.localeCompare(b.value));
}

function buildRemainingOptions(options: ListOption[]): ListOption[] {
  return options
    .filter((opt) => !opt.value.toLowerCase().startsWith(criteria.value))
    .sort((a, b) => a.value.localeCompare(b.value));
}

function filterOptions(options: ListOption[], filter: ListOption[]): ListOption[] {
  const filterValues = filter.map((flt) => flt.value.toLowerCase());
  return filterValues.length > 0
    ? options.filter((opt) => !filterValues.find((flt) => flt === opt.value.toLowerCase()))
    : options;
}

function filterSelected(options: ListOption[]): ListOption[] {
  const value = model.value ? model.value : [];
  return options.filter((opt) => value.indexOf(opt.value) === -1);
}

function setInputFocus(): void {
  const input = searchInput.value as HTMLElement;
  input.focus();
}

function tagFormatter(tag: string): string {
  return tag.replace(tagChars, "");
}

function titleFromTag(tag: string): string {
  return mapText.value.get(tag)!;
}

function onEnter(addTag: (opt: string) => void): void {
  if (!criteria.value) {
    return;
  }
  if (availableOptions.value.length === 0) {
    return;
  }
  const option = availableOptions.value[0];
  if (option?.text.toLowerCase().startsWith(criteria.value)) {
    addTag(option.value);
    search.value = "";
  }
}

function onOptionClick(option: ListOption, addTag: (opt: string) => void): void {
  addTag(option.value);
  search.value = "";
}
</script>

<template>
  <BFormTags v-model="model" no-outer-focus add-on-change class="mb-2" style="border-style: hidden">
    <template #default="{ tags, disabled, addTag, removeTag }">
      <ul v-if="tags.length > 0" class="list-inline d-inline-block mb-2">
        <li v-for="tag in tags" :key="tag" class="list-inline-item" data-bs-theme="dark">
          <slot :tag="tag" :disabled="disabled" :remove-tag="removeTag">
            <BFormTag
              :title="titleFromTag(tag)"
              :disabled="disabled"
              :variant="safeVariant"
              @remove="removeTag(tag)"
              >{{ titleFromTag(tag) }}</BFormTag
            >
          </slot>
        </li>
      </ul>
      <BDropdown
        ref="dropdown"
        size="sm"
        variant="outline-secondary"
        block
        class="tag-dropdown"
        @shown="activate()"
        @hidden="deactivate()"
      >
        <template #button-content> <IBiTagFill /> {{ chooseLabel }} </template>
        <div>
          <BDropdownForm @submit.stop.prevent="() => {}">
            <BFormGroup
              label-for="tag-search-input"
              :label="searchLabel"
              label-cols-md="auto"
              class="mb-0"
              label-size="sm"
              :description="searchDesc"
              :disabled="disabled"
            >
              <BFormInput
                ref="searchInput"
                v-model="search"
                type="search"
                size="sm"
                autocomplete="off"
                :formatter="tagFormatter"
                trim
                @keyup.enter.prevent.stop="onEnter(addTag)"
              />
            </BFormGroup>
          </BDropdownForm>
          <BDropdownDivider />
          <BDropdownText v-if="availableOptions.length === 0">
            {{ emptyLabel }}
          </BDropdownText>
          <BDropdownText v-if="!showList">
            Start typing to see a list of matching tags
          </BDropdownText>
          <template v-else>
            <div>
              <template v-for="option in availableOptions">
                <slot
                  name="option"
                  :option="option"
                  :add-tag="addTag"
                  :on-option-click="onOptionClick"
                >
                  <BDropdownItemButton
                    :key="option.value"
                    ref="option"
                    @click="onOptionClick(option, addTag)"
                  >
                    {{ option.text }}
                  </BDropdownItemButton>
                </slot>
              </template>
            </div>
          </template>
        </div>
      </BDropdown>
    </template>
  </BFormTags>
</template>
