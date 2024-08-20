<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{ id: string }>();

const register = computed(() => {
  return authenticate("register");
});

const login = computed(() => {
  return authenticate("login");
});

const authenticate = (type: string): string => {
  const idParam = props.id ? `?id=${props.id}` : "";
  return `/identity/account/${type}?returnUrl=/song/augment${idParam}`;
};
</script>

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
