import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import Vue, { PropType } from "vue";
import TagModal from "./TagModal.vue";

export default Vue.extend({
  components: { TagModal },
  props: {
    tagHandler: { type: Object as PropType<TagHandler>, required: true },
  },
  computed: {
    variant(): string {
      return this.tag.category.toLowerCase();
    },
    tag(): Tag {
      return this.tagHandler.tag;
    },
    icon(): string {
      const tagInfo = Tag.TagInfo.get(this.variant);

      if (tagInfo) {
        return tagInfo.iconName;
      }

      throw new Error(`Couldn't find tagInfo for ${this.variant}`);
    },
    selectedIcon(): string | undefined {
      return this.tagHandler.user && this.tagHandler.isSelected
        ? "check-circle"
        : undefined;
    },
  },
  methods: {
    showModal(): void {
      this.$bvModal.show(this.tagHandler.id);
    },
  },
});
