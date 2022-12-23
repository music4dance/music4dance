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
import PurchaseLogo from "@/components/PurcahseLogo.vue";
import { PurchaseInfo, ServiceType } from "@/model/Purchase";
import { SongProperty } from "@/model/SongProperty";
import { TrackModel } from "@/model/TrackModel";
import "reflect-metadata";
import Vue, { PropType } from "vue";
import TrackField from "./TrackField.vue";

export default Vue.extend({
  components: { PurchaseLogo, TrackField },
  props: {
    track: { type: Object as PropType<TrackModel>, required: true },
    enableProperties: Boolean,
  },
  computed: {
    purchaseInfo(): PurchaseInfo {
      const track = this.track;
      const type = track.service[0].toLowerCase() as ServiceType;
      return PurchaseInfo.Build(type, track.collectionId, track.trackId)!;
    },
  },
  methods: {
    addTrack(track: TrackModel): void {
      this.$emit("add-track", track);
    },

    addProperty(name: string, value: string): void {
      this.$emit(
        "add-property",
        new SongProperty({ name: name, value: value })
      );
    },
  },
});
</script>
