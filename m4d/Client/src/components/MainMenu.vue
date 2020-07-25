<template>
    <b-navbar id="mainMenu" type="dark" variant="primary" toggleable="md" fixed>
        <b-navbar-brand href="/">
            <img src="/images/header-logo.png" height="40" title="music4dance" />
        </b-navbar-brand>

        <b-navbar-toggle target="nav-collapse"></b-navbar-toggle>

        <b-collapse id="nav-collapse" is-nav>
            <b-navbar-nav>
                <b-nav-item-dropdown text="Music">
                    <b-dropdown-item href="/dances">Dances</b-dropdown-item>
                        <b-dropdown-item href="/dances/ballroom-competition-categories" class="nav-subitem">Ballroom</b-dropdown-item>
                        <b-dropdown-item href="/dances/latin" class="nav-subitem">Latin</b-dropdown-item>
                        <b-dropdown-item href="/dances/swing" class="nav-subitem">Swing</b-dropdown-item>
                        <b-dropdown-item href="/dances/tango" class="nav-subitem">Tango</b-dropdown-item>
                    <b-dropdown-item href="/song">Song Library</b-dropdown-item>
                        <b-dropdown-item href="/song/advancedsearchform" class="nav-subitem">Advanced Search</b-dropdown-item>
                        <b-dropdown-item href="/song/augment" class="nav-subitem">Add Song</b-dropdown-item>
                        <b-dropdown-item href="/song/newmusic" class="nav-subitem">New Music</b-dropdown-item>
                    <b-dropdown-item href="/dances/wedding-music">Wedding</b-dropdown-item>
                    <b-dropdown-item href="/song/holidaymusic">Holiday</b-dropdown-item>
                    <b-dropdown-item href="/tag">Tags</b-dropdown-item>
                </b-nav-item-dropdown>
                <b-nav-item-dropdown text="Tools">
                    <b-dropdown-item href="/home/counter">Tempo Counter</b-dropdown-item>
                    <b-dropdown-item href="/home/tempi">Tempi (Tempos)</b-dropdown-item>
                </b-nav-item-dropdown>
                <b-nav-item-dropdown text="Info">
                    <b-dropdown-item :href="context.helpLink">Help</b-dropdown-item>
                    <b-dropdown-item href="https://music4dance.blog/">Blog</b-dropdown-item>
                    <b-dropdown-item href="/home/faq">FAQ</b-dropdown-item>
                    <b-dropdown-item href="/home/about">About Us</b-dropdown-item>
                    <b-dropdown-item href="https://music4dance.blog/reading-list/">Reading List</b-dropdown-item>
                    <b-dropdown-item href="/home/sitemap">Site Map</b-dropdown-item>
                    <b-dropdown-item href="/home/privacypolicy">Privacy Policy</b-dropdown-item>
                    <b-dropdown-item href="/home/credits">Credits</b-dropdown-item>
                </b-nav-item-dropdown>
                <b-nav-item right href="/home/contribute">Contribute</b-nav-item>
                <b-nav-item-dropdown text="Admin" v-if="context.isAdmin">
                    <b-dropdown-item href="/admin">Index</b-dropdown-item>
                    <b-dropdown-item href="/applicationusers">Users</b-dropdown-item>
                    <b-dropdown-item href="/tags">Tags</b-dropdown-item>
                    <b-dropdown-item href="/admin/diagnostics">Diagnostics</b-dropdown-item>
                    <b-dropdown-item href="/admin/initializationTasks">Initialization and Cleanup</b-dropdown-item>
                    <b-dropdown-item href="/admin/scraping">Scraping</b-dropdown-item>
                    <b-dropdown-item href="/playlist">PlayLists</b-dropdown-item>
                    <b-dropdown-item href="/admin/uploadbackup">Uploads and Backups</b-dropdown-item>
                </b-nav-item-dropdown>
            </b-navbar-nav>

            <b-navbar-nav class="ml-auto" v-if="context.userName">
                <b-nav-item-dropdown :html="profileHeader">
                    <b-dropdown-item right href="/identity/account/manage">My Profile</b-dropdown-item>
                    <b-dropdown-item right :href="songLink">My Songs</b-dropdown-item>
                    <b-dropdown-item right :href="searchLink">My Searches</b-dropdown-item>
                    <b-dropdown-item right href="javascript:document.getElementById('logoutForm').submit()">Log out</b-dropdown-item>
                </b-nav-item-dropdown>
            </b-navbar-nav>
            <b-navbar-nav class="ml-auto" v-else>
                <b-nav-item right href="/identity/account/register">Register</b-nav-item>
                <b-nav-item right href="/identity/account/login">Login</b-nav-item>
            </b-navbar-nav>
        </b-collapse>
    </b-navbar>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import { MenuContext } from '@/model/MenuContext';

declare const menuContext: MenuContext;

@Component
export default class Page extends Vue {
    private context = menuContext;

    private get profileHeader(): string {
        const context = this.context;
        const index = context.indexId ? ` (${context.indexId})` : '';
        return `${context.userName}${index} <img src="/images/swing-ui.png" alt="User Icon" height="30" width="30" />`;
        // return `${context.userName}${index}`;
    }

    private get songLink(): string {
        return `/song/filteruser/?user=${this.context.userName}`;
    }

    private get searchLink(): string {
        return `/song/searches/?user=${this.context.userName}`;
    }
}
</script>
