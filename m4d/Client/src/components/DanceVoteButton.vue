<template>
  <vote-button
    :vote="vote"
    :value="value"
    :redirectUrl="redirectUrl"
    :authenticated="authenticated"
    :mainTip="numberTip"
    signInTip="Sign in to vote"
    :upTipVote="upTipVote"
    :upTipVoted="upTipVoted"
    :downTipVote="downTipVote"
    :downTipVoted="downTipVoted"
    @up-vote="$emit('up-vote')"
    @down-vote="$emit('down-vote')"
  >
  </vote-button>
</template>

<script lang="ts">
import Vue from "vue";
import VoteButton from "./VoteButton.vue";

export default Vue.extend({
  components: { VoteButton },
  props: {
    vote: Boolean,
    value: Number,
    maxVote: Number,
    authenticated: Boolean,
    danceName: String,
  },
  computed: {
    upTipVote(): string {
      return `Click here to vote for this song being danceable ${this.danceName}`;
    },
    upTipVoted(): string {
      return `You have voted that this song is danceable to ${this.danceName}`;
    },
    downTipVote(): string {
      return `Click here to vote that this song is not danceable to ${this.danceName}`;
    },
    downTipVoted(): string {
      return `You have voted that this song is not danceable to ${this.danceName}`;
    },
    numberTip(): string {
      return (
        `This song has ${this.value} votes. ` +
        `The most popular ${this.danceName} has ${this.maxVote} votes.`
      );
    },
    redirectUrl(): string {
      const location = window.location;
      return `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`;
    },
  },
});
</script>
