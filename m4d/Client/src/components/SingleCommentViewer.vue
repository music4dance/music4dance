<template>
  <div>
    {{ comment.comment }} -- <user-link :user="comment.userName"></user-link>
  </div>
</template>

<script lang="ts">
import { UserComment } from "@/model/UserComment";
import { Component, Prop, Vue } from "vue-property-decorator";
import UserLink from "./UserLink.vue";

@Component({ components: { UserLink } })
export default class SingleCommentViewer extends Vue {
  @Prop() readonly comment!: UserComment;
}
</script>
