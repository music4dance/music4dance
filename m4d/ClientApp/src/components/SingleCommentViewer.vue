<script setup lang="ts">
import { UserComment } from "@/models/UserComment";

defineProps<{ comment: UserComment }>();
</script>

<template>
  <div>{{ comment.comment }} -- <UserLink :user="comment.userName" /></div>
</template>
