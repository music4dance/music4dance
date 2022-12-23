<template>
  <a
    href="#"
    @click.prevent="onClick"
    v-b-tooltip.hover.blur.right="likeTip"
    role="button"
  >
    <b-icon
      :icon="trueIcon"
      :variant="trueVariant"
      v-if="state"
      :font-scale="scale"
    ></b-icon>
    <b-iconstack v-else-if="state === false" :font-scale="scale">
      <b-icon
        stacked
        :icon="falseIcon"
        :variant="falseVariant"
        scale="0.75"
        shift-v="-1"
      ></b-icon>
      <b-icon stacked icon="x-circle" variant="danger"></b-icon>
    </b-iconstack>
    <b-icon
      :icon="falseIcon"
      :variant="falseVariant"
      :font-scale="scale"
      v-else
    ></b-icon>
  </a>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  props: {
    state: Boolean,
    authenticated: Boolean,
    redirectUrl: String,
    signInTip: String,
    undefinedTip: String,
    trueTip: String,
    falseTip: String,
    trueIcon: String,
    falseIcon: String,
    trueVariant: String,
    falseVariant: String,
    scale: Number,
  },
  computed: {
    likeTip(): string | undefined {
      if (!this.authenticated) {
        return this.signInTip;
      }

      const state = this.state;
      if (state === undefined) {
        return this.undefinedTip;
      } else if (state) {
        return this.trueTip;
      } else {
        return this.falseTip;
      }
    },
  },
  methods: {
    async onClick(): Promise<void> {
      if (this.redirectUrl) {
        if (this.authenticated) {
          this.$emit("click-icon");
        } else {
          window.location.href = this.redirectUrl;
        }
      }
    },
  },
});
</script>
