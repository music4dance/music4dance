<script setup lang="ts">
import { computed } from "vue";
import VoteButton from "./VoteButton.vue";

const props = defineProps<{
  vote?: boolean;
  value: number;
  maxVote: number;
  authenticated: boolean;
  danceName: string;
}>();

const emit = defineEmits<{
  "up-vote": [];
  "down-vote": [];
}>();
const location = window.location;

const upTipVote = computed(
  () => `Click here to vote for this song being danceable ${props.danceName}`,
);
const upTipVoted = computed(
  () => `You have voted that this song is danceable to ${props.danceName}`,
);
const downTipVote = computed(
  () => `Click here to vote that this song is not danceable to ${props.danceName}`,
);
const downTipVoted = computed(
  () => `You have voted that this song is not danceable to ${props.danceName}`,
);
const numberTip = computed(
  () =>
    `This song has ${props.value} votes. The most popular ${props.danceName} has ${props.maxVote} votes`,
);
const redirectUrl = computed(
  () => `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`,
);
</script>

<template>
  <VoteButton
    :vote="vote"
    :value="value"
    :redirect-url="redirectUrl"
    :authenticated="authenticated"
    :main-tip="numberTip"
    sign-in-tip="Sign in to vote"
    :up-tip-vote="upTipVote"
    :up-tip-voted="upTipVoted"
    :down-tip-vote="downTipVote"
    :down-tip-voted="downTipVoted"
    @up-vote="emit('up-vote')"
    @down-vote="emit('down-vote')"
  >
  </VoteButton>
</template>
