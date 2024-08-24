<script setup lang="ts">
import { AlbumDetails } from "@/models/AlbumDetails";
import { getMenuContext } from "@/helpers/GetMenuContext";

const context = getMenuContext();

defineProps<{
  albums: AlbumDetails[];
  editing: boolean;
}>();

const emit = defineEmits<{
  "delete-album": [album: AlbumDetails];
}>();

const albumLink = (album: AlbumDetails): string => {
  return `/song/album?title=${encodeURIComponent(album.name ?? "")}`;
};
const onDelete = (album: AlbumDetails): void => {
  emit("delete-album", album);
};
</script>

<template>
  <BCard header="Albums" header-text-variant="primary" no-body border-variant="primary">
    <BListGroup flush>
      <BListGroupItem v-for="(album, index) in albums" :key="index">
        <BCloseButton
          v-if="context.isAdmin && editing"
          size="sm"
          text-variant="danger"
          class="me-2"
          @click="onDelete(album)"
          ><IBiX variant="danger"
        /></BCloseButton>
        <a :href="albumLink(album)">{{ album.name }}</a>
        <span v-if="album.track && album.track < 100"> (Track {{ album.track }})</span>
        <span v-if="album.purchase" class="mx-2">
          <PurchaseLogo
            v-for="purchase in album.purchase.decode()"
            :key="purchase.link"
            :info="purchase"
          ></PurchaseLogo>
        </span>
      </BListGroupItem>
    </BListGroup>
  </BCard>
</template>
