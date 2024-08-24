<script setup lang="ts">
import { UserComment } from "@/models/UserComment";
import UserLink from "./UserLink.vue";

defineProps<{ comment: UserComment }>();
</script>

<template>
  <div>{{ comment.comment }} -- <UserLink :user="comment.userName" /></div>
</template>
