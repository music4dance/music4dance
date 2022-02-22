<template>
  <b-button
    block
    variant="outline-primary"
    :aria-expanded="showExtra ? 'true' : 'false'"
    :aria-controls="extraId"
    @click="$emit('change', !showExtra)"
  >
    <b-icon-triangle :flip-v="!showExtra" class="mx-3"></b-icon-triangle
    >{{ buttonText }}
    <b-icon-triangle :flip-v="!showExtra" class="mx-3"></b-icon-triangle
  ></b-button>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Model, Prop, Vue } from "vue-property-decorator";

@Component
export default class ShowMore extends Vue {
  @Model("change") private readonly showExtra!: boolean;
  @Prop() private readonly extraId!: string;

  private get buttonText(): string {
    return this.showExtra ? "Show Less" : "Show More";
  }
}
</script>
