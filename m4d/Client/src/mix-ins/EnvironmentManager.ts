import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { Component, Vue } from "vue-property-decorator";

declare const environment: DanceEnvironment;

@Component
export default class EnvironmentManager extends Vue {
  protected get environment(): DanceEnvironment {
    return environment;
  }
}
