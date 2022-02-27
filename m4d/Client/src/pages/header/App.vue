<template>
  <main-menu :context="context"></main-menu>
</template>

<script lang="ts">
import MainMenu from "@/components/MainMenu.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { BreadCrumbItem, infoTrail } from "@/model/BreadCrumbItem";
import "reflect-metadata";
import { Component, Mixins } from "vue-property-decorator";

@Component({ components: { MainMenu } })
export default class App extends Mixins(AdminTools) {
  private breadcrumbs: BreadCrumbItem[] = [
    ...infoTrail,
    { text: "FAQ", active: true },
  ];
}
</script>
