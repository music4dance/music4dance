<template>
  <span>
    <tag-button
      v-for="tag in tags"
      :key="tag.key"
      :tagHandler="tagHandler(tag)"
    ></tag-button>
  </span>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import TagButton from "@/components/TagButton.vue";
import { SongFilter } from "@/model/SongFilter";
import { Tag } from "@/model/Tag";
import { TagHandler } from "@/model/TagHandler";
import { TaggableObject } from "@/model/TaggableObject";

@Component({
  components: {
    TagButton,
  },
})
export default class TagList extends Vue {
  @Prop() readonly container!: TaggableObject;
  @Prop() readonly filter?: SongFilter;
  @Prop() readonly user?: string;

  private get tags(): Tag[] {
    const ret = this.container.tags.filter(
      (t) => t.category && t.category !== "Dance"
    );
    return ret;
  }

  private tagHandler(tag: Tag): TagHandler {
    return new TagHandler(tag, this.user, this.filter, this.container);
  }
}
</script>
