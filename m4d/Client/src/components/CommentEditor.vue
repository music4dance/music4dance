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
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { SongEditor } from "@/model/SongEditor";
import { TaggableObject } from "@/model/TaggableObject";
import { UserComment } from "@/model/UserComment";
import { Component, Mixins, Prop } from "vue-property-decorator";
import SingleCommentEditor from "./SingleCommentEditor.vue";
import SingleCommentViewer from "./SingleCommentViewer.vue";

@Component({
  components: {
    SingleCommentEditor,
    SingleCommentViewer,
  },
})
export default class CommentEditor extends Mixins(
  EnvironmentManager,
  AdminTools
) {
  @Prop() readonly container!: TaggableObject;
  @Prop() readonly editor!: SongEditor;
  @Prop() readonly edit!: boolean;
  @Prop() readonly placeholder!: string;
  @Prop() readonly rows!: number;

  private get comments(): UserComment[] {
    let comments = this.container.comments;
    if (this.edit && !comments.find((c) => c.userName === this.userName)) {
      comments = [
        ...comments,
        new UserComment({ userName: this.userName, comment: "" }),
      ];
    }
    return comments;
  }

  private enableEdit(comment: UserComment): boolean {
    return this.edit && this.userName === comment.userName;
  }
}
</script>

<style lang="scss" scoped>
.title {
  font-size: 1.25rem;
  font-weight: bold;
}
</style>
