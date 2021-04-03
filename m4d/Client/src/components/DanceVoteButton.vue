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
import { Component, Prop, Vue } from "vue-property-decorator";
import VoteButton from "./VoteButton.vue";

@Component({ components: { VoteButton } })
export default class DanceVoteButton extends Vue {
  @Prop() private readonly vote?: boolean;
  @Prop() private readonly value!: number;
  @Prop() private readonly maxVote!: number;
  @Prop() private readonly authenticated!: boolean;
  @Prop() private readonly danceName!: string;

  private get upTipVote(): string {
    return `Click here to vote for this song being danceable ${this.danceName}`;
  }

  private get upTipVoted(): string {
    return `You have voted that this song is danceable to ${this.danceName}`;
  }

  private get downTipVote(): string {
    return `Click here to vote that this song is not danceable to ${this.danceName}`;
  }

  private get downTipVoted(): string {
    return `You have voted that this song is not danceable to ${this.danceName}`;
  }

  private get numberTip(): string {
    return (
      `This song has ${this.value} votes. ` +
      `The most popular ${this.danceName} has ${this.maxVote} votes.`
    );
  }

  private get redirectUrl(): string {
    const location = window.location;
    return `/identity/account/login?returnUrl=${location.pathname}${location.search}${location.hash}`;
  }
}
</script>
