<template>
  <b-card
    header="Albums"
    header-text-variant="primary"
    no-body
    border-variant="primary"
  >
    <b-list-group flush>
      <b-list-group-item v-for="(album, index) in albums" :key="index">
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
import { Component, Prop, Vue } from "vue-property-decorator";
import { AlbumDetails } from "@/model/AlbumDetails";

@Component
export default class AlbumList extends Vue {
  @Prop() private readonly albums!: AlbumDetails[];

  private albumLink(album: AlbumDetails): string {
    return `/song/album?title=${album.name}`;
  }
}
</script>
