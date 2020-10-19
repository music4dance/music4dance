<template>
    <b-form-tags 
        v-model="selectedInternal" 
        no-outer-focus 
        class="mb-2"
        style="border-style: hidden">
        <template v-slot="{ tags, disabled, addTag, removeTag }">
            <ul v-if="tags.length > 0" class="list-inline d-inline-block mb-2">
                <li v-for="tag in tags" :key="tag" class="list-inline-item">
                    <slot :tag="tag" :disabled="disabled" :removeTag="removeTag">
                        <b-form-tag
                            @remove="removeTag(tag)"
                            :title="titleFromTag(tag)"
                            :disabled="disabled"
                            :variant="variant"
                        >{{ titleFromTag(tag) }}</b-form-tag>
                    </slot>
                </li>
            </ul>
            <b-dropdown 
                size="sm"
                variant="outline-secondary"
                block
                class="tag-dropdown">
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
                    :disabled="disabled">
                    <b-form-input
                    v-model="search"
                    id="tag-search-input"
                    type="search"
                    size="sm"
                    autocomplete="off"
                    ></b-form-input>
                </b-form-group>
                </b-dropdown-form>
                <b-dropdown-divider></b-dropdown-divider>
                <b-dropdown-text v-if="availableOptions.length === 0">
                    {{ emptyLabel }}
                </b-dropdown-text>
                <b-dropdown-text v-if="!criteria">
                    Start typing to see a list of matching tags
                </b-dropdown-text>
                <template v-else>
                    <template v-for="option in availableOptions">
                        <slot name="option" 
                            :option="option" 
                            :addTag="addTag"
                            :onOptionClick="onOptionClick">
                            <b-dropdown-item-button
                                :key="option.value"
                                @click="onOptionClick(option, addTag)">
                                {{ option.text }}
                            </b-dropdown-item-button>
                        </slot>
                    </template>
                </template>
            </b-dropdown>
        </template>
    </b-form-tags>
</template>

<script lang='ts'>
import 'reflect-metadata';
import { Component, Watch, Prop, Model, Vue } from 'vue-property-decorator';
import { ListOption, valuesFromOptions } from '@/model/ListOption';

@Component
export default class TagSelector extends Vue {
    @Model('change') private readonly selected!: string[];
    @Prop() private options!: ListOption[];
    @Prop() private searchLabel!: string;
    @Prop() private chooseLabel!: string;
    @Prop() private emptyLabel!: string;
    @Prop() private variant!: string;

    private selectedInternal: string[];
    private search: string;
    private mapText: Map<string, string>;

     constructor() {
        super();

        this.selectedInternal = this.selected;
        this.search = '';
        this.mapText = new Map(this.options.map((opt) => [opt.value, opt.text]));
    }

    private get criteria(): string {
        // Compute the search criteria
        return this.search.trim().toLowerCase();
    }

    private get availableOptions() {
        const criteria = this.criteria;
        // Filter out already selected options
        const options = this.options.filter((opt) => this.selected.indexOf(opt.value) === -1);
        if (criteria) {
          // Show only options that match criteria
          return options.filter((opt) => opt.text.toLowerCase().indexOf(criteria) > -1);
        }
        // Show all options available
        return options;
    }

    private get searchDesc(): string {
        if (this.criteria && this.availableOptions.length === 0) {
          return 'There are no tags matching your search criteria';
        }
        return '';
    }

    private onOptionClick(option: ListOption, addTag: (opt: string) => void): void {
      addTag(option.value);
      this.search = '';
    }

    private titleFromTag(tag: string): string {
      return this.mapText.get(tag)!;
    }

    @Watch('selectedInternal')
    private onSelectedChanged(newVal: string[], oldVal: string[]): void {
        this.$emit('change', newVal);
    }
}
</script>
