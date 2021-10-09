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
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class TempoModal extends Vue {
  @Prop() private readonly identifier!: string;
  @Prop() private readonly tempo!: number;
  @Prop() private readonly label!: string;

  private tempoInternal = 0;

  private initialize(): void {
    this.tempoInternal = Number(this.tempo.toFixed(1));
    const el = this.$refs.input as HTMLInputElement;
    el.focus();
    el.select();
  }

  private submit(): void {
    this.$parent.$emit("change-tempo", Number(this.tempoInternal));
  }

  private logKeyDown(): void {
    // This is a kludge to prevent lastpass from screaming
  }

  private handleSubmit(): void {
    this.submit();
    this.$root.$emit("bv::hide::modal", this.identifier);
  }
}
</script>
