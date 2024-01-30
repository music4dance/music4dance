<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { TagMatrix, TagRow } from "@/models/TagMatrix";
import DanceName from "./DanceName.vue";
import type { TableField, TableItem } from "bootstrap-vue-next";
import { NamedObject } from "@/models/DanceDatabase/NamedObject";

const props = defineProps({
  matrix: TagMatrix,
});

const fields = buildFields();

const groupClass = "tag-matrix-group";

const itemClass = "tag-matrix-item";

// INT-TODO: Get rid of this once we have generic tables
const rows = props.matrix!.list as unknown as TableItem[];

function buildFields(): TableField[] {
  const fields: TableField[] = [
    {
      key: "dance",
      label: "Dance Style",
      stickyColumn: true,
      formatter: (value: any) => value.name,
    },
  ];

  const columns = props.matrix!.columns;

  for (const column of columns) {
    fields.push({
      key: column.tag,
      label: column.title.replace("/", "/<wbr>"),
      // INT-TODO: Use this once formatted value gets passed to template
      //formatter: (key: unknown, item: any) => countFromKey(key as string, item),
    });
  }

  return fields;
}

function columnFromTag(tag: string): number {
  return props.matrix!.columns.findIndex((c) => c.tag === tag)!;
}

function countFromKey(key: string, row: TagRow): string {
  return row.counts[columnFromTag(key)].toString();
}

function danceLink(row: TagRow): string {
  return `/dances/${wordsToKebab(row.dance.name)}`;
}

function danceTagLink(row: TagRow, field: { key: string }): string {
  return `/Song/?filter=Index-OOX,${row.dance.id}-Dances-.-.-.-.-.-.-+${field.key}`;
}

function toNamedObject(obj: unknown): NamedObject {
  return obj as NamedObject;
}

function tagLink(key: string): string {
  return `/Song/?filter=Index-.-.-.-.-.-.-.-.-+${key}`;
}
</script>

<template>
  <div>
    <!-- INT-TODO: Add back in foot-clone -->
    <BTable striped hover :items="rows" :fields="fields" responsive>
      <template #head(dance)="data">{{ data.field.label }}</template>

      <template #head()="data">
        <a :href="tagLink(data.field.key)" v-html="data.field.label"></a>
      </template>

      <template #cell(dance)="data">
        <div :class="data.item.isGroup ? groupClass : itemClass">
          <!-- INT-TODO: Should be able to remove this case if we get generic tables -->
          <a :href="danceLink(data.item as unknown as TagRow)">
            <DanceName
              :dance="toNamedObject(data.item.dance)"
              :show-synonyms="true"
              :multi-line="true"
            ></DanceName>
          </a>
        </div>
      </template>

      <template #cell()="data">
        <!-- INT-TODO: should be able to use data.value rather than calling countFromKey -->
        <BButton
          v-if="countFromKey(data.field.key, data.item as unknown as TagRow) !== '0'"
          variant="primary"
          size="sm"
          :href="danceTagLink(data.item as unknown as TagRow, data.field)"
        >
          {{ countFromKey(data.field.key, data.item as unknown as TagRow) }}
        </BButton>
      </template>
    </BTable>
  </div>
</template>

<style scoped>
.tag-matrix-group {
  font-weight: bold;
}

.tag-matrix-item {
  padding-left: 1em;
}
</style>
