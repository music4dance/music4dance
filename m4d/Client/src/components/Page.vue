<template>
  <div>
    <main-menu :context="context"></main-menu>
    <nav aria-label="breadcrumb" v-if="breadcrumbs">
      <b-breadcrumb
        :items="breadcrumbs"
        style="padding: 0.25rem 0.5rem"
      ></b-breadcrumb>
    </nav>
    <div id="body-content" class="container-fluid body-content">
      <h1 v-if="title">{{ title }}</h1>
      <div v-else class="mt-2"></div>
      <loader :loaded="loaded">
        <slot></slot>
      </loader>
    </div>
    <div id="footer-content">
      <hr />
      <footer>
        <p>
          &copy; {{ year }} -
          <a href="https://www.music4dance.net">Music4Dance.net</a> -
          <a href="https://www.music4dance.net/home/sitemap">Site Map</a> -
          <a href="https://www.music4dance.net/home/termsofservice"
            >Terms of Service</a
          >
          -
          <a href="https://www.music4dance.net/home/privacypolicy"
            >Privacy Policy</a
          >
          - <a href="https://www.music4dance.net/home/credits">Credits</a> -
          <a :href="context.helpLink">Help</a>
        </p>
      </footer>
    </div>
  </div>
</template>

<script lang="ts">
import Loader from "@/components/Loader.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { BreadCrumbItem } from "@/model/BreadCrumbItem";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { TagDatabase } from "@/model/TagDatabase";
import { PropType } from "vue";
import MainMenu from "./MainMenu.vue";

export default AdminTools.extend({
  components: { Loader, MainMenu },
  props: {
    id: String,
    title: String,
    help: String,
    breadcrumbs: Array as PropType<BreadCrumbItem[]>,
  },
  data() {
    return new (class {
      environment: DanceEnvironment = new DanceEnvironment();
      tagDatabase: TagDatabase = new TagDatabase();
    })();
  },
  computed: {
    loaded(): boolean {
      return true;
    },
    year(): string {
      return new Date().getFullYear().toString();
    },
  },
  created() {
    this.$emit("loaded");
  },
});
</script>
