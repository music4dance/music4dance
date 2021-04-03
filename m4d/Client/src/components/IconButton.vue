<template>
  <a
    href="#"
    @click.prevent="onClick"
    v-b-tooltip.hover.right="likeTip"
    role="button"
  >
    <b-icon
      :icon="trueIcon"
      :variant="trueVariant"
      v-if="state"
      :font-scale="scale"
    ></b-icon>
    <b-iconstack v-else-if="state === false" :font-scale="scale">
      <b-icon
        stacked
        :icon="falseIcon"
        :variant="falseVariant"
        scale="0.75"
        shift-v="-1"
      ></b-icon>
      <b-icon stacked icon="x-circle" variant="danger"></b-icon>
    </b-iconstack>
    <b-icon
      :icon="falseIcon"
      :variant="falseVariant"
      :font-scale="scale"
      v-else
    ></b-icon>
  </a>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class IconButton extends Vue {
  @Prop() private readonly state?: boolean;
  @Prop() private readonly authenticated!: boolean;
  @Prop() private readonly redirectUrl!: string;
  @Prop() private readonly signInTip!: string;
  @Prop() private readonly undefinedTip!: string;
  @Prop() private readonly trueTip!: string;
  @Prop() private readonly falseTip!: string;
  @Prop() private readonly trueIcon!: string;
  @Prop() private readonly falseIcon!: string;
  @Prop() private readonly trueVariant!: string;
  @Prop() private readonly falseVariant!: string;
  @Prop() private readonly scale!: number;

  private get likeTip(): string {
    if (!this.authenticated) {
      return this.signInTip;
    }

    const state = this.state;
    if (state === undefined) {
      return this.undefinedTip;
    } else if (state) {
      return this.trueTip;
    } else {
      return this.falseTip;
    }
  }

  private async onClick(): Promise<void> {
    if (this.authenticated) {
      this.$emit("click-icon");
    } else {
      window.location.href = this.redirectUrl;
    }
  }
}
</script>
