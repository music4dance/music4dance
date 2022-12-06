import { initializeAsync } from "@/initialize";
import { Preloads } from "@/Preloads";
import App from "./App.vue";

await initializeAsync(App, Preloads.All);
