<script setup lang="ts">
import { MenuContext } from "@/models/MenuContext";
import { computed, ref } from "vue";
import { checkServiceAndWarn } from "@/helpers/DropTarget";
import logo from "@/assets/images/header-logo.png";
import dancers from "@/assets/images/swing-ui.png";

const renewalTag = "renewal-acknowledged";
const marketingTag = "marketing-acknowledged";

const props = defineProps<{ context: MenuContext }>();
const searchString = ref<string>("");

const renewal = ref(!sessionStorage.getItem(renewalTag));
const marketing = ref(!sessionStorage.getItem(marketingTag));

const songIndex = computed(() => {
  const context = props.context;
  return context.isAdmin && context.indexId ? ` (${context.indexId})` : "";
});

const helpLink = computed(() => {
  return props.context.helpLink ?? "https://music4dance.blog/music4dance-help/";
});

const songLink = computed(() => {
  return `/users/info/${props.context.userName}`;
});

const searchLink = computed(() => {
  return `/searches/?user=${props.context.userName}`;
});

const showExpiration = computed(() => {
  return (
    props.context.daysToExpiration !== undefined &&
    props.context.daysToExpiration < 30 &&
    !sessionStorage.getItem(renewalTag)
  );
});

const showMarketing = computed(() => {
  return (
    props.context.marketingMessage &&
    props.context.marketingMessage.length > 0 &&
    !sessionStorage.getItem(marketingTag)
  );
});

const isTest = computed(() => {
  return window.location.hostname.endsWith(".azurewebsites.net");
});

function accountLink(type: string): string {
  const url = window.location.pathname + window.location.search;
  return `/identity/account/${type}?returnUrl=${url}`;
}

function onDismissed(target: string): void {
  sessionStorage.setItem(target, "true");
}

function search(): void {
  event?.preventDefault();
  window.location.href = `/search?search=${encodeURIComponent(searchString.value)}`;
}
</script>

