import { Component, Vue } from "vue-property-decorator";
import { DanceEnvironment } from "@/model/DanceEnvironmet";

declare const environment: DanceEnvironment;

@Component
export default class EnvironmentManager extends Vue {
  protected get environment(): DanceEnvironment {
    return environment;
  }
}
