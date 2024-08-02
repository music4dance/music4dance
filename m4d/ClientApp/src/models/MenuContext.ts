import type { AxiosInstance } from "axios";
import axios from "axios";

export interface MenuContextInterface {
  helpLink?: string;
  userName?: string;
  userId?: string;
  roles?: string[];
  indexId?: string;
  expiration?: Date;
  updateMessage?: string;
  marketingMessage?: string;
  xsrfToken?: string;
}

export class MenuContext implements MenuContextInterface {
  public helpLink?: string;
  public userName?: string;
  public userId?: string;
  public roles?: string[];
  public indexId?: string;
  public expiration?: Date;
  public updateMessage?: string;
  public marketingMessage?: string;
  public xsrfToken?: string;

  public constructor(init?: MenuContextInterface) {
    Object.assign(this, init);
  }

  public get isAdmin(): boolean {
    return !!this.roles?.find((r) => r === "dbAdmin");
  }

  public get isPremium(): boolean {
    return !!this.roles?.find((r) => r === "premium" || r === "trial");
  }

  public get canTag(): boolean {
    return !!this.roles?.find((r) => r === "canTag");
  }

  public get canEdit(): boolean {
    return !!this.roles?.find((r) => r === "canEdit");
  }

  public get isBeta(): boolean {
    return !!this.roles?.find((r) => r === "beta");
  }

  public get isAuthenticated(): boolean {
    return !!this.userName;
  }

  public get daysToExpiration(): number | undefined {
    if (!this.expiration) {
      return undefined;
    }
    const timeDiff = this.expiration.getTime() - Date.now();
    const dayDiff = timeDiff / (1000 * 60 * 60 * 24);
    return dayDiff > 0 ? dayDiff : 0;
  }

  public hasRole(role: string): boolean {
    return !!this.roles?.includes(role);
  }

  public getAccountLink(page: string): string {
    return `/identity/account/${page}?returnUrl=${window.location.pathname}?${window.location.search}`;
  }

  public get axiosXsrf(): AxiosInstance {
    return axios.create({
      headers: { RequestVerificationToken: this.xsrfToken },
    });
  }
}
