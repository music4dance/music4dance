<script setup lang="ts">
import { wordsToKebab } from "@/helpers/StringHelpers";
import { TagMatrix, TagRow } from "@/models/TagMatrix";
import type { TableFieldRaw, TableItem } from "bootstrap-vue-next";
import { NamedObject } from "@/models/DanceDatabase/NamedObject";
import type { LiteralUnion } from "@/helpers/bsvn-types";

const props = defineProps({
  matrix: TagMatrix,
});

const fields = buildFields();

const groupClass = "tag-matrix-group";

const itemClass = "tag-matrix-item";

const rows = props.matrix!.list as TableItem<TagRow>[];

function buildFields(): Exclude<TableFieldRaw<TagRow>, string>[] {
  const fields: Exclude<TableFieldRaw<TagRow>, string>[] = [
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
      formatter: (_value: unknown, key: unknown, item: any) => countFromKey(key as string, item),
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

function danceTagLink(key: string, row: TagRow): string {
  return `/Song/?filter=Index-OOX,${row.dance.id}-Dances-.-.-.-.-.-.-+${key}`;
}

function toNamedObject(obj: unknown): NamedObject {
  return obj as NamedObject;
}

function tagLink(key: LiteralUnion<string | number | symbol>): string {
  return `/Song/?filter=Index-.-.-.-.-.-.-.-.-+${String(key)}`;
}
</script>

<template>
  <div>
    <!-- INT-TODO: Add back in foot-clone -->
    <BTable striped hover :items="rows" :fields="fields" responsive>
      <template #head(dance)="data">{{ data.field.label }}</template>

      <template #head()="data">
        <a :href="tagLink(data.field.key)" v-html="data.field.label" />
      </template>

      <template #cell(dance)="data">
        <div :class="data.item.isGroup ? groupClass : itemClass">
          <a :href="danceLink(data.item)">
            <DanceName
              :dance="toNamedObject(data.item.dance)"
              :show-synonyms="true"
              :multi-line="true"
            />
          </a>
        </div>
      </template>

      <template #cell()="data">
        <BButton
          v-if="countFromKey(data.field.key as string, data.item) !== '0'"
          variant="primary"
          size="sm"
          :href="danceTagLink(data.field.key as string, data.item)"
        >
          {{ countFromKey(data.field.key as string, data.item) }}
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
