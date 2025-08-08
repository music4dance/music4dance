<script setup lang="ts">
import { MenuContext } from "@/models/MenuContext";
import { computed, ref, useTemplateRef } from "vue";
import logo from "@/assets/images/header-logo.png";
import dancers from "@/assets/images/swing-ui.png";
import { BToastOrchestrator } from "bootstrap-vue-next";
import { onClickOutside } from "@vueuse/core";

const renewalTag = "renewal-acknowledged";
const marketingTag = "marketing-acknowledged";
const customerReminder = "reminder-acknowledged";

const props = defineProps<{ context: MenuContext }>();

const isNavExpanded = ref(false);
const collapseElement = useTemplateRef<HTMLElement>("collapse-element");
const navToggle = useTemplateRef<HTMLElement>("nav-toggle");

onClickOutside(
  collapseElement,
  () => {
    if (isNavExpanded.value) {
      isNavExpanded.value = false;
    }
  },
  { ignore: [navToggle] },
);

const searchString = ref<string>("");

const reminderAcknowledged = () => {
  const ack = sessionStorage.getItem(customerReminder);
  if (!ack) {
    return false;
  }

  const timestamp = parseInt(ack);
  if (isNaN(timestamp)) {
    return false;
  }

  const now = Date.now();
  const delta = now - timestamp;
  return delta < 60 * 60 * 1000; // 1 hour
};

const renewal = ref(!sessionStorage.getItem(renewalTag));
const showReminder = ref(props.context.customerReminder && !reminderAcknowledged());
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
    (props.context.daysToExpiration < 30 || props.context.daysToExpiration < 0) &&
    !sessionStorage.getItem(renewalTag)
  );
});

const expired = computed(() => {
  return props.context.daysToExpiration !== undefined && props.context.daysToExpiration < 0;
});

const absoluteExpiration = computed(() => {
  return Math.abs(Math.round(props.context.daysToExpiration || 0));
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

function onReminderDismissed(): void {
  sessionStorage.setItem(customerReminder, Date.now().toString());
}

function search(s?: string): void {
  if (s) {
    window.location.href = `/search?search=${encodeURIComponent(s)}`;
  }
}
</script>

<template>
  <div>
    <BModalOrchestrator />
    <BToastOrchestrator />
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
        <img :src="logo" height="40" width="170" title="music4dance" />
      </BNavbarBrand>

      <BNavbarToggle id="drop-toggle" ref="nav-toggle" target="nav-collapse" />

      <BCollapse id="nav-collapse" ref="collapse-element" v-model="isNavExpanded" is-nav>
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
            <BDropdownItem href="/dances/country" class="nav-subitem"
              >&nbsp;&nbsp;Country</BDropdownItem
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
            <BDropdownItem href="/customsearch?name=holiday">Holiday</BDropdownItem>
            <BDropdownItem href="/customsearch?name=halloween">Halloween</BDropdownItem>
            <BDropdownItem href="/customsearch?name=broadway">Broadway</BDropdownItem>
            <BDropdownItem href="/tag">Tags</BDropdownItem>
            <BDropdownItem
              v-if="context.isAdmin"
              href="/Song/filtersearch?filter=Advanced----------3"
              >All Songs</BDropdownItem
            >
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
            <BDropdownItem href="/home/technicalblog/">Technical Blog</BDropdownItem>
            <BDropdownItem href="/home/sitemap">Site Map</BDropdownItem>
            <BDropdownItem href="/home/privacypolicy">Privacy Policy</BDropdownItem>
            <BDropdownItem href="/home/credits">Credits</BDropdownItem>
          </BNavItemDropdown>
          <BNavItem id="contribute-menu" right href="/home/contribute">Contribute</BNavItem>
          <BNavItemDropdown v-if="context.isAdmin" id="admin-menu" text="Admin">
            <BDropdownItem href="/admin">Index</BDropdownItem>
            <BDropdownItem href="/applicationusers">Users</BDropdownItem>
            <BDropdownItem href="/activitylog">Activity Log</BDropdownItem>
            <BDropdownItem href="/usagelog">Usage Log</BDropdownItem>
            <BDropdownItem href="/tag/list">Tags</BDropdownItem>
            <BDropdownItem href="/admin/diagnostics">Diagnostics</BDropdownItem>
            <BDropdownItem href="/admin/initializationTasks"
              >Initialization and Cleanup</BDropdownItem
            >
            <BDropdownItem href="/playlist">PlayLists</BDropdownItem>
            <BDropdownItem href="/Searches?showDetails=True&user=all">Searches</BDropdownItem>
            <BDropdownItem href="/song/rawsearchform">Raw Search</BDropdownItem>
            <BDropdownItem href="/admin/uploadbackup">Uploads and Backups</BDropdownItem>
          </BNavItemDropdown>
        </BNavbarNav>

        <BNavbarNav class="ms-auto">
          <BNavForm id="search">
            <BInputGroup>
              <SuggestionEntry id="search-text" v-model="searchString" size="sm" @search="search" />
            </BInputGroup>
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
      id="marketing-alert"
      v-model="marketing"
      variant="success"
      dismissible
      style="margin-bottom: 0"
      @hidden="onDismissed(marketingTag)"
      ><span v-html="context.marketingMessage"
    /></BAlert>
    <BAlert
      v-if="showExpiration"
      id="expiration-alert"
      v-model="renewal"
      variant="warning"
      dismissible
      style="margin-bottom: 0"
      @hidden="onDismissed(renewalTag)"
    >
      Your premium subcription {{ expired ? "expired" : "will expire in" }}
      {{ absoluteExpiration }}
      day(s) {{ expired ? "ago" : "" }}. Please
      <a href="/home/contribute" class="alert-link">click here</a> to renew. Thanks for your
      continued support.
    </BAlert>
    <BAlert
      v-else-if="showReminder"
      id="premium-alert"
      show
      variant="warning"
      dismissible
      style="margin-bottom: 0"
      @hidden="onReminderDismissed()"
    >
      <p>
        The core <b>music4dance.net</b> service is free, but it does cost money to run. If you find
        this site useful, please consider contributing to help keep it running. Your contribution
        will help cover the costs of hosting and maintenance give you access to some
        <a href="https://music4dance.blog/music4dance-help/subscriptions/">exclusive features</a>.
        Thank you for your support!
      </p>
      <BButton href="/home/contribute" variant="primary" size="sm">Contribute</BButton>
    </BAlert>
    <form id="logoutForm" action="/identity/account/logout" method="post" style="height: 0">
      <input name="__RequestVerificationToken" type="hidden" :value="context.xsrfToken" />
      <input type="hidden" name="returnUrl" value="/" />
      <button id="logout" type="submit" class="btn btn-link" />
    </form>
  </div>
</template>
