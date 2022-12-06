<template>
  <b-row v-if="editing">
    <b-col sm="auto">
      <b-button-close @click="deleteLink()"></b-button-close>
    </b-col>
    <b-col sm="2">
      <b-form-input
        v-model="link.description"
        @input="updateDescription($event)"
      ></b-form-input>
    </b-col>
    <b-col sm="9">
      <b-form-input
        v-model="link.link"
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
import { Component, Model, Prop, Vue } from "vue-property-decorator";

@Component
export default class EditableLink extends Vue {
  @Model("update") readonly link!: DanceLink;
  @Prop() private readonly editing!: boolean;

  private get internalLink(): DanceLink {
    return this.link;
  }

  private set internalLink(value: DanceLink) {
    this.$emit("update", value);
  }

  private updateDescription(value: string): void {
    this.internalLink = this.link.cloneAndModify({ description: value });
  }

  private updateLink(value: string): void {
    this.internalLink = this.link.cloneAndModify({ link: value });
  }

  private deleteLink(): void {
    this.$emit("delete", this.link);
  }
}
</script>
