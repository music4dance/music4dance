<script setup lang="ts">
import PurchaseLogo from "@/components/PurcahseLogo.vue";
import { PurchaseInfo, ServiceType } from "@/models/Purchase";
import { SongProperty } from "@/models/SongProperty";
import { TrackModel } from "@/models/TrackModel";
import TrackField from "./TrackField.vue";
import { computed } from "vue";

const props = defineProps<{
  track: TrackModel;
  enableProperties?: boolean;
}>();
const emit = defineEmits<{
  "add-track": [track: TrackModel];
  "add-property": [property: SongProperty];
}>();

const purchaseInfo = computed(() => {
  const track = props.track;
  const type = track.service[0].toLowerCase() as ServiceType;
  return PurchaseInfo.Build(type, track.collectionId, track.trackId)!;
});
const indexString = computed(() => {
  const track = props.track;
  return track && track.trackNumber ? track.trackNumber.toString() : "";
});
const durationString = computed(() => {
  const track = props.track;
  return track && track.duration ? track.duration.toString() : "";
});

const addTrack = (track: TrackModel) => {
  emit("add-track", track);
};
const addProperty = (name: string, value: string) => {
  emit("add-property", new SongProperty({ name: name, value: value }));
};
</script>

<template>
  <div>
    <BRow>
      <BCol style="flex: 0 0 110px" align-self="center">
        <PurchaseLogo :info="purchaseInfo"></PurchaseLogo>
        <img v-if="track.imageUrl" :src="track.imageUrl" class="mx-1" width="40" height="40" />
      </BCol>
      <BCol>
        <TrackField
          name="title"
          :value="track.name"
          :can-add="enableProperties"
          @add-property="addProperty"
        ></TrackField>
        <TrackField
          name="artist"
          :value="track.artist"
          :can-add="enableProperties"
          @add-property="addProperty"
        ></TrackField>
        <TrackField name="album" :value="track.album!"></TrackField>
        <TrackField name="track" :value="indexString"></TrackField>
        <TrackField
          name="length"
          :value="durationString"
          :can-add="enableProperties"
          @add-property="addProperty"
        ></TrackField>
      </BCol>
      <BCol align-self="center" @click="addTrack(track)"><BButton>Add</BButton></BCol>
    </BRow>
  </div>
</template>
