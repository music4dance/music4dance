import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import { PropType } from "vue";

export default EnvironmentManager.extend({
  props: {
    tagHandler: { type: Object as PropType<TagHandler>, required: true },
  },
  computed: {
    tag(): Tag {
      return this.tagHandler.tag;
    },
    title(): string {
      const parent = this.tagHandler.parent;
      return parent ? parent.description : this.tag.value;
    },
  },
});
