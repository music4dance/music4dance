<template>
  <div>
    {{ comment.comment }} -- <user-link :user="comment.userName"></user-link>
  </div>
</template>

<script lang="ts">
import { UserComment } from "@/model/UserComment";
import Vue, { PropType } from "vue";
import UserLink from "./UserLink.vue";

export default Vue.extend({
  components: { UserLink },
  props: { comment: { type: Object as PropType<UserComment>, required: true } },
});
</script>
