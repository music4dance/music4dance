// TODO: This is an example of a mixin - I didn't end up using it for the core
//  vue page path (this code ended up in page.vue) but keeping this around
//  for a bit both in case it's useful and as an example if I want to build
//  other mixins

import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import { getEnvironment } from "@/helpers/DanceEnvironmentManager";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

@Component
export default class EnvironmentLoader extends Vue {
  private environment: DanceEnvironment = new DanceEnvironment();

  public get loaded(): boolean {
    const stats = this.environment?.tree;
    const loaded = !!stats && stats.length > 0;
    return loaded;
  }

  private async created() {
    this.environment = await getEnvironment();

    console.log(
      `Environment loaded: Stats = ${this.environment!.tree!.length}`
    );
  }
}
