<script setup lang="ts">
import { SongEditor } from "@/models/SongEditor";
import { TaggableObject } from "@/models/TaggableObject";
import { UserComment } from "@/models/UserComment";
import SingleCommentEditor from "./SingleCommentEditor.vue";
import SingleCommentViewer from "./SingleCommentViewer.vue";
import { computed } from "vue";

defineOptions({ inheritAttrs: false });

const props = defineProps<{
  container: TaggableObject;
  editor?: SongEditor;
  edit?: boolean;
  placeholder?: string;
  rows?: number;
}>();

const comments = computed(() => {
  let comments = props.container.comments;
  if (props.edit && !comments.find((c) => c.userName === props.editor?.user)) {
    comments = [...comments, new UserComment({ userName: props.editor?.user, comment: "" })];
  }
  return comments;
});

const enableEdit = (comment: UserComment): boolean => {
  return props.edit && props.editor?.user === comment.userName;
};
</script>

<template>
  <div>
    <div v-for="(comment, idx) in comments" :key="idx">
      <SingleCommentEditor
        v-if="enableEdit(comment)"
        :editor="editor"
        :container="container"
        :comment="comment"
        :placeholder="placeholder"
        :rows="rows"
        v-bind="$attrs"
      ></SingleCommentEditor>
      <SingleCommentViewer v-else :comment="comment"></SingleCommentViewer>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.title {
  font-size: 1.25rem;
  font-weight: bold;
}
</style>
