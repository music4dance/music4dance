<template>
  <div>
    <b-alert v-if="isTest" show variant="warning" style="margin-bottom: 0"
      >This is a TEST site. Please navigate to
      <a href="https://www.music4dance.net" class="alert-link"
        >wwww.music4dance.net</a
      >
      to use the production site.</b-alert
    >
    <b-alert
      v-if="context.updateMessage"
      show
      variant="danger"
      style="margin-bottom: 0"
      >{{ context.updateMessage }}</b-alert
    >
    <b-navbar id="mainMenu" type="dark" variant="primary" toggleable="lg" fixed>
      <b-navbar-brand href="/">
        <img src="/images/header-logo.png" height="40" title="music4dance" />
      </b-navbar-brand>

      <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>

      <b-collapse id="nav-collapse" is-nav>
        <b-navbar-nav>
          <b-nav-item-dropdown id="music-menu" text="Music">
            <b-dropdown-item href="/dances">Dances</b-dropdown-item>
            <b-dropdown-item
              href="/dances/ballroom-competition-categories"
              class="nav-subitem"
              >Ballroom</b-dropdown-item
            >
            <b-dropdown-item href="/dances/latin" class="nav-subitem"
              >Latin</b-dropdown-item
            >
            <b-dropdown-item href="/dances/swing" class="nav-subitem"
              >Swing</b-dropdown-item
            >
            <b-dropdown-item href="/dances/tango" class="nav-subitem"
              >Tango</b-dropdown-item
            >
            <b-dropdown-item href="/song">Song Library</b-dropdown-item>
            <b-dropdown-item href="/song/advancedsearchform" class="nav-subitem"
              >Advanced Search</b-dropdown-item
            >
            <b-dropdown-item href="/song/augment" class="nav-subitem"
              >Add Song</b-dropdown-item
            >
            <b-dropdown-item href="/song/newmusic" class="nav-subitem"
              >New Music</b-dropdown-item
            >
            <b-dropdown-item href="/dances/wedding-music"
              >Wedding</b-dropdown-item
            >
            <b-dropdown-item href="/song/holidaymusic">Holiday</b-dropdown-item>
            <b-dropdown-item href="/song/holidaymusic?occassion=halloween"
              >Halloween</b-dropdown-item
            >
            <b-dropdown-item href="/tag">Tags</b-dropdown-item>
          </b-nav-item-dropdown>
          <b-nav-item-dropdown id="tools-menu" text="Tools">
            <b-dropdown-item href="/home/counter"
              >Tempo Counter</b-dropdown-item
            >
            <b-dropdown-item href="/home/tempi">Tempi (Tempos)</b-dropdown-item>
            <b-dropdown-item href="/song/advancedsearchform"
              >Advanced Search</b-dropdown-item
            >
            <b-dropdown-item
              v-if="context.isBeta || context.isAdmin"
              href="/home/spotifyexplorer"
              >Spotify Explorer (BETA)</b-dropdown-item
            >
          </b-nav-item-dropdown>
          <b-nav-item-dropdown text="Info">
            <b-dropdown-item :href="context.helpLink">Help</b-dropdown-item>
            <b-dropdown-item href="https://music4dance.blog/"
              >Blog</b-dropdown-item
            >
            <b-dropdown-item href="/home/faq">FAQ</b-dropdown-item>
            <b-dropdown-item href="/home/about"
              >About music4dance</b-dropdown-item
            >
            <b-dropdown-item href="/home/readinglist"
              >Reading List</b-dropdown-item
            >
            <b-dropdown-item href="/home/sitemap">Site Map</b-dropdown-item>
            <b-dropdown-item href="/home/privacypolicy"
              >Privacy Policy</b-dropdown-item
            >
            <b-dropdown-item href="/home/credits">Credits</b-dropdown-item>
          </b-nav-item-dropdown>
          <b-nav-item id="contribute-menu" right href="/home/contribute"
            >Contribute</b-nav-item
          >
          <b-nav-item-dropdown
            id="admin-menu"
            text="Admin"
            v-if="context.isAdmin"
          >
            <b-dropdown-item href="/admin">Index</b-dropdown-item>
            <b-dropdown-item href="/applicationusers">Users</b-dropdown-item>
            <b-dropdown-item href="/activitylog">Activity Log</b-dropdown-item>
            <b-dropdown-item href="/tag/list">Tags</b-dropdown-item>
            <b-dropdown-item href="/admin/diagnostics"
              >Diagnostics</b-dropdown-item
            >
            <b-dropdown-item href="/admin/initializationTasks"
              >Initialization and Cleanup</b-dropdown-item
            >
            <b-dropdown-item href="/admin/scraping">Scraping</b-dropdown-item>
            <b-dropdown-item href="/playlist">PlayLists</b-dropdown-item>
            <b-dropdown-item href="/Searches?showDetails=True&user=all"
              >Searches</b-dropdown-item
            >
            <b-dropdown-item href="/song/rawsearchform"
              >Raw Search</b-dropdown-item
            >
            <b-dropdown-item href="/admin/uploadbackup"
              >Uploads and Backups</b-dropdown-item
            >
          </b-nav-item-dropdown>
        </b-navbar-nav>

        <b-navbar-nav class="ml-auto">
          <b-nav-form @submit="search">
            <b-form-input
              v-model="searchString"
              size="sm"
              class="mr-sm-2"
              placeholder="Search"
              @input="checkServiceAndWarn"
            ></b-form-input>
            <b-button
              size="sm"
              class="my-2 my-sm-0"
              type="submit"
              :disabled="!searchString.trim()"
              >Search</b-button
            >
          </b-nav-form>
          <template v-if="context.userName">
            <b-nav-item-dropdown :html="profileHeader">
              <b-dropdown-item right href="/identity/account/manage"
                >My Profile</b-dropdown-item
              >
              <b-dropdown-item right :href="songLink">My Songs</b-dropdown-item>
              <b-dropdown-item right :href="searchLink"
                >My Searches</b-dropdown-item
              >
              <b-dropdown-item
                right
                href="javascript:document.getElementById('logoutForm').submit()"
                >Log out</b-dropdown-item
              >
            </b-nav-item-dropdown>
          </template>
          <template v-else>
            <b-nav-item right :href="accountLink('register')"
              >Register</b-nav-item
            >
            <b-nav-item right :href="accountLink('login')">Login</b-nav-item>
          </template>
        </b-navbar-nav>
      </b-collapse>
    </b-navbar>
    <b-alert
      v-if="showMarketing"
      show
      variant="success"
      style="margin-bottom: 0"
      dismissible
      @dismissed="onDismissed('marketing-acknowledged')"
      ><span v-html="context.marketingMessage"></span
    ></b-alert>
    <b-alert
      variant="warning"
      show
      dismissible
      v-if="showExpiration"
      style="margin-bottom: 0"
      @dismissed="onDismissed('renewal-acknowledged')"
    >
      Your premium subcription will expire in
      {{ Math.round(context.daysToExpiration || 0) }}
      day(s). Please
      <a href="/home/contribute" class="alert-link">click here</a> to renew.
      Thanks for your continued support.
    </b-alert>
    <form
      id="logoutForm"
      action="/identity/account/logout"
      method="post"
      style="height: 0"
    >
      <input
        name="__RequestVerificationToken"
        type="hidden"
        :value="context.xsrfToken"
      />
      <input type="hidden" name="returnUrl" value="/" />
      <button id="logout" type="submit" class="btn btn-link"></button>
    </form>
  </div>
