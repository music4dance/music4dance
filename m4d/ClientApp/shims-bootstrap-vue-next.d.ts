// shims-bootstrap-vue-next.d.ts

// INT-TODO: This file doesn't appear to have any effect
import "bootstrap-vue-next";

declare module "bootstrap-vue-next/dist/src/types" {
  export interface BaseColorVariant {
    music: unknown;
    tempo: unknown;
    style: unknown;
    other: unknown;
    tools: unknown;
    blog: unknown;
    dance: unknown;
  }

  export interface BaseButtonVariant {
    "outline-style": unknown;
    "outline-tempo": unknown;
    "outline-music": unknown;
    "outline-other": unknown;
    "outline-dance": unknown;
  }
  export interface BaseTextColorVariant {}
}
