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
    <v-tour
      v-if="tourSteps"
      name="defaultTour"
      :steps="tourSteps"
      :callbacks="tourCallbacks"
    ></v-tour>
  </div>
</template>

<script lang="ts">
import Loader from "@/components/Loader.vue";
import {
  getEnvironment,
  getTagDatabase,
} from "@/helpers/DanceEnvironmentManager";
import AdminTools from "@/mix-ins/AdminTools";
import { BreadCrumbItem } from "@/model/BreadCrumbItem";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { TagDatabase } from "@/model/TagDatabase";
import { TourManager } from "@/model/TourManager";
import { TourCallbacks } from "@/model/VueTour";
import { Component, Mixins, Prop } from "vue-property-decorator";
import MainMenu from "./MainMenu.vue";

@Component({
  components: {
    Loader,
    MainMenu,
  },
})
export default class Page extends Mixins(AdminTools) {
  @Prop() private id?: string;
  @Prop() private title?: string;
  @Prop() private help?: string;
  @Prop() private breadcrumbs?: BreadCrumbItem[];
  @Prop() private consumesEnvironment?: boolean;
  @Prop() private consumesTags?: boolean;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  @Prop() private tourSteps?: [] | undefined;

  private environment: DanceEnvironment = new DanceEnvironment();
  private tagDatabase: TagDatabase = new TagDatabase();

  private get year(): string {
    return new Date().getFullYear().toString();
  }

  protected get loaded(): boolean {
    let loaded = true;

    if (this.consumesEnvironment) {
      const stats = this.environment?.tree;
      loaded &&= !!stats && stats.length > 0;
    }

    if (this.consumesTags) {
      const tags = this.tagDatabase?.tags;
      loaded &&= !!tags && tags.length > 0;
    }

    return loaded;
  }

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private get tourCallbacks(): TourCallbacks {
    return {
      onSkip: this.skipTour,
      onFinish: this.finishTour,
    };
  }

  private skipTour(): void {
    const tourManager = this.tourManager;
    if (tourManager) {
      tourManager.skipTour(this.id!);
    }
  }

  private finishTour(): void {
    const tourManager = this.tourManager;
    if (tourManager) {
      tourManager.completeTour(this.id!);
    }
  }

  private async created() {
    if (this.consumesEnvironment) {
      this.environment = await getEnvironment();
      this.$emit("environment-loaded", this.environment);
    }
    if (this.consumesTags) {
      this.tagDatabase = await getTagDatabase();
      this.$emit("tag-database-loaded", this.tagDatabase);
    }

    // if (this.consumesEnvironment || this.consumesTags) {
    // }
  }

  private get tourManager(): TourManager | undefined {
    return undefined;
    //return this.tourSteps && this.id ? TourManager.loadTours() : undefined;
  }

  // TOURNEXT:
  //  Figure out a way to globally turn tours on and off
  //  Have a way to explicity invoke a tour
  //  Write an actual tour
  private mounted(): void {
    const tourManager = this.tourManager;
    if (tourManager && tourManager?.showTour(this.id!)) {
      this.$tours["defaultTour"].start();
    }
  }
}
</script>
