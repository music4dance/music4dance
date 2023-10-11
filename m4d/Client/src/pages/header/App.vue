<template>
  <main-menu :context="context"></main-menu>
</template>

<script lang="ts">
import MainMenu from "@/components/MainMenu.vue";
import AdminTools from "@/mix-ins/AdminTools";
import "reflect-metadata";

export default AdminTools.extend({
  components: { MainMenu },
});
</script>
