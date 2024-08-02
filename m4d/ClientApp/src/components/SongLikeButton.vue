<script setup lang="ts">
import LikeButton from "@/components/LikeButton.vue";
import { Song } from "@/models/Song";
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    song: Song;
    user?: string;
    toggleBehavior?: boolean;
    scale?: number;
  }>(),
  {
    user: undefined,
    toggleBehavior: true,
    scale: 1,
  },
);

const emit = defineEmits<{ "click-like": [songId: string] }>();

const buttonId = `like-button-${props.song.songId}`;

const state = computed(() => {
  const user = props.user;
  if (!user) {
    return undefined;
  }
  const modified = props.song.getUserModified(user);
  return modified ? modified.like : undefined;
});

const redirect = (() => {
  const location = window.location;
  return `${location.pathname}${location.search}${location.hash}`;
})();

const onClick = (songId: string): void => {
  if (props.user) {
    emit("click-like", songId);
  } else {
    window.location.href = `/identity/account/login?returnUrl=${redirect}`;
  }
};
</script>

<template>
  <LikeButton
    :id="buttonId"
    :state="state"
    :authenticated="!!user"
    :title="song.title"
    :scale="scale"
    :toggle-behavior="toggleBehavior"
    @click-like="onClick"
  />
</template>
