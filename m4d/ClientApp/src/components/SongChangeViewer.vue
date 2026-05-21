<script setup lang="ts">
import { SongChange } from "@/models/SongChange";
import { PropertyType } from "@/models/SongProperty";
import { format } from "date-fns";
import type { DanceHandler } from "@/models/DanceHandler";
import type { TagHandler } from "@/models/TagHandler";
import { computed } from "vue";

const props = defineProps<{
  change: SongChange;
  oneUser?: boolean;
  activeTags?: Set<string>;
}>();

const emit = defineEmits<{
  "dance-clicked": [handler: DanceHandler];
  "tag-clicked": [handler: TagHandler];
}>();

const isAlgorithmic = computed(() => props.change.isAlgorithmic);
const action = computed(() =>
  props.change.action === PropertyType.createdField ? "Added" : "Changed",
);
const date = computed(() => props.change.date);
const formattedDate = computed(() => (date.value ? format(date.value, "P hh:mm a") : "<unknown>"));
const viewableProperties = computed(() =>
  props.change.properties.filter(
    (t) =>
      t.baseName.startsWith("Tag") ||
      t.baseName.startsWith("Comment") ||
      t.baseName === PropertyType.tempoField,
  ),
);
</script>

<template>
  <div>
    <span class="me-2">
      <IBiHeartFill v-if="change.like" style="color: red" />
      <IBiHeartbreakFill v-else-if="change.like === false" />
      <IBiPencil v-else />
    </span>
    <template v-if="!oneUser">
      {{ action }} by
      <UserLink :user="change.user!" />
      <IBiCpuFill
        v-if="isAlgorithmic"
        class="ms-1 text-secondary"
        style="font-size: 0.85em"
        title="Algorithmically generated"
      />
    </template>
    on
    {{ formattedDate }}
    <div v-for="(property, index) in viewableProperties" :key="index" class="ms-4">
      <SongPropertyViewer
        :property="property"
        :active-tags="activeTags"
        @dance-clicked="emit('dance-clicked', $event as DanceHandler)"
        @tag-clicked="emit('tag-clicked', $event as TagHandler)"
      />
    </div>
  </div>
</template>
