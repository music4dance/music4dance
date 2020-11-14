import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";

@Component
export default class TagModal extends Vue {
  @Prop() protected readonly tagHandler!: TagHandler;

  protected get tag(): Tag {
    return this.tagHandler.tag;
  }

  protected get title(): string {
    const parent = this.tagHandler.parent;
    return parent ? parent.description : this.tag.value;
  }
}
