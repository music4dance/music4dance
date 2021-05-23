<template>
  <b-card
    header="Albums"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-list-group flush>
      <b-list-group-item v-for="(album, index) in albums" :key="index">
        <b-button
          v-if="isAdmin && editing"
          size="sm"
          variant="outline-danger"
          @click="onDelete(album)"
          class="mr-2"
          ><b-icon-x variant="danger"></b-icon-x
        ></b-button>
        <a :href="albumLink(album)">{{ album.name }}</a>
        <span v-if="album.purchase" class="mx-2">
          <a
            v-for="purchase in album.purchase.decode()"
            :key="purchase.link"
            :href="purchase.link"
            target="_blank"
            ><img
              :src="purchase.logo"
              width="32"
              height="32"
              :alt="purchase.altText"
          /></a>
        </span>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";
import { AlbumDetails } from "@/model/AlbumDetails";
import AdminTools from "@/mix-ins/AdminTools";

@Component
export default class AlbumList extends Mixins(AdminTools) {
  @Prop() private readonly albums!: AlbumDetails[];
  @Prop() private readonly editing?: boolean;

  private albumLink(album: AlbumDetails): string {
    return `/song/album?title=${album.name}`;
  }

  private onDelete(album: AlbumDetails): void {
    console.log(album.name);
    this.$emit("delete-album", album);
  }
}
</script>
