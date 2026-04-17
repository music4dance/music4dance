<script setup lang="ts">
import { SongHistory } from "@/models/SongHistory";
import { computed, ref } from "vue";

const props = defineProps<{ history: SongHistory; authenticated: boolean }>();

const includeAutomated = ref(false);

const displayedChanges = computed(() =>
  !props.authenticated || !includeAutomated.value
    ? props.history.userChanges
    : props.history.inclusiveChanges,
);

const activeTags = computed(() => props.history.activeTags);
</script>

<template>
  <BCard header-text-variant="primary" no-body border-variant="primary">
    <template #header>
      <div class="d-flex align-items-center justify-content-between">
        <span class="text-primary fw-bold">Changes</span>
        <BFormCheckbox v-if="authenticated" v-model="includeAutomated" switch class="mb-0">
          <IBiCpuFill class="me-1" />Include automated
        </BFormCheckbox>
      </div>
    </template>
    <BListGroup flush>
      <BListGroupItem v-for="(change, index) in displayedChanges" :key="index">
        <SongChangeViewer :change="change" :active-tags="activeTags" />
      </BListGroupItem>
    </BListGroup>
  </BCard>
</template>
