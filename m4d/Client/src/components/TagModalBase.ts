import "reflect-metadata";
import { Component, Prop, Mixins } from "vue-property-decorator";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";

@Component
export default class TagModalBase extends Mixins(EnvironmentManager) {
  @Prop() protected readonly tagHandler!: TagHandler;

  protected get tag(): Tag {
    return this.tagHandler.tag;
  }

  protected get title(): string {
    const parent = this.tagHandler.parent;
    return parent ? parent.description : this.tag.value;
  }
}
