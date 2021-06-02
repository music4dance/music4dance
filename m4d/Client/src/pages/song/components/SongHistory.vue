<template>
  <b-card
    header="Song History"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-list-group flush>
      <b-list-group-item
        v-for="(property, index) in history.properties"
        :key="index"
      >
        {{ property.name }}={{ property.value }}
        <b-button-close
          v-if="editing"
          text-variant="danger"
          @click="onDelete(index)"
        ></b-button-close>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongHistory } from "@/model/SongHistory";

@Component
export default class SongHistoryComponent extends Vue {
  @Prop() private readonly history!: SongHistory;
  @Prop() private readonly editing!: boolean;

  private onDelete(index: number): void {
    this.$emit("delete-property", index);
  }
}
</script>