<template>
  <div>
    <BAlert v-if="isTest" :model-value="true" variant="warning" style="margin-bottom: 0"
      >This is a TEST site. Please navigate to
      <a href="https://www.music4dance.net" class="alert-link">wwww.music4dance.net</a>
      to use the production site.</BAlert
    >
    <BAlert
      v-if="context.updateMessage"
      v-model="renewal"
      variant="danger"
      style="margin-bottom: 0"
      dismissable
      >{{ context.updateMessage }}</BAlert
    >
    <BNavbar id="mainMenu" data-bs-theme="dark" variant="primary" toggleable="lg">
      <BNavbarBrand href="/">
        <img :src="logo" height="40" title="music4dance" />
      </BNavbarBrand>

      <BNavbarToggle id="drop-toggle" target="nav-collapse"></BNavbarToggle>

      <BCollapse id="nav-collapse" is-nav>
        <BNavbarNav>
          <BNavItemDropdown id="music-menu" text="Music">
            <BDropdownItem href="/dances">Dances</BDropdownItem>
            <BDropdownItem href="/dances/ballroom-competition-categories" class="nav-subitem"
              >&nbsp;&nbsp;Ballroom</BDropdownItem
            >
            <BDropdownItem href="/dances/latin" class="nav-subitem"
              >&nbsp;&nbsp;Latin</BDropdownItem
            >
            <BDropdownItem href="/dances/swing" class="nav-subitem"
              >&nbsp;&nbsp;Swing</BDropdownItem
            >
            <BDropdownItem href="/dances/tango" class="nav-subitem"
              >&nbsp;&nbsp;Tango</BDropdownItem
            >
            <BDropdownItem href="/song">Song Library</BDropdownItem>
            <BDropdownItem href="/song/advancedsearchform" class="nav-subitem"
              >&nbsp;&nbsp;Advanced Search</BDropdownItem
            >
            <BDropdownItem href="/song/augment" class="nav-subitem"
              >&nbsp;&nbsp;Add Song</BDropdownItem
            >
            <BDropdownItem href="/song/newmusic" class="nav-subitem"
              >&nbsp;&nbsp;New Music</BDropdownItem
            >
            <BDropdownItem href="/dances/wedding-music">Wedding</BDropdownItem>
            <BDropdownItem href="/song/holidaymusic">Holiday</BDropdownItem>
            <BDropdownItem href="/song/holidaymusic?occassion=halloween">Halloween</BDropdownItem>
            <BDropdownItem href="/tag">Tags</BDropdownItem>
          </BNavItemDropdown>
          <BNavItemDropdown id="tools-menu" text="Tools">
            <BDropdownItem href="/home/counter">Tempo Counter</BDropdownItem>
            <BDropdownItem href="/home/tempi">Tempi (Tempos)</BDropdownItem>
            <BDropdownItem href="/song/advancedsearchform">Advanced Search</BDropdownItem>
            <BDropdownItem v-if="context.isBeta || context.isAdmin" href="/home/spotifyexplorer"
              >Spotify Explorer (BETA)</BDropdownItem
            >
          </BNavItemDropdown>
          <BNavItemDropdown id="info" text="Info">
            <BDropdownItem :href="helpLink">Help</BDropdownItem>
            <BDropdownItem href="https://music4dance.blog/">Blog</BDropdownItem>
            <BDropdownItem href="/home/faq">FAQ</BDropdownItem>
            <BDropdownItem href="/home/about">About</BDropdownItem>
            <BDropdownItem href="/home/readinglist">Reading List</BDropdownItem>
            <BDropdownItem href="/home/sitemap">Site Map</BDropdownItem>
            <BDropdownItem href="/home/privacypolicy">Privacy Policy</BDropdownItem>
            <BDropdownItem href="/home/credits">Credits</BDropdownItem>
          </BNavItemDropdown>
          <BNavItem id="contribute-menu" right href="/home/contribute">Contribute</BNavItem>
          <BNavItemDropdown id="admin-menu" text="Admin" v-if="context.isAdmin">
            <BDropdownItem href="/admin">Index</BDropdownItem>
            <BDropdownItem href="/applicationusers">Users</BDropdownItem>
            <BDropdownItem href="/activitylog">Activity Log</BDropdownItem>
            <BDropdownItem href="/tag/list">Tags</BDropdownItem>
            <BDropdownItem href="/admin/diagnostics">Diagnostics</BDropdownItem>
            <BDropdownItem href="/admin/initializationTasks"
              >Initialization and Cleanup</BDropdownItem
            >
            <BDropdownItem href="/admin/scraping">Scraping</BDropdownItem>
            <BDropdownItem href="/playlist">PlayLists</BDropdownItem>
            <BDropdownItem href="/Searches?showDetails=True&user=all">Searches</BDropdownItem>
            <BDropdownItem href="/song/rawsearchform">Raw Search</BDropdownItem>
            <BDropdownItem href="/admin/uploadbackup">Uploads and Backups</BDropdownItem>
          </BNavItemDropdown>
        </BNavbarNav>

        <BNavbarNav class="ms-auto">
          <BNavForm id="search" @submit="search">
            <BFormInput
              id="search-text"
              v-model="searchString"
              size="sm"
              class="me-sm-2"
              placeholder="Search"
              @input="checkServiceAndWarn"
            ></BFormInput>
            <BButton
              size="sm"
              class="mx-2 my-2 my-sm-0"
              type="submit"
              :disabled="!searchString.trim()"
              >Search</BButton
            >
          </BNavForm>
          <template v-if="context.userName">
            <BNavItemDropdown>
              <template #button-content>
                {{ context.userName }} {{ songIndex }}
                <img :src="dancers" alt="User Icon" height="30" width="30" />
              </template>
              <BDropdownItem right href="/identity/account/manage">My Profile</BDropdownItem>
              <BDropdownItem right :href="songLink">My Songs</BDropdownItem>
              <BDropdownItem right :href="searchLink">My Searches</BDropdownItem>
              <BDropdownItem right href="javascript:document.getElementById('logoutForm').submit()"
                >Log out</BDropdownItem
              >
            </BNavItemDropdown>
          </template>
          <template v-else>
            <BNavItem right :href="accountLink('register')">Register</BNavItem>
            <BNavItem right :href="accountLink('login')">Login</BNavItem>
          </template>
        </BNavbarNav>
      </BCollapse>
    </BNavbar>
    <BAlert
      v-if="showMarketing"
      v-model="marketing"
      variant="success"
      dismissible
      style="margin-bottom: 0"
      @update:modelValue="onDismissed(marketingTag)"
      ><span v-html="context.marketingMessage"></span
    ></BAlert>
    <BAlert
      v-if="showExpiration"
      variant="warning"
      v-model="renewal"
      dismissible
      style="margin-bottom: 0"
      @update:modelValue="onDismissed(renewalTag)"
    >
      Your premium subcription will expire in
      {{ Math.round(context.daysToExpiration || 0) }}
      day(s). Please
      <a href="/home/contribute" class="alert-link">click here</a> to renew. Thanks for your
      continued support.
    </BAlert>
    <form id="logoutForm" action="/identity/account/logout" method="post" style="height: 0">
      <input name="__RequestVerificationToken" type="hidden" :value="context.xsrfToken" />
      <input type="hidden" name="returnUrl" value="/" />
      <button id="logout" type="submit" class="btn btn-link"></button>
    </form>
  </div>
</template>
