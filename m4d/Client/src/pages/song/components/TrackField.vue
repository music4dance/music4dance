<template>
  <div>
    <b-button
      v-if="canAdd"
      variant="outline-primary"
      size="sm"
      block
      style="text-align: left"
      class="mb-2"
      @click="onAdd"
    >
      <b-icon-lock-fill aria-label="add"></b-icon-lock-fill>
      {{ friendlyName }}: <b>{{ value }}</b>
    </b-button>
    <div v-else>
      {{ friendlyName }}: <b>{{ value }}</b>
    </div>
  </div>
</template>

<script lang="ts">
import PurchaseLogo from "@/components/PurcahseLogo.vue";
import { camelToPascal } from "@/helpers/StringHelpers";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: { PurchaseLogo } })
export default class TrackItem extends Vue {
  @Prop() name!: string;
  @Prop() value!: string;
  @Prop() canAdd?: boolean;

  private get friendlyName(): string {
    return camelToPascal(this.name);
  }

  private onAdd(): void {
    this.$emit("add-property", this.name, this.value);
  }
}
</script>
