<template>
  <a :href="link" :style="styles"
    ><slot>{{ text }}</slot></a
  >
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class InfoLink extends Vue {
  @Prop() private text?: string;
  @Prop() private link!: string;
  /* eslint-disable-next-line @typescript-eslint/no-explicit-any */
  @Prop() private styles?: any;
}
</script>
