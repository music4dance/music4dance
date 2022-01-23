<template>
  <a :href="userLink" :class="userClasses">{{ userQuery.displayName }}</a>
</template>

<script lang="ts">
import { UserQuery } from "@/model/UserQuery";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class UserLink extends Vue {
  @Prop() readonly user!: string;

  private get userLink(): string {
    return `/users/info/${this.userQuery.userName}`;
  }

  private get userClasses(): string[] {
    return this.userQuery.isPseudo ? ["pseudo"] : [];
  }

  private get userQuery(): UserQuery {
    return new UserQuery(this.user);
  }
}
</script>

<style lang="scss" scoped>
.pseudo {
  font-style: italic;
}
</style>
