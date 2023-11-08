<template>
  <div v-if="!menuContext.isAuthenticated">
    {{ title }}
    <ul>
      <li>
        <a :href="menuContext.getAccountLink('register')">Click here to register.</a>
      </li>
      <li>
        If you're already registered,
        <a :href="menuContext.getAccountLink('login')">click here to log in.</a>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import type { MenuContext } from "@/models/MenuContext";

defineProps<{ title: string; menuContext: MenuContext }>();
</script>
