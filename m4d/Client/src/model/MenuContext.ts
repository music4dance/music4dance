export interface MenuContextInterface {
  helpLink?: string;
  userName?: string;
  roles?: string[];
  indexId?: string;
  xsrfToken?: string;
}

export class MenuContext implements MenuContextInterface {
  public helpLink?: string;
  public userName?: string;
  public roles?: string[];
  public indexId?: string;
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

  public hasRole(role: string): boolean {
    return !!this.roles?.includes(role);
  }
}
