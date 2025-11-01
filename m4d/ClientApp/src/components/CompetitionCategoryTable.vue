<script setup lang="ts">
import { DanceInstance } from "@/models/DanceDatabase/DanceInstance";
import { danceLink, filteredTempoLink } from "@/helpers/LinkHelpers";
import type { TableFieldRaw } from "bootstrap-vue-next";
import type { LiteralUnion } from "@/helpers/bsvn-types";
import { computed } from "vue";

const props = defineProps<{
  dances: DanceInstance[];
  title: string;
  useFullName?: boolean;
}>();

const showBpm = defineModel<boolean>("showBpm", { default: true });

// Extract unique organizations across all dance instances
const organizationColumns = computed(() => {
  const orgs = new Set<string>();
  for (const dance of props.dances) {
    for (const org of dance.organizations) {
      orgs.add(org);
    }
  }
  return Array.from(orgs).sort();
});

// Build dynamic fields based on available organizations
const fields = computed<Exclude<TableFieldRaw<DanceInstance>, string>[]>(() => {
  const baseFields: Exclude<TableFieldRaw<DanceInstance>, string>[] = [
    {
      key: "name",
      label: "Name",
    },
  ];

  // Add a column for each organization
  for (const org of organizationColumns.value) {
    baseFields.push({
      key: org,
      label: org,
    });
  }

  // Add meter column
  baseFields.push({
    key: "meter",
    label: "Meter",
    formatter: (_value: unknown, _key?: LiteralUnion<keyof DanceInstance>, item?: DanceInstance) =>
      item!.meter.toString(),
  });

  return baseFields;
});

function displayName(dance: DanceInstance): string {
  // If useFullName is true, show the full name with style prefix (e.g., "American Smooth Waltz")
  // Otherwise, show just the dance type name (e.g., "Slow Waltz")
  if (props.useFullName) {
    return dance.name;
  }
  // Use the dance type's name if available, otherwise fall back to the dance name
  return dance.danceType?.name ?? dance.name;
}

function formatTempo(dance: DanceInstance, organization: string): string {
  // Check if this dance instance supports this organization (case-insensitive)
  const hasOrg = dance.organizations.some(
    (org) => org.localeCompare(organization, undefined, { sensitivity: "accent" }) === 0,
  );
  if (!hasOrg) {
    return "";
  }

  const tempo = dance.filteredTempo([organization]);
  if (!tempo) {
    return "";
  }

  return showBpm.value ? tempo.toString() : tempo.mpm(dance.meter.numerator);
}
</script>

<template>
  <div>
    <div class="d-flex justify-content-between align-items-center mb-2">
      <h4 v-if="title" class="mb-0">{{ title }}</h4>
      <BButtonGroup size="sm">
        <BButton :variant="showBpm ? 'primary' : 'outline-primary'" @click="showBpm = true">
          BPM
        </BButton>
        <BButton :variant="!showBpm ? 'primary' : 'outline-primary'" @click="showBpm = false">
          MPM
        </BButton>
      </BButtonGroup>
    </div>
    <BTable striped hover :items="props.dances" :fields="fields" responsive>
      <template #cell(name)="data">
        <a :href="danceLink(data.item)">{{ displayName(data.item) }}</a>
      </template>
      <template v-for="org in organizationColumns" :key="org" #[`cell(${org})`]="data">
        <a :href="filteredTempoLink(data.item, org)">
          {{ formatTempo(data.item, org) }}
        </a>
      </template>
    </BTable>
  </div>
</template>
