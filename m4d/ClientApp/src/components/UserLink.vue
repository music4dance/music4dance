<script setup lang="ts">
import { UserQuery } from "@/models/UserQuery";

const props = defineProps<{ user: string }>();
const userQuery = new UserQuery(props.user);
const userLink = `/users/info/${userQuery.userName}`;
const userClasses = userQuery.isPseudo ? ["pseudo"] : [];
</script>

<template>
  <strong v-if="userQuery.isUnavailable">{{ userQuery.displayName }}</strong>
  <a v-else :href="userLink" :class="userClasses">{{ userQuery.displayName }}</a>
</template>

<style lang="scss" scoped>
.pseudo {
  font-style: italic;
}
</style>
