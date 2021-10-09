<template>
  <a :href="info.link" target="blank" :style="styles">
    <img :src="image" :width="width" :height="height" :alt="info.altText" />
  </a>
</template>

<script lang="ts">
import { PurchaseInfo } from "@/model/Purchase";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class PurchaseLogo extends Vue {
  @Prop() private readonly info!: PurchaseInfo;
  @Prop() private readonly isCharm?: boolean;
  @Prop() private readonly styles?: string;

  private get width(): number {
    return this.isCharm ? 110 : 32;
  }

  private get height(): number {
    return this.isCharm ? 40 : 32;
  }

  private get image(): string {
    return this.isCharm ? this.info.charm : this.info.logo;
  }
}
</script>
