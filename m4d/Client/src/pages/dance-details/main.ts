import Vue from "vue";
import { initialize } from "@/initialize";
import VueShowdown from "vue-showdown";
import App from "./App.vue";

Vue.use(VueShowdown, { flavor: "vanilla" });
initialize(App);
