<template>
    <div>
        <main-menu></main-menu>
        <nav aria-label="breadcrumb" v-if="breadcrumbs">
            <b-breadcrumb :items="breadcrumbs" style="padding: .25rem .5rem"></b-breadcrumb>
        </nav>
        <div id="body-content" class="container-fluid body-content">
            <h1 v-if="title">{{title}}</h1>
            <div v-else class="mt-2"></div>
            <slot></slot>
            <div class="row">
                <div class="col">
                    <hr />
                    <footer>
                        <p>
                            &copy; {{year}} - <a href="https://www.music4dance.net">Music4Dance.net</a>  -
                            <a href="https://www.music4dance.net/home/sitemap">Site Map</a> -
                            <a href="https://www.music4dance.net/home/termsofservice">Terms of Service</a> -
                            <a href="https://www.music4dance.net/home/privacypolicy">Privacy Policy</a> -
                            <a href="https://www.music4dance.net/home/credits">Credits</a> -
                            <a :href="context.helpLink">Help</a>
                        </p>
                    </footer>
                </div>
            </div>
            <form id="logoutForm" action="/identity/account/logout" method="post">
                <input name="__RequestVerificationToken" type="hidden" :value="context.xsrfToken">
                <input type="hidden" name="returnUrl" value="/">
                <button id="logout" type="submit" class="btn btn-link"></button>
            </form>
        </div>
    </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import MainMenu from './MainMenu.vue';
import { MenuContext } from '@/model/MenuContext';
import { BreadCrumbItem } from '@/model/BreadCrumbItem';

declare const menuContext: MenuContext;

@Component({
  components: {
      MainMenu,
  },
})
export default class Page extends Vue {
    @Prop() private title: string | undefined;
    @Prop() private help: string | undefined;
    @Prop() private breadcrumbs: BreadCrumbItem[] | undefined;

    private context: MenuContext = menuContext;

    private get year(): string {
        return new Date().getFullYear().toString();
    }
}
</script>
