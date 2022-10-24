<template>
  <b-card header-text-variant="primary" no-body border-variant="primary">
    <b-card-header header-class="d-flex justify-content-between"
      ><span>History Log</span
      ><span
        ><b-button
          :disabled="history.isAnnotated"
          variant="primary"
          size="sm"
          class="mr-2"
          @click="annotate"
          >{{ annotateTitle }}</b-button
        ><b-button
          :disabled="history.isSorted"
          variant="primary"
          size="sm"
          @click="sort"
          >{{ sortTitle }}</b-button
        ></span
      >
    </b-card-header>
    <b-list-group flush>
      <b-list-group-item
        v-for="(property, index) in history.properties"
        :key="index"
        :variant="getVariant(property)"
        class="d-flex justify-content-between"
        :class="{ subprop: !property.isAction }"
      >
        {{ property.name }}={{ property.value }}
        <b-button-group v-if="editing">
          <b-button
            v-if="!property.isAction"
            size="sm"
            variant="success"
            @click="$emit('promote-property', index)"
          >
            <b-icon-person-plus-fill></b-icon-person-plus-fill>
          </b-button>
          <b-button
            :disabled="!showFirst(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-first', index)"
          >
            <b-icon-chevron-bar-up></b-icon-chevron-bar-up>
          </b-button>
          <b-button
            :disabled="!showPrevious(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-up', index)"
          >
            <b-icon-chevron-up></b-icon-chevron-up>
          </b-button>
          <b-button
            :disabled="!showNext(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-down', index)"
          >
            <b-icon-chevron-down></b-icon-chevron-down>
          </b-button>
          <b-button
            :disabled="!showLast(property, index)"
            size="sm"
            variant="primary"
            @click="$emit('move-property-last', index)"
          >
            <b-icon-chevron-bar-down></b-icon-chevron-bar-down>
          </b-button>
          <b-button-close
            text-variant="danger"
            class="ml-2"
            @click="$emit('delete-property', index)"
          ></b-button-close>
        </b-button-group>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import { SongHistory } from "@/model/SongHistory";
import { SongProperty } from "@/model/SongProperty";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";

// TODO:
//  - At some point may want to make a best guess at attribution for existing merge
//  - At some point may want to have move keep date in sync (e.g. update date - but maybe with a second field?)

@Component
export default class SongHistoryLog extends Vue {
  @Prop() private readonly history!: SongHistory;
  @Prop() private readonly editing!: boolean;

  private getVariant(prop: SongProperty): string | undefined {
    return prop.isAction ? "primary" : undefined;
  }

  private get sortTitle(): string {
    return this.history.isSorted ? "sorted" : "sort";
  }

  private sort(): void {
    this.$emit("replace-history", this.history.sorted);
  }

  private get annotateTitle(): string {
    return this.history.isAnnotated ? "annotated" : "annotate";
  }

  private annotate(): void {
    this.$emit("replace-history", this.history.annotated);
  }

  private showFirst(prop: SongProperty, index: number): boolean {
    if (prop.isAction) {
      return index > 0;
    } else {
      return index > 0 && !this.history.properties[index - 1].isAction;
    }
  }

  private showLast(prop: SongProperty, index: number): boolean {
    const properties = this.history.properties;
    if (prop.isAction) {
      return properties.find((p, i) => i > index && p.isAction);
    } else {
      return index < properties.length - 1 && !properties[index + 1].isAction;
    }
  }

  private showPrevious(prop: SongProperty, index: number): boolean {
    if (prop.isAction) {
      return index > 0;
    } else {
      return index > 0 && !this.history.properties[index - 1].isAction;
    }
  }

  private showNext(prop: SongProperty, index: number): boolean {
    if (prop.isAction) {
      return this.history.properties.find((p, i) => i > index && p.isAction);
    } else {
      const length = this.history.properties.length;
      return index < length - 1 && !this.history.properties[index + 1].isAction;
    }
  }
}
</script>

<style lang="scss" scoped>
.subprop {
  padding-left: 2rem;
}
</style>
