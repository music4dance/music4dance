import { Component, Vue } from "vue-property-decorator";
import { MenuContext, MenuContextInterface } from "@/model/MenuContext";

declare const menuContext: MenuContextInterface;

@Component
export default class AdminTools extends Vue {
  protected get context(): MenuContext {
    return new MenuContext(menuContext);
  }

  protected get isAdmin(): boolean {
    return this.context.isAdmin;
  }

  protected get canEdit(): boolean {
    return this.context.canEdit;
  }

  protected get userName(): string | undefined {
    return this.context.userName;
  }
}
