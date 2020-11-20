<template>
  <div class="col-sm">
    <a :href="link">
      <img
        :src="image"
        :alt="area.name"
        role="button"
        width="128"
        height="128"
        class="center-block"
      />
      <h2 class="center-block" :class="classes" style="text-align: center">
        {{ area.name }}
      </h2>
    </a>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import { AreaInfo } from "./AreaInfo";

@Component
export default class AreaIcon extends Vue {
  @Prop() private area!: AreaInfo;

  private get link(): string {
    return this.area.link ?? `#${this.area.name}`;
  }

  private get image(): string {
    return `/images/icons/${this.area.name}.png`;
  }

  private get classes(): string[] {
    return [this.area.name.toLowerCase()];
  }
}
</script>
