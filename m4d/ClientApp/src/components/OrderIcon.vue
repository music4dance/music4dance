<script setup lang="ts">
import { SortOrder } from "@/models/SongSort";

defineProps<{
  order?: SortOrder;
}>();
</script>

<template>
  <IBiFileEarmarkPlus v-if="order === SortOrder.Created" />
  <IBiPencil v-else-if="order === SortOrder.Modified" />
  <IBiPencilFill v-else-if="order === SortOrder.Edited" />
  <IBiVectorPen v-else-if="order === SortOrder.Comments" />
  <IBiAsterisk v-else />
</template>
