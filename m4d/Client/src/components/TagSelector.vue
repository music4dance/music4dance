<template>
  <b-tags
    v-model="selectedInternal"
    no-outer-focus
    add-on-change
    class="mb-2"
    style="border-style: hidden"
  >
    <template v-slot="{ tags, disabled, addTag, removeTag }">
      <ul v-if="tags.length > 0" class="list-inline d-inline-block mb-2">
        <li v-for="tag in tags" :key="tag" class="list-inline-item">
          <slot :tag="tag" :disabled="disabled" :removeTag="removeTag">
            <b-form-tag
              @remove="removeTag(tag)"
              :title="titleFromTag(tag)"
              :disabled="disabled"
              :variant="variant"
              >{{ titleFromTag(tag) }}</b-form-tag
            >
          </slot>
        </li>
      </ul>
      <b-dropdown
        size="sm"
        variant="outline-secondary"
        block
        class="tag-dropdown"
        @shown="setInputFocus"
        ref="dropdown"
      >
        <template v-slot:button-content>
          <b-icon icon="tag-fill"></b-icon> {{ chooseLabel }}
        </template>
        <b-dropdown-form @submit.stop.prevent="() => {}">
          <b-form-group
            label-for="tag-search-input"
            :label="searchLabel"
            label-cols-md="auto"
            class="mb-0"
            label-size="sm"
            :description="searchDesc"
            :disabled="disabled"
          >
            <b-form-input
              v-model="search"
              type="search"
              size="sm"
              autocomplete="off"
              :formatter="tagFormatter"
              @keyup.enter.prevent.stop="onEnter(addTag)"
              @keyup.down="onDownArrow()"
              ref="searchInput"
              trim
            ></b-form-input>
          </b-form-group>
        </b-dropdown-form>
        <b-dropdown-divider></b-dropdown-divider>
        <b-dropdown-text v-if="availableOptions.length === 0">
          {{ emptyLabel }}
        </b-dropdown-text>
        <b-dropdown-text v-if="!showList">
          Start typing to see a list of matching tags
        </b-dropdown-text>
        <template v-else>
          <div ref="options">
            <template v-for="option in availableOptions">
              <slot
                name="option"
                :option="option"
                :addTag="addTag"
                :onOptionClick="onOptionClick"
              >
                <b-dropdown-item-button
                  :key="option.value"
                  @click="onOptionClick(option, addTag)"
                  ref="option"
                >
                  {{ option.text }}
                </b-dropdown-item-button>
              </slot>
            </template>
          </div>
        </template>
      </b-dropdown>
    </template>
  </b-tags>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Model, Vue } from "vue-property-decorator";
import { ListOption } from "@/model/ListOption";

@Component
export default class TagSelector extends Vue {
  @Model("input") private readonly selected!: string[];
  @Prop() private options!: ListOption[];
  @Prop() private searchLabel!: string;
  @Prop() private chooseLabel!: string;
  @Prop() private emptyLabel!: string;
  @Prop() private variant!: string;
  @Prop() private showInitialList?: boolean;
  @Prop() private readonly addCategories?: string[];

  private search: string;
  private mapText: Map<string, string>;

  constructor() {
    super();

    this.search = "";
    this.mapText = new Map(this.options.map((opt) => [opt.value, opt.text]));
  }

  private get selectedInternal(): string[] {
    return this.selected;
  }

  private set selectedInternal(selected: string[]) {
    this.$emit("input", selected);
  }

  private get showList(): boolean {
    return this.showInitialList || !!this.criteria;
  }

  private get criteria(): string {
    // Compute the search criteria
    return this.search.trim().toLowerCase();
  }

  private get availableOptions(): ListOption[] {
    const criteria = this.criteria;
    const prefix = `${criteria}:`;
    const exactMatches = this.criteria
      ? this.filterSelected(
          this.options.filter((opt) =>
            opt.value.toLowerCase().startsWith(prefix)
          )
        )
      : [];
    const addableOptions = this.filterSelected(
      this.addableOptions.filter(
        (opt) =>
          !exactMatches.find(
            (m) => m.value.toLocaleLowerCase() === opt.value.toLowerCase()
          )
      )
    );

    let options = this.filterSelected(this.options);
    if (criteria) {
      // Show only options that match criteria
      options = options.filter(
        (opt) => opt.text.toLowerCase().indexOf(criteria) > -1
      );
    }

    options = this.filterOptions(options, [...exactMatches, ...addableOptions]);

    const prefixOptions = this.buildPrefixOptions(options);
    const remainingOptions = this.buildRemainingOptions(options);
    // Show all options available

    return [
      ...exactMatches,
      ...addableOptions,
      ...prefixOptions,
      ...remainingOptions,
    ];
  }

  private filterSelected(options: ListOption[]): ListOption[] {
    return options.filter((opt) => this.selected.indexOf(opt.value) === -1);
  }

  private buildPrefixOptions(options: ListOption[]): ListOption[] {
    return options
      .filter((opt) => opt.value.toLowerCase().startsWith(this.criteria))
      .sort((a, b) => a.value.localeCompare(b.value));
  }

  private buildRemainingOptions(options: ListOption[]): ListOption[] {
    return options
      .filter((opt) => !opt.value.toLowerCase().startsWith(this.criteria))
      .sort((a, b) => a.value.localeCompare(b.value));
  }

  private filterOptions(
    options: ListOption[],
    filter: ListOption[]
  ): ListOption[] {
    const filterValues = filter.map((flt) => flt.value.toLowerCase());
    return filterValues.length > 0
      ? options.filter(
          (opt) => !filterValues.find((flt) => flt === opt.value.toLowerCase())
        )
      : options;
  }

  private get addableOptions(): ListOption[] {
    const categories = this.addCategories;
    const search = this.search.trim();
    return categories
      ? categories.map((cat) => ({
          text: `+${search}`,
          value: `${search}:${cat}`,
        }))
      : [];
  }

  private get searchDesc(): string {
    if (this.criteria && this.availableOptions.length === 0) {
      return "There are no tags matching your search criteria";
    }
    return "";
  }

  private onOptionClick(
    option: ListOption,
    addTag: (opt: string) => void
  ): void {
    addTag(option.value);
    this.search = "";
  }

  private onEnter(addTag: (opt: string) => void): void {
    const criteria = this.criteria;
    if (!criteria) {
      return;
    }
    const options = this.availableOptions;
    if (options.length === 0) {
      return;
    }
    const option = options[0];
    if (option.text.toLowerCase().startsWith(criteria)) {
      addTag(option.value);
      this.search = "";
    }
  }

  private async onDownArrow(): Promise<void> {
    const options = this.$refs.options as HTMLElement;
    const item = options.firstElementChild as HTMLElement;
    if (!item) {
      return;
    }
    const button = item.firstElementChild as HTMLElement;
    if (!button) {
      return;
    }
    await this.$nextTick();
    button.focus();
  }

  private async setInputFocus(): Promise<void> {
    await this.$nextTick();
    ((this.$refs.searchInput as Vue).$el as HTMLElement).focus();
  }

  private titleFromTag(tag: string): string {
    return this.mapText.get(tag)!;
  }

  private tagFormatter(tag: string): string {
    return tag.replace(/[^a-zA-Z0-9/\-'" ]/gm, "");
  }
}
</script>
