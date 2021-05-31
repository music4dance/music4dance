<template>
  <div>
    <b-row>
      <b-col style="flex: 0 0 110px" align-self="center">
        <purchase-logo :info="purchaseInfo"></purchase-logo>
        <img
          v-if="track.imageUrl"
          :src="track.imageUrl"
          class="mx-1"
          width="40"
          height="40"
        />
      </b-col>
      <b-col>
        <div>
          Album: <b>{{ track.album }}</b>
        </div>
        <div>
          Track: <b>{{ track.trackNumber }}</b>
        </div>
        <b-button
          variant="outline-primary"
          size="sm"
          block
          style="text-align: left"
          class="mb-2"
          @click="addProperty('title', track.name)"
        >
          <b-icon-lock-fill aria-label="add"></b-icon-lock-fill>
          Title: <b>{{ track.name }}</b>
        </b-button>
        <b-button
          variant="outline-primary"
          size="sm"
          block
          style="text-align: left"
          class="mb-2"
          @click="addProperty('artist', track.artist)"
        >
          <b-icon-lock-fill aria-label="add"></b-icon-lock-fill>
          Artist: <b>{{ track.artist }}</b>
        </b-button>
        <div>
          <b-button
            v-if="track.duration"
            variant="outline-primary"
            size="sm"
            block
            style="text-align: left"
            @click="addProperty('artist', track.duration)"
          >
            <b-icon-lock-fill aria-label="add"></b-icon-lock-fill>
            Length: <b>{{ track.duration }}</b>
          </b-button>
        </div>
      </b-col>
      <b-col align-self="center" @click="addTrack(track)"
        ><b-button>Add</b-button></b-col
      >
    </b-row>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import PurchaseLogo from "@/components/PurcahseLogo.vue";
import { Component, Prop, Vue } from "vue-property-decorator";
import { TrackModel } from "@/model/TrackModel";
import { PurchaseInfo, ServiceType } from "@/model/Purchase";
import { SongProperty } from "@/model/SongProperty";

@Component({ components: { PurchaseLogo } })
export default class TrackItem extends Vue {
  @Prop() private readonly track!: TrackModel;

  private get purchaseInfo(): PurchaseInfo {
    const track = this.track;
    const type = track.service[0].toLowerCase() as ServiceType;
    return PurchaseInfo.Build(type, track.collectionId, track.trackId)!;
  }

  private addTrack(track: TrackModel): void {
    this.$emit("add-track", track);
  }

  private addProperty(name: string, value: string): void {
    this.$emit("add-property", new SongProperty({ name: name, value: value }));
  }
}
</script>
