<script setup lang="ts">
import { TempoType } from "@/models/TempoType";
import { computed, ref } from "vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { NamedObject } from "@/models/DanceDatabase/NamedObject";
import { DanceDatabase } from "@/models/DanceDatabase/DanceDatabase";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { BaseColorVariant } from "bootstrap-vue-next";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import type { DanceInstance } from "@/models/DanceDatabase/DanceInstance";

const danceDB = safeDanceDatabase();

const props = withDefaults(
  defineProps<{
    id?: string;
    danceId?: string;
    filterIds?: string[];
    tempo?: number;
    numerator?: number;
    includeGroups?: boolean;
    hideNameLink?: boolean;
  }>(),
  {
    id: "dance-chooser",
    danceId: "",
    filterIds: () => [],
    tempo: 0,
    numerator: 0,
    includeGroups: false,
    hideNameLink: false,
  },
);

const emit = defineEmits<{
  "choose-dance": [id?: string, persist?: boolean, familyTag?: string];
}>();

const nameFilter = ref("");
const selectedStyleFamilies = ref<string[]>([]);
const expandedDances = ref<Set<string>>(new Set());

const danceTypes = danceDB.dances;
const allStyleFamilies = danceDB.allStyleFamilies;

const computedId = computed(() => props.id ?? "dance-chooser");

const filterAll = (dances: NamedObject[], includeChildren = false): NamedObject[] => {
  return DanceDatabase.filterByName(dances, nameFilter.value, includeChildren);
};

const sortedDances = computed(() => {
  let filtered = filterAll(danceDB.dances);

  // Filter by selected style families if any are selected
  if (selectedStyleFamilies.value.length > 0) {
    filtered = filtered.filter((dance) => {
      const danceType = dance as DanceType;
      return danceType.styleFamilies.some((family) => selectedStyleFamilies.value.includes(family));
    });
  }

  return filtered.sort((a, b) => a.name.localeCompare(b.name));
});

const toggleDanceExpansion = (danceId: string): void => {
  if (expandedDances.value.has(danceId)) {
    expandedDances.value.delete(danceId);
  } else {
    expandedDances.value.add(danceId);
  }
};

const isDanceExpanded = (danceId: string): boolean => {
  return expandedDances.value.has(danceId);
};

const getDanceInstances = (dance: NamedObject): DanceInstance[] => {
  const danceType = dance as DanceType;

  // Filter instances by selected style families if any are selected
  if (selectedStyleFamilies.value.length > 0) {
    return danceType.instances.filter((inst) =>
      selectedStyleFamilies.value.includes(inst.styleFamily),
    );
  }

  return danceType.instances;
};

const groupedDances = computed(() => {
  return filterAll(danceDB.flatGroups, true);
});

const tempoFiltered = computed(() => {
  return DanceDatabase.filterByName(danceTypes, nameFilter.value, false) as DanceType[];
});

const tempoType = TempoType.Measures;
const hasTempo = computed(() => !!props.tempo && !!props.numerator);

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
  if (id) {
    // When clicking the main dance row, don't pass a familyTag
    // The selectedStyleFamilies is only for filtering the UI list
    // Family tags should only be added when explicitly clicking a dance instance
    choose(id, persist, undefined);
  }
};

const choose = (id?: string, persist?: boolean, familyTag?: string): void => {
  emit("choose-dance", id, persist, familyTag);
};

const chooseInstance = (danceId: string, familyTag: string, event?: MouseEvent): void => {
  const persist = event?.ctrlKey;
  if (persist) {
    event?.preventDefault();
  }
  choose(danceId, persist, familyTag);
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
  <BModal :id="computedId" header-bg-variant="primary" header-text-variant="light" no-footer>
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
    <div v-if="allStyleFamilies.length > 0" class="mb-2">
      <div class="small text-muted mb-1">Filter by:</div>
      <BButtonGroup size="sm">
        <BButton
          v-for="family in allStyleFamilies"
          :key="family"
          :variant="selectedStyleFamilies.includes(family) ? 'primary' : 'outline-primary'"
          @click="
            selectedStyleFamilies.includes(family)
              ? (selectedStyleFamilies = selectedStyleFamilies.filter((f) => f !== family))
              : selectedStyleFamilies.push(family)
          "
        >
          {{ family }}
        </BButton>
      </BButtonGroup>
    </div>
    <BTabs>
      <BTab title="By Name" :active="!hasTempo">
        <BListGroup>
          <template v-for="dance in sortedDances" :key="dance.id">
            <BListGroupItem
              button
              :active="danceId === dance.id"
              :disabled="exists(dance.id)"
              class="d-flex justify-content-between align-items-center"
              @click="chooseEvent(dance.id, $event)"
            >
              <div>
                <DanceName
                  :dance="dance"
                  :show-synonyms="true"
                  :show-tempo="tempoType"
                  :hide-link="hideNameLink"
                />
              </div>
              <BButton
                v-if="getDanceInstances(dance).length > 1"
                size="sm"
                variant="link"
                @click.stop="toggleDanceExpansion(dance.id)"
              >
                <IBiChevronDown v-if="!isDanceExpanded(dance.id)" />
                <IBiChevronUp v-else />
              </BButton>
            </BListGroupItem>
            <BListGroupItem
              v-for="instance in isDanceExpanded(dance.id) ? getDanceInstances(dance) : []"
              :key="instance.id"
              button
              class="sub-item"
              :active="danceId === instance.id"
              :disabled="exists(instance.id)"
              @click="chooseInstance(dance.id, instance.styleFamily, $event)"
            >
              <span class="text-muted small">{{ instance.styleFamily }}:</span>
              {{ instance.name }}
              <span class="text-muted small">({{ instance.tempoRange.toString() }})</span>
            </BListGroupItem>
          </template>
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
            @click="chooseEvent(dance.id, $event)"
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
