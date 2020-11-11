import Vue from "vue";
import VueMq from "vue-mq";
import { BootstrapVue, BootstrapVueIcons } from "bootstrap-vue";
import VueShowdown from "vue-showdown";
import App from "./App.vue";

Vue.config.productionTip = false;

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
Vue.use(VueShowdown, { flavor: "vanilla" });

new Vue({
  render: (h) => h(App),
}).$mount("#app");
