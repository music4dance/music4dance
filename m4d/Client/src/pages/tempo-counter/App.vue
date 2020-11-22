<template>
  <page
    id="app"
    :consumesEnvironment="true"
    @environment-loaded="onEnvironmentLoaded"
  >
    <counter />
    <dance-list :dances="dances" />
  </page>
</template>

<script lang="ts">
import { Component, Vue } from "vue-property-decorator";
import Page from "@/components/Page.vue";
import Counter from "./components/Counter.vue";
import DanceList from "./components/DanceList.vue";
import { DanceStats } from "@/model/DanceStats";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

@Component({
  components: {
    Counter,
    DanceList,
    Page,
  },
})
export default class App extends Vue {
  private dances: DanceStats[] = [];

  private onEnvironmentLoaded(environment: DanceEnvironment): void {
    this.dances = environment.stats!;
  }
}
</script>

<style lang="scss"></style>
