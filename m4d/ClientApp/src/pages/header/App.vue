<script setup lang="ts">
import MainMenu from "@/components/MainMenu.vue";
import { MenuContext } from "@/models/MenuContext";
import { getMenuContext } from "@/helpers/GetMenuContext";

const context: MenuContext = getMenuContext();
</script>

<template>
  <header>
    <MainMenu :context="context" />
  </header>
  <body></body>
</template>
