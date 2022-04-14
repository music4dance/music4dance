<template>
  <div>
    <p>
      In order to add songs, you must first
      <a :href="register">register</a>
      and
      <a :href="login">sign in.</a>
    </p>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class AugmentInfo extends Vue {
  @Prop() private id?: string;

  private get register(): string {
    return this.authenticate("register");
  }

  private get login(): string {
    return this.authenticate("login");
  }

  private authenticate(type: string): string {
    const idParam = this.id ? `?id=${this.id}` : "";
    return `/identity/account/${type}?returnUrl=/song/augment${idParam}`;
  }
}
</script>
