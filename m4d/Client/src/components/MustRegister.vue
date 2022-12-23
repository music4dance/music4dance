<template>
  <div v-if="!isAuthenticated">
    {{ title }}
    <ul>
      <li>
        <a :href="getAccountLink('register')">Click here to register.</a>
      </li>
      <li>
        If you're already registered,
        <a :href="getAccountLink('login')">click here to log in.</a>
      </li>
    </ul>
  </div>
</template>

<script lang="ts">
import AdminTools from "@/mix-ins/AdminTools";
import "reflect-metadata";

export default AdminTools.extend({
  components: {},
  props: { title: { type: String, required: true } },
});
</script>
