<template>
  <b-card
    header="History Log"
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
import { SongHistory } from "@/model/SongHistory";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class SongHistoryLog extends Vue {
  @Prop() private readonly history!: SongHistory;
  @Prop() private readonly editing!: boolean;

  private onDelete(index: number): void {
    this.$emit("delete-property", index);
  }
}
</script>
