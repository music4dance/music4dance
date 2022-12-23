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
import { SongEditor } from "@/model/SongEditor";
import { PropertyType } from "@/model/SongProperty";
import { TaggableObject } from "@/model/TaggableObject";
import { UserComment } from "@/model/UserComment";
// TODO:
//  Consider making placeholder smarter
//  Consider enabling markdown
//  Do we want to put comments in song changes (or maybe a reference that it's been commented on???)
// Beyond the Sea: 1454d1a3-3b40-4085-a2e0-0889f65f8f57
// https://localhost:5001/song/details/1454d1a3-3b40-4085-a2e0-0889f65f8f57?filter=Index-----%5C-me%7Ch
import { PropType } from "vue";
import UserLink from "./UserLink.vue";

export default AdminTools.extend({
  components: { UserLink },
  props: {
    editor: { type: Object as PropType<SongEditor>, required: true },
    container: { type: Object as PropType<TaggableObject>, required: true },
    comment: { type: Object as PropType<UserComment>, required: true },
    placeholder: String,
    rows: Number,
  },
  data() {
    return new (class {})();
  },
  computed: {
    text: {
      get: function (): string {
        return this.comment.comment;
      },
      set: function (text: string): void {
        this.editor.modifyProperty(
          PropertyType.addCommentField + this.container.modifier,
          text
        );
        this.$emit("update-song");
      },
    },
  },
});
</script>

<style lang="scss" scoped>
.signature {
  text-align: right;
}
</style>
