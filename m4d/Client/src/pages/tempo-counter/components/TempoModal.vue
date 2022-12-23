<template>
  <div>
    <b-modal
      :id="identifier"
      ref="modal"
      title="Set Tempo"
      size="sm"
      @shown="initialize"
      @ok="submit"
    >
      <form ref="form" @submit.stop.prevent="handleSubmit">
        <b-form-group :label="label" label-for="tempo-input">
          <b-form-input
            id="tempo-input"
            ref="input"
            v-model="tempoInternal"
            type="number"
            step=".1"
            min="0"
            max="500"
            @keydown.stop="logKeyDown"
          >
          </b-form-input>
        </b-form-group>
      </form>
    </b-modal>
  </div>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  props: {
    identifier: String,
    tempo: Number,
    label: String,
  },
  data() {
    return new (class {
      tempoInternal = 0;
    })();
  },
  methods: {
    initialize(): void {
      this.tempoInternal = Number(this.tempo.toFixed(1));
      const el = this.$refs.input as HTMLInputElement;
      el.focus();
      el.select();
    },

    submit(): void {
      if (!this.$parent) {
        throw new Error("Something went terribly wrong");
      }
      this.$parent.$emit("change-tempo", Number(this.tempoInternal));
    },

    logKeyDown(): void {
      // This is a kludge to prevent lastpass from screaming
    },

    handleSubmit(): void {
      this.submit();
      this.$root.$emit("bv::hide::modal", this.identifier);
    },
  },
});
</script>
