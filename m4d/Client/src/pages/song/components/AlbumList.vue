<template>
  <b-card
    header="Albums"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-list-group flush>
      <b-list-group-item v-for="(album, index) in albums" :key="index">
        <b-button-close
          v-if="isAdmin && editing"
          size="sm"
          text-variant="danger"
          @click="onDelete(album)"
          class="mr-2"
          ><b-icon-x variant="danger"></b-icon-x
        ></b-button-close>
        <a :href="albumLink(album)">{{ album.name }}</a>
        <span v-if="album.track && album.track < 100">
          (Track {{ album.track }})</span
        >
        <span v-if="album.purchase" class="mx-2">
          <purchase-logo
            v-for="purchase in album.purchase.decode()"
            :key="purchase.link"
            :info="purchase"
          ></purchase-logo>
        </span>
      </b-list-group-item>
    </b-list-group>
  </b-card>
</template>

<script lang="ts">
import PurchaseLogo from "@/components/PurcahseLogo.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { AlbumDetails } from "@/model/AlbumDetails";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({ components: { PurchaseLogo } })
export default class AlbumList extends Mixins(AdminTools) {
  @Prop() private readonly albums!: AlbumDetails[];
  @Prop() private readonly editing?: boolean;

  private albumLink(album: AlbumDetails): string {
    return `/song/album?title=${album.name}`;
  }

  private onDelete(album: AlbumDetails): void {
    this.$emit("delete-album", album);
  }
}
</script>
