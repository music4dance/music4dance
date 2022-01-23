<template>
  <div>
    <b-form-textarea
      v-model="text"
      :rows="rows"
      :placeholder="placeholder"
    ></b-form-textarea>
    <div class="signature">
      --<user-link :user="comment.userName"></user-link>
    </div>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import EnvironmentManager from "@/mix-ins/EnvironmentManager";
import { SongEditor } from "@/model/SongEditor";
import { PropertyType } from "@/model/SongProperty";
import { TaggableObject } from "@/model/TaggableObject";
import { UserComment } from "@/model/UserComment";
import { Component, Mixins, Prop } from "vue-property-decorator";
import UserLink from "./UserLink.vue";

// TODO:
//  Consider making placeholder smarter
//  Consider enabling markdown
//  Do we want to put comments in song changes (or maybe a reference that it's been commented on???)
// Beyond the Sea: 1454d1a3-3b40-4085-a2e0-0889f65f8f57
// https://localhost:5001/song/details/1454d1a3-3b40-4085-a2e0-0889f65f8f57?filter=Index-----%5C-me%7Ch
@Component({
  components: {
    UserLink,
  },
})
export default class SingleCommentEditor extends Mixins(
  EnvironmentManager,
  AdminTools
) {
  @Prop() readonly editor!: SongEditor;
  @Prop() readonly container!: TaggableObject;
  @Prop() readonly comment!: UserComment;
  @Prop() readonly placeholder!: string;
  @Prop() readonly rows!: number;

  private get text(): string {
    return this.comment.comment;
  }

  private set text(text: string) {
    this.editor.modifyProperty(
      PropertyType.addCommentField + this.container.modifier,
      text
    );
    this.$emit("update-song");
  }
}
</script>

<style lang="scss" scoped>
.signature {
  text-align: right;
}
</style>
