<template>
  <span>
    <div v-if="edit">
      <span class="title" v-if="userTagKeys.length">Your Tags:</span>
      <tag-category-selector
        id="songTags"
        :tagList="tagList"
        chooseLabel="Add Tags"
        searchLabel="Search/Add"
        emptyLabel="No more tags to choose"
        :addCategories="container.categories"
        v-model="userTagKeys"
        @input="updateTags"
      ></tag-category-selector>
      <span class="title mr-2" v-if="otherTags.length">Other's Tags:</span>
      <span>
        <tag-button-other
          v-for="tag in otherTags"
          :key="tag.key"
          :tagHandler="tagHandler(tag)"
          @change-tag="addTag"
        ></tag-button-other>
      </span>
      <div v-if="canEdit">
        <span class="title mr-2" v-if="otherTags.length">Remove Tags:</span>
        <tag-button-other
          v-for="tag in otherTags"
          :key="tag.key"
          :tagHandler="tagHandler(tag)"
          :isDelete="true"
          @change-tag="deleteTag"
        ></tag-button-other>
      </div>
    </div>
    <span v-else>
      <tag-list :container="container" :filter="filter" :user="user"></tag-list>
      <b-link href="#" v-if="authenticated" @click="$emit('edit')">
        <b-icon-pencil-fill class="mr-2"></b-icon-pencil-fill>
      </b-link>
    </span>
  </span>
</template>

<script lang="ts">
import TagButtonOther from "@/components/TagButtonOther.vue";
import TagCategorySelector from "@/components/TagCategorySelector.vue";
import TagList from "@/components/TagList.vue";
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { SongEditor } from "@/model/SongEditor";
import { SongFilter } from "@/model/SongFilter";
import { PropertyType } from "@/model/SongProperty";
import { Tag } from "@/model/Tag";
import { TaggableObject } from "@/model/TaggableObject";
import { TagHandler } from "@/model/TagHandler";
import { PropType } from "vue";
import mixins from "vue-typed-mixins";

export default mixins(EnvironmentManager, AdminTools).extend({
  components: { TagButtonOther, TagCategorySelector, TagList },
  props: {
    container: { type: Object as PropType<TaggableObject>, required: true },
    filter: Object as PropType<SongFilter>,
    user: String,
    editor: { type: Object as PropType<SongEditor>, required: true },
    edit: Boolean,
  },
  data() {
    return new (class {})();
  },
  computed: {
    userTagKeys: {
      get: function (): string[] {
        const userTags = this.container.currentUserTags;
        return userTags
          ? userTags
              .filter((t) => t.category.toLowerCase() !== "dance")
              .map((t) => t.key)
          : [];
      },
      set: function (keys: string[]): void {
        // eslint-disable-next-line no-console
        console.log(keys.join("|"));
      },
    },
    authenticated(): boolean {
      return !!this.user && !!this.editor;
    },
    tagList(): Tag[] {
      return this.tagDatabase.tags;
    },
    otherTags(): Tag[] {
      const userTags = this.container.currentUserTags;
      return this.container.tags.filter(
        (t) =>
          !userTags.find((u) => u.key === t.key) &&
          t.category.toLowerCase() !== "dance"
      );
    },
  },
  methods: {
    updateTags(newKeys: string[]): void {
      const oldKeys = this.userTagKeys;

      const delta = Math.abs(newKeys.length - oldKeys.length);
      if (delta === 0) {
        return;
      }

      if (delta !== 1) {
        throw new Error(
          "Shouldn't be able to add or remove more than one tag at a time"
        );
      }
      const add = newKeys.length > oldKeys.length;
      const tag = this.keyDifference(
        add ? newKeys : oldKeys,
        add ? oldKeys : newKeys
      );
      this.editor.addProperty(
        (add ? PropertyType.addedTags : PropertyType.removedTags) +
          this.container.modifier,
        tag
      );
      if (add) {
        this.tagDatabase.addTag(tag);
      }

      this.$emit("update-song");
    },

    addTag(tag: Tag): void {
      this.editor.addProperty(
        PropertyType.addedTags + this.container.modifier,
        tag.key
      );
      this.$emit("update-song");
    },

    deleteTag(tag: Tag): void {
      this.editor.addProperty(
        PropertyType.deleteTag + this.container.modifier,
        tag.key
      );
      this.$emit("update-song");
    },

    // Returns the first string in long that doesn't exist in short
    keyDifference(long: string[], short: string[]): string {
      return long.find((k) => !short.includes(k))!;
    },

    tagHandler(tag: Tag): TagHandler {
      return new TagHandler(tag);
    },
  },
});
</script>

<style lang="scss" scoped>
.title {
  font-size: 1.25rem;
  font-weight: bold;
}
</style>
