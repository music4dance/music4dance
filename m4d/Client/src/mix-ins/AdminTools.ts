import { Component, Vue } from "vue-property-decorator";
import { MenuContext } from "@/model/MenuContext";

declare const menuContext: MenuContext;

@Component
export default class AdminTools extends Vue {
  protected get context(): MenuContext {
    return menuContext;
  }

  protected get isAdmin(): boolean {
    return !!this.context.isAdmin;
  }

  protected get userName(): string | undefined {
    return this.context.userName;
  }
}
