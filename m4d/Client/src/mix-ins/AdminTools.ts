import { MenuContext, MenuContextInterface } from "@/model/MenuContext";
import axios, { AxiosInstance } from "axios";
import { Component, Vue } from "vue-property-decorator";

declare const menuContext: MenuContextInterface;

@Component
export default class AdminTools extends Vue {
  protected get context(): MenuContext {
    return new MenuContext(menuContext);
  }

  protected get isAdmin(): boolean {
    return this.context.isAdmin;
  }

  protected get isPremium(): boolean {
    return this.context.isPremium;
  }

  protected get canEdit(): boolean {
    return this.context.canEdit;
  }

  protected get canTag(): boolean {
    return this.context.canTag;
  }

  protected get userName(): string | undefined {
    return this.context.userName;
  }

  protected get userId(): string | undefined {
    return this.context.userId;
  }

  protected get isAuthenticated(): boolean {
    return !!this.userName;
  }

  protected get xsrfToken(): string | undefined {
    return this.context.xsrfToken;
  }

  protected get axiosXsrf(): AxiosInstance {
    return axios.create({
      headers: { RequestVerificationToken: this.xsrfToken },
    });
  }

  protected hasRole(role: string): boolean {
    return this.context.hasRole(role);
  }

  protected getAccountLink(page: string): string {
    return `/identity/account/${page}?returnUrl=${window.location.pathname}?${window.location.search}`;
  }
}
