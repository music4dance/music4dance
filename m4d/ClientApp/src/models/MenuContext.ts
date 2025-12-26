import type { AxiosInstance } from "axios";
import axios from "axios";

export interface MenuContextInterface {
  helpLink?: string;
  userName?: string;
  userId?: string;
  roles?: string[];
  indexId?: string;
  expiration?: Date;
  started?: Date;
  level?: string;
  hitCount?: number;
  customerReminder?: boolean;
  marketingMessage?: string;
  xsrfToken?: string;
  isLocal?: boolean;
  isTest?: boolean;
  isProduction?: boolean;
  searchHealthy?: boolean;
  databaseHealthy?: boolean;
  configurationHealthy?: boolean;
}

export class MenuContext implements MenuContextInterface {
  public helpLink?: string;
  public userName?: string;
  public userId?: string;
  public roles?: string[];
  public indexId?: string;
  public expiration?: Date;
  public started?: Date;
  public level?: string;
  public hitCount?: number;
  public customerReminder?: boolean;
  public marketingMessage?: string;
  public xsrfToken?: string;
  public searchHealthy?: boolean;
  public databaseHealthy?: boolean;
  public configurationHealthy?: boolean;
  private axiosInstance?: AxiosInstance;

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
    return timeDiff / (1000 * 60 * 60 * 24);
  }

  public hasRole(role: string): boolean {
    return !!this.roles?.includes(role);
  }

  public getAccountLink(page: string): string {
    return `/identity/account/${page}?returnUrl=${encodeURIComponent(window.location.pathname + window.location.search)}`;
  }

  public get axiosXsrf(): AxiosInstance {
    if (!this.axiosInstance) {
      this.axiosInstance = axios.create({
        headers: { RequestVerificationToken: this.xsrfToken },
      });
    }
    return this.axiosInstance;
  }

  public get isLocal(): boolean {
    return window.location.hostname === "localhost";
  }
  public get isTest(): boolean {
    return window.location.hostname.endsWith(".azurewebsites.net");
  }
  public get isProduction(): boolean {
    return window.location.hostname === "www.music4dance.net";
  }
}
