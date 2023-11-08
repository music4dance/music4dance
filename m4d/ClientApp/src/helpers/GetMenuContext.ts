import { MenuContext, type MenuContextInterface } from "@/models/MenuContext";

declare const menuContext: MenuContextInterface;
let menuContextObject: MenuContext | null = null;

export function getMenuContext(): MenuContext {
  if (!menuContextObject) {
    menuContextObject = new MenuContext(menuContext);
  }
  return menuContextObject;
}
