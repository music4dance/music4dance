<script setup lang="ts">
import { TempoType } from "@/models/TempoType";
import { computed, ref } from "vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { BaseColorVariant } from "bootstrap-vue-next";
import type { DanceType } from "@/models/DanceDatabase/DanceType";

const danceDB = safeDanceDatabase();

const props = withDefaults(
  defineProps<{
    danceId?: string;
    filterIds?: string[];
    tempo?: number;
    numerator?: number;
    includeGroups?: boolean;
    hideNameLink?: boolean;
  }>(),
  {
    danceId: "",
    filterIds: () => [],
    tempo: 0,
    numerator: 0,
    includeGroups: false,
    hideNameLink: false,
  },
);

const emit = defineEmits<{
  "choose-dance": [id?: string, persist?: boolean];
}>();

const nameFilter = ref("");

const danceTypes = danceDB.dances;

const filterAll = (dances: NamedObject[], includeChildren = false): NamedObject[] => {
  return DanceDatabase.filterByName(dances, nameFilter.value, includeChildren);
};

const sortedDances = computed(() => {
  return filterAll(danceDB.flattened)
    .filter((d) => props.includeGroups || !DanceGroup.isGroup(d))
    .sort((a, b) => a.name.localeCompare(b.name));
});

const groupedDances = computed(() => {
  return filterAll(danceDB.flatGroups, true);
});

const tempoFiltered = computed(() => {
  return DanceDatabase.filterByName(danceTypes, nameFilter.value, false) as DanceType[];
});

const tempoType = TempoType.Measures;

const exists = (danceId: string): boolean => {
  const filtered = props.filterIds;
  if (!filtered) {
    return false;
  }
  return !!filtered.find((id) => id === danceId);
};

const chooseEvent = (id?: string, event?: MouseEvent): void => {
  const persist = event?.ctrlKey;
  if (persist) {
    event?.preventDefault();
  }
  choose(id, persist);
};

const choose = (id?: string, persist?: boolean): void => {
  emit("choose-dance", id, persist);
};

const groupVariant = (dance: NamedObject): keyof BaseColorVariant | undefined => {
  return DanceGroup.isGroup(dance) && !(props.danceId === dance.id)
    ? ("primary" as keyof BaseColorVariant)
    : undefined;
};

const isGroup = (dance: NamedObject) => {
  return DanceGroup.isGroup(dance);
};
</script>

<template>
  <BModal id="dance-chooser" header-bg-variant="primary" header-text-variant="light" no-footer>
    <template #title> <IBiAward />&nbsp;Choose Dance Style </template>
    <BButton
      v-if="danceId"
      block
      variant="outline-primary"
      style="margin-bottom: 0.5em"
      @click="choose()"
    >
      Search All Dance Styles
    </BButton>
    <BInputGroup class="mb-2">
      <BFormInput v-model="nameFilter" type="text" placeholder="Filter Dances" autofocus />
      <span><IBiSearch /></span>
    </BInputGroup>
    <BTabs>
      <BTab title="By Name" :active="!hasTempo">
        <BListGroup>
          <BListGroupItem
            v-for="dance in sortedDances"
            :key="dance.id"
            button
            :active="danceId === dance.id"
            :disabled="exists(dance.id)"
            @click.prevent="chooseEvent(dance.id, $event)"
          >
            <DanceName
              :dance="dance"
              :show-synonyms="true"
              :show-tempo="tempoType"
              :hide-link="hideNameLink"
            />
          </BListGroupItem>
        </BListGroup>
      </BTab>
      <BTab title="By Style">
        <BListGroup>
          <BListGroupItem
            v-for="(dance, idx) in groupedDances"
            :key="idx"
            button
            :variant="groupVariant(dance)"
            :class="{ item: isGroup(dance), 'sub-item': !isGroup(dance) }"
            :active="danceId === dance.id"
            :disabled="exists(dance.id) || (isGroup(dance) && !includeGroups)"
            @click.prevent="chooseEvent(dance.id, $event)"
          >
            <DanceName
              :dance="dance"
              :show-synonyms="true"
              :show-tempo="tempoType"
              :hide-link="hideNameLink"
            />
          </BListGroupItem>
        </BListGroup>
      </BTab>
      <BTab v-if="hasTempo" title="By Tempo" active>
        <DanceDeltas
          :dances="tempoFiltered"
          :beats-per-minute="tempo"
          :beats-per-measure="numerator"
          :epsilon-percent="20"
          :hide-name-link="true"
          :tempo-type="tempoType"
          @choose-dance="choose"
        />
      </BTab>
    </BTabs>
  </BModal>
</template>

<style lang="scss" scoped>
.sub-item {
  padding-left: 2em;
}
.item {
  font-weight: bolder;
}
</style>
