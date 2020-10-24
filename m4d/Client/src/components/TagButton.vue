<template>
  <b-button
    :title="tag.value"
    :variant="variant"
    size="sm"
    style="margin-inline-end: 0.25em; margin-bottom: 0.25em"
    @click="showModal()"
  >
    <b-icon :icon="icon"></b-icon>
    {{ tag.value }}
    <tag-modal :tagHandler="tagHandler"></tag-modal>
  </b-button>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import TagModal from "./TagModal.vue";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";

@Component({
  components: {
    TagModal,
  },
})
export default class TagButton extends Vue {
  @Prop() private readonly tagHandler!: TagHandler;

  private get variant(): string {
    return this.tag.category.toLowerCase();
  }

  private get tag(): Tag {
    return this.tagHandler.tag;
  }

  private get icon(): string {
    const tagInfo = Tag.TagInfo.get(this.variant);

    if (tagInfo) {
      return tagInfo.iconName;
    }

    const message = `Couldn't find tagInfo for ${this.variant}`;
    // tslint:disable-next-line:no-console
    console.log(message);
    throw new Error(message);
  }

  private showModal(): void {
    this.$bvModal.show(this.tagHandler.id);
  }
}
</script>
