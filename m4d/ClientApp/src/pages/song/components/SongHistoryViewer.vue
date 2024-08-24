<script setup lang="ts">
import { SongHistory } from "@/models/SongHistory";

defineProps<{ history: SongHistory }>();
</script>

<template>
  <BCard header="Changes" header-text-variant="primary" no-body border-variant="primary">
    <BListGroup flush>
      <BListGroupItem v-for="(change, index) in history.userChanges" :key="index">
        <SongChangeViewer :change="change" />
      </BListGroupItem>
    </BListGroup>
  </BCard>
</template>
