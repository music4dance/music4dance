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
import Vue from "vue";

export default Vue.extend({
  props: { id: String },
  computed: {
    register(): string {
      return this.authenticate("register");
    },

    login(): string {
      return this.authenticate("login");
    },
  },
  methods: {
    authenticate(type: string): string {
      const idParam = this.id ? `?id=${this.id}` : "";
      return `/identity/account/${type}?returnUrl=/song/augment${idParam}`;
    },
  },
});
</script>
