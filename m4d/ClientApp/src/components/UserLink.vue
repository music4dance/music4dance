<script setup lang="ts">
import { computed } from "vue";
import { UserQuery } from "@/models/UserQuery";

const props = defineProps<{ user: string }>();
const userQuery = computed(() => new UserQuery(props.user));
const userLink = computed(() => `/users/info/${userQuery.value.userName}`);
const userClasses = computed(() => (userQuery.value.isPseudo ? ["pseudo"] : []));
</script>

<template>
  <strong v-if="userQuery.isUnavailable || userQuery.isAlgorithmic || userQuery.isBatch">{{
    userQuery.displayName
  }}</strong>
  <a v-else :href="userLink" :class="userClasses">{{ userQuery.displayName }}</a>
</template>

<style lang="scss" scoped>
.pseudo {
  font-style: italic;
}
</style>
