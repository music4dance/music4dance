<script setup lang="ts">
import UserLink from "./UserLink.vue";
import { SongChange } from "@/models/SongChange";
import { PropertyType } from "@/models/SongProperty";
import { format } from "date-fns";
import SongPropertyViewer from "./SongPropertyViewer.vue";
import type { DanceHandler } from "@/models/DanceHandler";
import type { TagHandler } from "@/models/TagHandler";

const props = defineProps<{
  change: SongChange;
  oneUser?: boolean;
}>();

const emit = defineEmits<{
  "dance-clicked": [handler: DanceHandler];
  "tag-clicked": [handler: TagHandler];
}>();

const action = props.change.action === PropertyType.createdField ? "Added" : "Changed";
const date = props.change.date;
const formattedDate = date ? format(date, "Pp") : "<unknown>";
const viewableProperties = props.change.properties.filter(
  (t) =>
    t.baseName.startsWith("Tag") ||
    t.baseName.startsWith("Comment") ||
    t.baseName === PropertyType.tempoField,
);
</script>

<template>
  <div>
    <IBiHeartFill v-if="change.like" style="color: red" />
    <IBiHeartbreakFill v-else-if="change.like === false" />
    <IBiPencil v-else></IBiPencil>
    <template v-if="!oneUser">
      {{ action }} by
      <UserLink :user="change.user!"></UserLink>
    </template>
    on
    {{ formattedDate }}
    <div v-for="(property, index) in viewableProperties" :key="index" class="ml-4">
      <SongPropertyViewer
        :property="property"
        @dance-clicked="emit('dance-clicked', $event as DanceHandler)"
        @tag-clicked="emit('tag-clicked', $event as TagHandler)"
      ></SongPropertyViewer>
    </div>
  </div>
</template>
