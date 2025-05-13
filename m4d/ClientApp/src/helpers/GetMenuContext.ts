import { MenuContext } from "@/models/MenuContext";
import type { AxiosInstance } from "axios";

declare global {
  interface Window {
    menuContext: MenuContext;
  }
}

let menuContextObject: MenuContext | null = null;

export function getMenuContext(): MenuContext {
  if (!menuContextObject) {
    menuContextObject = window.menuContext
      ? new MenuContext(window.menuContext)
      : new MenuContext();
  }
  return menuContextObject;
}

export function getAxiosXsrf(): AxiosInstance {
  return getMenuContext().axiosXsrf;
}
