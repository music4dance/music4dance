import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { TagDatabase } from "@/model/TagDatabase";
import { Component, Vue } from "vue-property-decorator";

declare global {
  interface Window {
    environment?: DanceEnvironment;
    tagDatabe?: TagDatabase;
  }
}

@Component
export default class EnvironmentManager extends Vue {
  protected get environment(): DanceEnvironment {
    return window.environment ? window.environment : new DanceEnvironment();
  }

  protected get tagDatabase(): TagDatabase {
    return window.tagDatabase ? window.tagDatabase : new TagDatabase();
  }
}
