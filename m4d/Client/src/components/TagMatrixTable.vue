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
            {{ data.value }}
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
import { Component, Prop, Vue } from "vue-property-decorator";
import { TagMatrix, TagRow } from "@/model/TagMatrix";
import { DanceObject } from "@/model/DanceStats";
import { wordsToKebab } from "@/helpers/StringHelpers";

@Component({
  components: {},
})
export default class TagMatrixTable extends Vue {
  @Prop() private matrix!: TagMatrix;

  private get rows(): TagRow[] {
    return this.matrix.list;
  }

  private get fields() {
    /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
    const fields: any[] = [
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
  }

  private get groupClass(): string {
    return "tag-matrix-group";
  }

  private columnFromTag(tag: string): number {
    return this.matrix.columns.findIndex((c) => c.tag === tag)!;
  }

  private get itemClass(): string {
    return "tag-matrix-item";
  }

  private countFromKey(key: string, row: TagRow): string {
    return row.counts[this.columnFromTag(key)].toString();
  }

  private danceLink(row: TagRow): string {
    return `/dances/${wordsToKebab(row.dance.name)}`;
  }

  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  private danceTagLink(row: TagRow, field: any): string {
    return `/Song/?filter=Index-OOX,${row.dance.id}-Dances-.-.-.-.-.-.-+${field.key}`;
  }

  private tagLink(key: string): string {
    return `/Song/?filter=Index-.-.-.-.-.-.-.-.-+${key}`;
  }
}
</script>
