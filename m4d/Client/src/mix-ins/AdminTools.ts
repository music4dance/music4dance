import { MenuContext, MenuContextInterface } from "@/model/MenuContext";
import axios, { AxiosInstance } from "axios";
import Vue from "vue";

declare const menuContext: MenuContextInterface;

export default Vue.extend({
  computed: {
    context(): MenuContext {
      return new MenuContext(menuContext);
    },
    isAdmin(): boolean {
      return this.context.isAdmin;
    },
    isPremium(): boolean {
      return this.context.isPremium;
    },
    canEdit(): boolean {
      return this.context.canEdit;
    },
    canTag(): boolean {
      return this.context.canTag;
    },
    userName(): string | undefined {
      return this.context.userName;
    },
    userId(): string | undefined {
      return this.context.userId;
    },
    isAuthenticated(): boolean {
      return !!this.userName;
    },
    xsrfToken(): string | undefined {
      return this.context.xsrfToken;
    },
    axiosXsrf(): AxiosInstance {
      return axios.create({
        headers: { RequestVerificationToken: this.xsrfToken },
      });
    },
  },
  methods: {
    hasRole(role: string): boolean {
      return this.context.hasRole(role);
    },
    getAccountLink(page: string): string {
      return `/identity/account/${page}?returnUrl=${window.location.pathname}?${window.location.search}`;
    },
  },
});
