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
import TagButton from "@/components/TagButton.vue";
import { SongFilter } from "@/model/SongFilter";
import { Tag } from "@/model/Tag";
import { TaggableObject } from "@/model/TaggableObject";
import { TagHandler } from "@/model/TagHandler";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { TagButton },
  props: {
    container: { type: Object as PropType<TaggableObject>, required: true },
    filter: Object as PropType<SongFilter>,
    user: String,
  },
  computed: {
    tags(): Tag[] {
      const ret = this.container.tags.filter(
        (t) => t.category && t.category !== "Dance"
      );
      return ret;
    },
  },
  methods: {
    tagHandler(tag: Tag): TagHandler {
      return new TagHandler(tag, this.user, this.filter, this.container);
    },
  },
});
</script>
