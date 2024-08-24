<script setup lang="ts">
import { SongEditor } from "@/models/SongEditor";
import { PropertyType } from "@/models/SongProperty";
import { TaggableObject } from "@/models/TaggableObject";
import { UserComment } from "@/models/UserComment";
// TODO:
//  Consider making placeholder smarter
//  Consider enabling markdown
//  Do we want to put comments in song changes (or maybe a reference that it's been commented on???)
// Beyond the Sea: 1454d1a3-3b40-4085-a2e0-0889f65f8f57
// https://localhost:5001/song/details/1454d1a3-3b40-4085-a2e0-0889f65f8f57?filter=Index-----%5C-me%7Ch
import { computed } from "vue";
import UserLink from "./UserLink.vue";

const props = defineProps<{
  editor?: SongEditor;
  container: TaggableObject;
  comment: UserComment;
  placeholder?: string;
  rows?: number;
}>();

const emit = defineEmits<{
  "update-song": [];
}>();

const text = computed({
  get: () => {
    return props.comment.comment;
  },
  set: (text: string) => {
    props.editor?.modifyProperty(PropertyType.addCommentField + props.container.modifier, text);
    emit("update-song");
  },
});
</script>

<template>
  <div>
    <BFormTextarea v-model="text" :rows="rows" :placeholder="placeholder" />
    <div class="signature">--<UserLink :user="comment.userName" /></div>
  </div>
</template>

<style lang="scss" scoped>
.signature {
  text-align: right;
}
</style>
