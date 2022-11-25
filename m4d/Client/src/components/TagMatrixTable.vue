<template>
  <div>
    <b-table striped hover :items="rows" :fields="fields" responsive foot-clone>
      <template v-slot:head(dance)="data">{{ data.label }}</template>

      <template v-slot:head()="data">
        <a :href="tagLink(data.column)" v-html="data.label"></a>
      </template>

      <template v-slot:cell(dance)="data">
        <div :class="data.item.isGroup ? groupClass : itemClass">
          <a :href="danceLink(data.item)">
            <dance-name
              :dance="data.item.dance"
              :showSynonyms="true"
              :multiLine="true"
            ></dance-name>
          </a>
        </div>
      </template>

      <template v-slot:cell()="data">
        <b-button
          v-if="data.value !== '0'"
          variant="primary"
          size="sm"
          :href="danceTagLink(data.item, data.field)"
        >
          {{ data.value }}
        </b-button>
      </template>
    </b-table>
  </div>
</template>

<script lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { DanceObject } from "@/model/DanceObject";
import { TagMatrix, TagRow } from "@/model/TagMatrix";
import { BvTableFieldArray } from "bootstrap-vue";
import Vue, { PropType } from "vue";
import DanceName from "./DanceName.vue";

export default Vue.extend({
  components: {
    DanceName,
  },
  props: {
    matrix: { type: Object as PropType<TagMatrix>, required: true },
  },
  computed: {
    fields(): BvTableFieldArray {
      const fields: BvTableFieldArray = [
        {
          key: "dance",
          label: "Dance Style",
          stickyColumn: true,
          formatter: (value: DanceObject) => value.name,
        },
      ];

      const columns = this.matrix.columns;
      for (const column of columns) {
        fields.push({
          key: column.tag,
          label: column.title.replace("/", "/<wbr>"),
          formatter: (value: null, key: string, item: TagRow) =>
            this.countFromKey(key, item),
        });
      }

      return fields;
    },
    groupClass(): string {
      return "tag-matrix-group";
    },
    itemClass(): string {
      return "tag-matrix-item";
    },
    rows(): TagRow[] {
      return this.matrix.list;
    },
  },
  methods: {
    columnFromTag(tag: string): number {
      return this.matrix.columns.findIndex((c) => c.tag === tag)!;
    },
    countFromKey(key: string, row: TagRow): string {
      return row.counts[this.columnFromTag(key)].toString();
    },
    danceLink(row: TagRow): string {
      return `/dances/${wordsToKebab(row.dance.name)}`;
    },
    danceTagLink(row: TagRow, field: { key: string }): string {
      return `/Song/?filter=Index-OOX,${row.dance.id}-Dances-.-.-.-.-.-.-+${field.key}`;
    },
    tagLink(key: string): string {
      return `/Song/?filter=Index-.-.-.-.-.-.-.-.-+${key}`;
    },
  },
});
</script>
