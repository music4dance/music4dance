import { BootstrapVue, BootstrapVueIcons } from "bootstrap-vue";
import Vue from "vue";
import App from "./App.vue";

Vue.config.productionTip = false;

Vue.use(BootstrapVue);
Vue.use(BootstrapVueIcons);

new Vue({
  render: (h) => h(App),
}).$mount("#app");
