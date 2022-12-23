<template>
  <div>
    <div v-for="(comment, idx) in comments" :key="idx">
      <single-comment-editor
        v-if="enableEdit(comment)"
        :editor="editor"
        :container="container"
        :comment="comment"
        :placeholder="placeholder"
        :rows="rows"
        v-on="$listeners"
      ></single-comment-editor>
      <single-comment-viewer v-else :comment="comment"></single-comment-viewer>
    </div>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import { SongEditor } from "@/model/SongEditor";
import { TaggableObject } from "@/model/TaggableObject";
import { UserComment } from "@/model/UserComment";
import { PropType } from "vue";
import SingleCommentEditor from "./SingleCommentEditor.vue";
import SingleCommentViewer from "./SingleCommentViewer.vue";

export default AdminTools.extend({
  components: { SingleCommentEditor, SingleCommentViewer },
  props: {
    container: { type: Object as PropType<TaggableObject>, required: true },
    editor: { type: Object as PropType<SongEditor>, required: true },
    edit: Boolean,
    placeholder: String,
    rows: Number,
  },
  computed: {
    comments(): UserComment[] {
      let comments = this.container.comments;
      if (this.edit && !comments.find((c) => c.userName === this.userName)) {
        comments = [
          ...comments,
          new UserComment({ userName: this.userName, comment: "" }),
        ];
      }
      return comments;
    },
  },
  methods: {
    enableEdit(comment: UserComment): boolean {
      return this.edit && this.userName === comment.userName;
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
