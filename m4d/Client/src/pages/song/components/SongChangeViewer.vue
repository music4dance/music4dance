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
    <span v-else style="margin-right: 1.25rem"></span>
    {{ action }} by
    <a :href="userLink" :class="userClasses">{{ change.baseUser }}</a>
    on
    {{ formattedDate }}
    <div v-for="(property, index) in tagProperties" :key="index" class="ml-4">
      <song-property-viewer :property="property"></song-property-viewer>
    </div>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { SongChange } from "@/model/SongChange";
import format from "date-fns/format";
import { PropertyType, SongProperty } from "@/model/SongProperty";
import SongPropertyViewer from "./SongPropertyViewer.vue";

@Component({ components: { SongPropertyViewer } })
export default class SongChangeViewer extends Vue {
  @Prop() private readonly change!: SongChange;

  private get action(): string {
    return this.change.action === PropertyType.createdField
      ? "Added"
      : "Changed";
  }

  private get userLink(): string {
    return `/song/filteruser?user=${this.change.baseUser}`;
  }

  private get formattedDate(): string {
    const date = this.change.date;
    return date ? format(date, "Pp") : "<unknown>";
  }

  private get userClasses(): string[] {
    return this.change.isPseudo ? ["pseudo"] : [];
  }

  private get tagProperties(): SongProperty[] {
    return this.change.properties.filter((t) => t.baseName.startsWith("Tag"));
  }
}
</script>

<style scoped lang="scss">
.pseudo {
  font-style: italic;
}
</style>
