import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

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
