<template>
  <div>
    <main-menu></main-menu>
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
      <div class="row">
        <div class="col">
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
      <form id="logoutForm" action="/identity/account/logout" method="post">
        <input
          name="__RequestVerificationToken"
          type="hidden"
          :value="context.xsrfToken"
        />
        <input type="hidden" name="returnUrl" value="/" />
        <button id="logout" type="submit" class="btn btn-link"></button>
      </form>
    </div>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import Loader from "@/components/Loader.vue";
import MainMenu from "./MainMenu.vue";
import { MenuContext } from "@/model/MenuContext";
import { BreadCrumbItem } from "@/model/BreadCrumbItem";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { getEnvironment } from "@/helpers/DanceEnvironmentManager";

declare const menuContext: MenuContext;

@Component({
  components: {
    Loader,
    MainMenu,
  },
})
export default class Page extends Vue {
  @Prop() private title: string | undefined;
  @Prop() private help: string | undefined;
  @Prop() private breadcrumbs: BreadCrumbItem[] | undefined;
  @Prop() private consumesEnvironment?: boolean;

  private environment: DanceEnvironment = new DanceEnvironment();

  private context: MenuContext = menuContext;

  private get year(): string {
    return new Date().getFullYear().toString();
  }

  public get loaded(): boolean {
    const stats = this.environment?.stats;
    const loaded = !!stats && stats.length > 0;
    return !this.consumesEnvironment || loaded;
  }

  private async created() {
    if (this.consumesEnvironment) {
      this.environment = await getEnvironment();

      this.$emit("environment-loaded", this.environment);

      // tslint:disable-next-line:no-console
      console.log(
        `Environment loaded: Stats = ${this.environment!.stats!.length}`
      );
    }
  }
}
</script>