</template>

<script lang="ts">
import DropTarget from "@/mix-ins/DropTarget";
import { MenuContext } from "@/model/MenuContext";
import "reflect-metadata";
import { PropType } from "vue";

const renewal = "renewal-acknowledged";
const marketing = "marketing-acknowledged";

export default DropTarget.extend({
  props: {
    context: { type: Object as PropType<MenuContext>, required: true },
  },
  data() {
    return new (class {
      searchString = "";
    })();
  },
  computed: {
    profileHeader(): string {
      const context = this.context;
      const index =
        context.hasRole("dbAdmin") && context.indexId
          ? ` (${context.indexId})`
          : "";
      return `${context.userName}${index} <img src="/images/swing-ui.png" alt="User Icon" height="30" width="30" />`;
    },

    songLink(): string {
      return `/users/info/${this.context.userName}`;
    },

    searchLink(): string {
      return `/searches/?user=${this.context.userName}`;
    },

    showExpiration(): boolean {
      return (
        this.context.daysToExpiration !== undefined &&
        this.context.daysToExpiration < 30 &&
        !sessionStorage.getItem(renewal)
      );
    },

    showMarketing(): boolean {
      return (
        !!this.context.marketingMessage && !sessionStorage.getItem(marketing)
      );
    },

    isTest(): boolean {
      return window.location.hostname.endsWith(".azurewebsites.net");
    },
  },
  methods: {
    accountLink(type: string): string {
      const url = window.location.pathname + window.location.search;
      return `/identity/account/${type}?returnUrl=${url}`;
    },
    onDismissed(name: string): void {
      sessionStorage.setItem(name, "true");
    },
    search(): void {
      event?.preventDefault();
      window.location.href = `/search?search=${encodeURIComponent(
        this.searchString
      )}`;
    },
  },
});
</script>
