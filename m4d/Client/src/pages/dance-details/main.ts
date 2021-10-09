import { initialize } from "@/initialize";
import Vue from "vue";
import VueShowdown from "vue-showdown";
import App from "./App.vue";

Vue.use(VueShowdown, { flavor: "vanilla" });
initialize(App);
