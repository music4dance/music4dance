<script setup lang="ts">
import { computed } from "vue";
import { getId } from "@/helpers/ObjectHelpers";

const props = defineProps<{
  vote?: boolean;
  value: number;
  redirectUrl: string;
  authenticated: boolean;
  mainTip: string;
  signInTip: string;
  upTipVote: string;
  upTipVoted: string;
  downTipVote: string;
  downTipVoted: string;
}>();

const emit = defineEmits<{
  "up-vote": [];
  "down-vote": [];
}>();

const upClass = computed(() => (props.vote ? ["voted-up"] : ["vote-up"]));
const downClass = computed(() => (props.vote === false ? ["voted-down"] : ["vote-down"]));
const upTip = computed(() =>
  props.authenticated ? (props.vote ? props.upTipVoted : props.upTipVote) : props.signInTip,
);
const downTip = computed(() =>
  props.authenticated
    ? props.vote === false
      ? props.downTipVoted
      : props.downTipVote
    : props.signInTip,
);

const login = () => {
  window.location.href = props.redirectUrl;
};

const upClick = async () => {
  if (props.authenticated) {
    emit("up-vote");
  } else {
    login();
  }
};

const downClick = async () => {
  if (props.authenticated) {
    emit("down-vote");
  } else {
    login();
  }
};
</script>

<template>
  <div class="vote-container">
    <div
      v-b-tooltip.hover.right="{ title: upTip, id: getId() }"
      :class="upClass"
      @click="upClick"
    />
    <div v-b-tooltip.hover.right="{ title: mainTip, id: getId() }" class="vote-number">
      {{ value }}
    </div>
    <div
      v-b-tooltip.hover.right="{ title: downTip, id: getId() }"
      :class="downClass"
      @click="downClick"
    />
  </div>
</template>

<style lang="scss" scoped>
.vote-container {
  display: inline-block;
}

.voted-up {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-bottom: 0.75em solid black;
}

.voted-down {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-top: 0.75em solid black;
}

.vote-up {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-bottom: 0.75em solid gray;
}

.vote-down {
  width: 0;
  height: 0;
  border-left: 0.75em solid transparent;
  border-right: 0.75em solid transparent;
  border-top: 0.75em solid gray;
}

.vote-number {
  text-align: center;
}
</style>
