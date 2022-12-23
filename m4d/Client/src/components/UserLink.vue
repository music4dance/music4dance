<template>
  <a :href="userLink" :class="userClasses">{{ userQuery.displayName }}</a>
</template>

<script lang="ts">
import { UserQuery } from "@/model/UserQuery";
import Vue from "vue";

export default Vue.extend({
  props: { user: { type: String, required: true } },
  computed: {
    userLink(): string {
      return `/users/info/${this.userQuery.userName}`;
    },

    userClasses(): string[] {
      return this.userQuery.isPseudo ? ["pseudo"] : [];
    },

    userQuery(): UserQuery {
      return new UserQuery(this.user);
    },
  },
});
</script>

<style lang="scss" scoped>
.pseudo {
  font-style: italic;
}
</style>
