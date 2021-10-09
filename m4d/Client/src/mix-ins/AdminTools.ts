import { MenuContext, MenuContextInterface } from "@/model/MenuContext";
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

  protected hasRole(role: string): boolean {
    return this.context.hasRole(role);
  }
}
