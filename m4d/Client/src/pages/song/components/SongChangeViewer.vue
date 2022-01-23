<template>
  <div>
    <b-icon-heart-fill v-if="change.like" variant="danger"></b-icon-heart-fill>
    <b-iconstack v-else-if="change.like === false">
      <b-icon-heart
        stacked
        variant="secondary"
        scale="0.75"
        shift-v="-1"
      ></b-icon-heart>
      <b-icon-x-circle stacked variant="danger"></b-icon-x-circle>
    </b-iconstack>
    <b-icon-pencil v-else></b-icon-pencil>
    <template v-if="!oneUser">
      {{ action }} by
      <user-link :user="this.change.user"></user-link>
    </template>
    on
    {{ formattedDate }}
    <div v-for="(property, index) in tagProperties" :key="index" class="ml-4">
      <song-property-viewer :property="property"></song-property-viewer>
    </div>
  </div>
</template>

<script lang="ts">
import UserLink from "@/components/UserLink.vue";
import { SongChange } from "@/model/SongChange";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import format from "date-fns/format";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import SongPropertyViewer from "./SongPropertyViewer.vue";

@Component({ components: { SongPropertyViewer, UserLink } })
export default class SongChangeViewer extends Vue {
  @Prop() private readonly change!: SongChange;
  @Prop() private readonly oneUser?: boolean;

  private get action(): string {
    return this.change.action === PropertyType.createdField
      ? "Added"
      : "Changed";
  }

  private get formattedDate(): string {
    const date = this.change.date;
    return date ? format(date, "Pp") : "<unknown>";
  }

  private get tagProperties(): SongProperty[] {
    return this.change.properties.filter((t) => t.baseName.startsWith("Tag"));
  }
}
</script>
