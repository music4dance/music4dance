<script setup lang="ts">
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    id: string;
    state?: boolean;
    authenticated: boolean;
    title: string;
    toggleBehavior?: boolean;
    scale?: number;
  }>(),
  {
    toggleBehavior: true,
    scale: 1,
  },
);

const emit = defineEmits(["click-like"]);

const redirectUrl = `/identity/account/login?returnUrl=${window.location.pathname}${window.location.search}${window.location.hash}`;

const tip = computed(() => {
  if (!props.authenticated) {
    return "Log in to like/dislike this song.";
  }

  const state = props.state;
  if (state === undefined) {
    return `Click to add ${props.title} to favorites.`;
  } else if (state) {
    return `${props.title} is in your favorites, click to ${props.toggleBehavior ? "move to your blocked list." : "change."}`;
  } else {
    return `${props.title} is in your blocked list, click to ${props.toggleBehavior ? "remove it." : "change."}`;
  }
});

const onClick = async (): Promise<void> => {
  if (props.authenticated) {
    emit("click-like");
  } else {
    window.location.href = redirectUrl;
  }
};
</script>

<template>
  <span>
    <a :id="id" href="#" role="button" :aria-label="tip" @click.prevent="onClick">
      <LikeIcon :state="state" :scale="scale" />
    </a>
    <BTooltip :title="tip" triggers="hover" placement="right" :target="id" />
  </span>
</template>
