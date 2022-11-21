import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import { Component, Prop, Vue } from "vue-property-decorator";
import TagModal from "./TagModal.vue";

@Component({
  components: {
    TagModal,
  },
})
export default class TagButtonBase extends Vue {
  @Prop() protected readonly tagHandler!: TagHandler;

  protected get variant(): string {
    return this.tag.category.toLowerCase();
  }

  protected get tag(): Tag {
    return this.tagHandler.tag;
  }

  protected get icon(): string {
    const tagInfo = Tag.TagInfo.get(this.variant);

    if (tagInfo) {
      return tagInfo.iconName;
    }

    throw new Error(`Couldn't find tagInfo for ${this.variant}`);
  }

  protected get selectedIcon(): string | undefined {
    return this.tagHandler.user && this.tagHandler.isSelected
      ? "check-circle"
      : undefined;
  }

  protected showModal(): void {
    this.$bvModal.show(this.tagHandler.id);
  }
}
