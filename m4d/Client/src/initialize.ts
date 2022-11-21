import { BootstrapVue, BootstrapVueIcons } from "bootstrap-vue";
import Vue, { VueConstructor } from "vue";
import VueMq from "vue-mq";
import VueTour from "vue-tour";
import { loadPreloads } from "./loadPreloads";
import { Preloads } from "./Preloads";

require("vue-tour/dist/vue-tour.css");

export async function initializeAsync(
  app: VueConstructor<Vue>,
  preloads?: Preloads
): Promise<void> {
  if (preloads) {
    await loadPreloads(preloads);
  }
  coreInitialize(app);
}

export function initialize(app: VueConstructor<Vue>): void {
  coreInitialize(app);
}

function coreInitialize(app: VueConstructor<Vue>): void {
  Vue.config.productionTip = false;

  Vue.use(VueTour);
  Vue.use(BootstrapVue);
  Vue.use(BootstrapVueIcons);
  Vue.use(VueMq, {
    breakpoints: {
      sm: 576,
      md: 768,
      lg: 992,
      xl: Infinity, // 1200?
    },
  });

  new Vue({
    render: (h) => h(app),
  }).$mount("#app");
}
