<script setup lang="ts">
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

const undefinedTip = `Click to add ${props.title} to favorites.`;
const trueTip = `${props.title} is in your favorites, click to ${props.toggleBehavior ? "move to your blocked list." : "change."}`;
const falseTip = `${props.title} is in your blocked list, click to ${props.toggleBehavior ? "remove it." : "change."}`;
const redirectUrl = `/identity/account/login?returnUrl=${window.location.pathname}${window.location.search}${window.location.hash}`;

const tip = (() => {
  if (!props.authenticated) {
    return "Log in to like/dislike this song.";
  }

  const state = props.state;
  if (state === undefined) {
    return undefinedTip;
  } else if (state) {
    return trueTip;
  } else {
    return falseTip;
  }
})();

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
    <a :id="id" href="#" role="button" @click.prevent="onClick">
      <LikeIcon :state="state" :scale="scale" />
    </a>
    <BTooltip :title="tip" triggers="hover" placement="right" :target="id" />
  </span>
</template>
