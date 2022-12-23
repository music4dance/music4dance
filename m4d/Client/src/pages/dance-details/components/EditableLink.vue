<template>
  <b-row v-if="editing">
    <b-col sm="auto">
      <b-button-close @click="deleteLink()"></b-button-close>
    </b-col>
    <b-col sm="2">
      <b-form-input
        v-model="internalLink.description"
        @input="updateDescription($event)"
      ></b-form-input>
    </b-col>
    <b-col sm="9">
      <b-form-input
        v-model="internalLink.link"
        @input="updateLink($event)"
      ></b-form-input>
    </b-col>
  </b-row>
  <div v-else>
    <b>{{ link.description }}: </b><span> </span>
    <a :href="link.link" target="_blank"
      >{{ link.link }} <b-icon-box-arrow-up-right></b-icon-box-arrow-up-right
    ></a>
  </div>
</template>

<script lang="ts">
import { DanceLink } from "@/model/DanceLink";
import "reflect-metadata";
import Vue, { PropType } from "vue";

export default Vue.extend({
  model: {
    prop: "link",
    event: "update",
  },
  props: {
    link: { type: Object as PropType<DanceLink>, required: true },
    editing: Boolean,
  },
  computed: {
    internalLink: {
      get: function (): DanceLink {
        return this.link;
      },
      set: function (value: DanceLink): void {
        this.$emit("update", value);
      },
    },
  },
  methods: {
    updateDescription(value: string): void {
      this.internalLink = this.link.cloneAndModify({ description: value });
    },

    updateLink(value: string): void {
      this.internalLink = this.link.cloneAndModify({ link: value });
    },

    deleteLink(): void {
      this.$emit("delete", this.link);
    },
  },
});
</script>
