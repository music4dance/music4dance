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
import Vue from "vue";

export default Vue.extend({
  props: {
    vote: Boolean,
    value: Number,
    redirectUrl: String,
    authenticated: Boolean,
    mainTip: String,
    signInTip: String,
    upTipVote: String,
    upTipVoted: String,
    downTipVote: String,
    downTipVoted: String,
  },

  computed: {
    upClass(): string[] {
      return this.vote ? ["voted-up"] : ["vote-up"];
    },
    downClass(): string[] {
      return this.vote === false ? ["voted-down"] : ["vote-down"];
    },
    upTip(): string {
      return this.authenticated
        ? this.vote
          ? this.upTipVoted
          : this.upTipVote
        : this.signInTip;
    },
    downTip(): string {
      return this.authenticated
        ? this.vote === false
          ? this.downTipVoted
          : this.downTipVote
        : this.signInTip;
    },
  },
  methods: {
    async upClick(): Promise<void> {
      if (this.authenticated) {
        this.$emit("up-vote");
      } else {
        this.login();
      }
    },
    async downClick(): Promise<void> {
      if (this.authenticated) {
        this.$emit("down-vote");
      } else {
        this.login();
      }
    },
    login(): void {
      window.location.href = this.redirectUrl;
    },
  },
});
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
