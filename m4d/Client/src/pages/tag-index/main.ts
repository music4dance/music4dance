import { Preloads } from "@/Preloads";
import { initializeAsync } from "@/initialize";
import App from "./App.vue";

await initializeAsync(App, Preloads.Tags);
