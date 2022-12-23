import { DanceEnvironment } from "@/model/DanceEnvironment";
import { TagDatabase } from "@/model/TagDatabase";
import Vue from "vue";

declare global {
  interface Window {
    environment?: DanceEnvironment;
    tagDatabe?: TagDatabase;
  }
}

export default Vue.extend({
  computed: {
    environment(): DanceEnvironment {
      return window.environment ? window.environment : new DanceEnvironment();
    },
    tagDatabase(): TagDatabase {
      return window.tagDatabase ? window.tagDatabase : new TagDatabase();
    },
  },
});
