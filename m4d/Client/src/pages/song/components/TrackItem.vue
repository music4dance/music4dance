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
        <track-field
          name="title"
          :value="track.name"
          :canAdd="enableProperties"
          @add-property="addProperty"
        ></track-field>
        <track-field
          name="artist"
          :value="track.artist"
          :canAdd="enableProperties"
          @add-property="addProperty"
        ></track-field>
        <track-field name="album" :value="track.album"></track-field>
        <track-field
          name="track"
          :value="track.trackNumber.toString()"
        ></track-field>
        <track-field
          name="length"
          :value="track.duration.toString()"
          :canAdd="enableProperties"
          @add-property="addProperty"
        ></track-field>
        <track-field
          v-if="track.tempo"
          name="tempo"
          :value="track.tempo.toString()"
          :canAdd="enableProperties"
          @add-property="addProperty"
        ></track-field>
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
import TrackField from "./TrackField.vue";
import { Component, Prop, Vue } from "vue-property-decorator";
import { TrackModel } from "@/model/TrackModel";
import { PurchaseInfo, ServiceType } from "@/model/Purchase";
import { SongProperty } from "@/model/SongProperty";

@Component({ components: { PurchaseLogo, TrackField } })
export default class TrackItem extends Vue {
  @Prop() private readonly track!: TrackModel;
  @Prop() private readonly enableProperties?: boolean;

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
