<template>
  <b-nav pills
    ><b-nav-item :active="isActive" @click="onClick">{{
      title
    }}</b-nav-item></b-nav
  >
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class SearchNavItem extends Vue {
  @Prop() private id!: string;
  @Prop() private title!: string;
  @Prop() private active!: string;

  private get isActive(): boolean {
    return this.active === this.id;
  }

  private onClick(): void {
    window.location.hash = `#${this.id}`;
  }
}
</script>
