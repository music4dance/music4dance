<template>
  <div class="vote-container">
    <div
      :class="upClass"
      v-b-tooltip.hover.right="upTip"
      @click="upClick"
    ></div>
    <div class="vote-number" v-b-tooltip.hover.right="mainTip">
      {{ value }}
    </div>
    <div
      :class="downClass"
      v-b-tooltip.hover.right="downTip"
      @click="downClick"
    ></div>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class VoteButton extends Vue {
  @Prop() private readonly vote?: boolean;
  @Prop() private readonly value!: number;
  @Prop() private readonly redirectUrl!: string;
  @Prop() private readonly authenticated!: boolean;
  @Prop() private readonly mainTip!: string;
  @Prop() private readonly signInTip!: string;
  @Prop() private readonly upTipVote!: string;
  @Prop() private readonly upTipVoted!: string;
  @Prop() private readonly downTipVote!: string;
  @Prop() private readonly downTipVoted!: string;

  private get upClass(): string[] {
    return this.vote ? ["voted-up"] : ["vote-up"];
  }

  private get downClass(): string[] {
    return this.vote === false ? ["voted-down"] : ["vote-down"];
  }

  private get upTip(): string {
    return this.authenticated
      ? this.vote
        ? this.upTipVoted
        : this.upTipVote
      : this.signInTip;
  }

  private get downTip(): string {
    return this.authenticated
      ? this.vote === false
        ? this.downTipVoted
        : this.downTipVote
      : this.signInTip;
  }

  private async upClick(): Promise<void> {
    if (this.authenticated) {
      this.$emit("up-vote");
    } else {
      this.login();
    }
  }

  private async downClick(): Promise<void> {
    if (this.authenticated) {
      this.$emit("down-vote");
    } else {
      this.login();
    }
  }

  private login(): void {
    window.location.href = this.redirectUrl;
  }
}
</script>

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
