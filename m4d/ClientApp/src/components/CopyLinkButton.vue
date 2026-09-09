<script setup lang="ts">
import { useToast, type ButtonVariant, type Size } from "bootstrap-vue-next";

// Defaults to copying the current page's own address (kept live by useUrlQuerySync on pages that
// use it) - pass `url` only when the link worth sharing isn't the current page's own address, e.g.
// Advanced Search's form wants to share the results link, not the form's own URL.
const props = withDefaults(
  defineProps<{
    url?: string;
    label?: string;
    variant?: ButtonVariant;
    size?: Size;
  }>(),
  { url: undefined, label: "Copy Link", variant: "outline-secondary", size: "sm" },
);

const { create: createToast } = useToast();

async function copyLink(): Promise<void> {
  const url = props.url ?? window.location.href;
  try {
    await navigator.clipboard.writeText(url);
    createToast({
      props: {
        title: "Link Copied",
        body: "This link has been copied to your clipboard.",
        variant: "success",
      },
    });
  } catch {
    createToast({
      props: {
        title: "Couldn't Copy Link",
        body: "Copy it from the address bar instead.",
        variant: "danger",
      },
    });
  }
}
</script>

<template>
  <BButton :variant="variant" :size="size" @click="copyLink">
    <IBiLink45deg aria-hidden="true" />
    {{ label }}
  </BButton>
</template>
