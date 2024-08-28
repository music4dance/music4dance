<script setup lang="ts">
import { SongEditor } from "@/models/SongEditor";
import { SongFilter } from "@/models/SongFilter";
import { PropertyType } from "@/models/SongProperty";
import { Tag } from "@/models/Tag";
import { TaggableObject } from "@/models/TaggableObject";
import { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { safeTagDatabase } from "@/helpers/TagEnvironmentManager";

const context = getMenuContext();
const tagDatabase = safeTagDatabase();

const props = defineProps<{
  container: TaggableObject;
  filter: SongFilter;
  user?: string;
  editor?: SongEditor;
  edit?: boolean;
}>();

const emit = defineEmits<{
  "update-song": [];
  "tag-clicked": [tag: TagHandler];
  edit: [];
}>();

const userTagKeys = computed<string[]>({
  get: () => {
    const userTags = props.container.currentUserTags;
    return userTags
      ? userTags.filter((t) => t.category.toLowerCase() !== "dance").map((t) => t.key)
      : [];
  },
  set: (keys: string[]) => {
    // eslint-disable-next-line no-console
    console.log(keys.join("|"));
    const oldKeys = userTagKeys.value;

    const delta = Math.abs(keys.length - oldKeys.length);
    if (delta === 0) {
      return;
    }

    if (delta !== 1) {
      throw new Error("Shouldn't be able to add or remove more than one tag at a time");
    }
    const add = keys.length > oldKeys.length;
    const tag = keyDifference(add ? keys : oldKeys, add ? oldKeys : keys);
    props.editor?.addProperty(
      (add ? PropertyType.addedTags : PropertyType.removedTags) + props.container.modifier,
      tag,
    );
    if (add) {
      tagDatabase.addTag(tag);
    }

    emit("update-song");
  },
});

const authenticated = computed(() => !!props.user && !!props.editor);

const tagList = computed(() => tagDatabase.tags);

const otherTags = computed(() => {
  const userTags = props.container.currentUserTags;
  return props.container.tags.filter(
    (t) => !userTags.find((u) => u.key === t.key) && t.category.toLowerCase() !== "dance",
  );
});

const keyDifference = (long: string[], short: string[]): string => {
  return long.find((k) => !short.includes(k))!;
};

const addTag = (handler: TagHandler): void => {
  props.editor?.addProperty(PropertyType.addedTags + props.container.modifier, handler.tag.key);
  emit("update-song");
};

const deleteTag = (handler: TagHandler): void => {
  props.editor?.addProperty(PropertyType.removedTags + props.container.modifier, handler.tag.key);
  emit("update-song");
};

const tagHandler = (tag: Tag): TagHandler => {
  return new TagHandler(tag);
};
</script>

<template>
  <span>
    <div v-if="edit">
      <span v-if="userTagKeys.length" class="title">Your Tags:</span>
      <TagCategorySelector
        id="songTags"
        v-model="userTagKeys"
        :tag-list="tagList"
        choose-label="Add Tags"
        search-label="Search/Add"
        empty-label="No more tags to choose"
        :add-categories="container.categories"
      />
      <span v-if="otherTags.length" class="title mr-2">Other's Tags:</span>
      <span>
        <TagButtonOther
          v-for="tag in otherTags"
          :key="tag.key"
          :tag-handler="tagHandler(tag)"
          @change-tag="addTag"
        />
      </span>
      <div v-if="context.canEdit">
        <span v-if="otherTags.length" class="title mr-2">Remove Tags:</span>
        <TagButtonOther
          v-for="tag in otherTags"
          :key="tag.key"
          :tag-handler="tagHandler(tag)"
          :is-delete="true"
          @change-tag="deleteTag"
        />
      </div>
    </div>
    <span v-else>
      <TagList
        :container="container"
        :filter="filter"
        :user="user"
        @tag-clicked="emit('tag-clicked', $event)"
      />
      <BLink v-if="authenticated" href="#" @click="$emit('edit')">
        <IBiPencilFill class="me-2" />
      </BLink>
    </span>
  </span>
</template>

<style lang="scss" scoped>
.title {
  font-size: 1.25rem;
  font-weight: bold;
}
</style>
