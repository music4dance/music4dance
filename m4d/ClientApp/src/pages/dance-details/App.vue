<script setup lang="ts">
import CompetitionCategoryTable from "@/components/CompetitionCategoryTable.vue";
import DanceList from "@/components/DanceList.vue";
import DanceReference from "@/components/DanceReference.vue";
import PageFrame from "@/components/PageFrame.vue";
import SpotifyPlayer from "@/components/SpotifyPlayer.vue";
import TagCloud from "@/components/TagCloud.vue";
import { type BreadCrumbItem, danceTrail } from "@/models/BreadCrumbItem";
import { DanceModel } from "@/models/DanceModel";
import { SongFilter } from "@/models/SongFilter";
import { TempoType } from "@/models/TempoType";
import axios from "axios";
import { TypedJSON } from "typedjson";
import DanceContents from "./components/DanceContents.vue";
import DanceDescription from "./components/DanceDescription.vue";
import DanceLinks from "./components/DanceLinks.vue";
import TopTen from "./components/TopTen.vue";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import { getMenuContext } from "@/helpers/GetMenuContext";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { computed, ref } from "vue";

const danceDB = safeDanceDatabase();
const menuContext = getMenuContext();

declare const model_: string;
const model = TypedJSON.parse(model_, DanceModel)!;

const danceDescription = ref<InstanceType<typeof DanceDescription> | null>(null);
const danceLinks = ref<InstanceType<typeof DanceLinks> | null>(null);

const editing = ref(false);
const description = ref(model.description);
const links = ref(model.links);

const filter = new SongFilter();
filter.dances = model.danceId;
filter.sortOrder = "Dances";
model.filter = filter;
const dance = danceDB.fromId(model.danceId);
const histories = model.histories ?? [];
const tags = dance ? model.tags : [];
const isGroup = dance ? DanceGroup.isGroup(dance) : false;
const danceType = isGroup ? undefined : (dance as DanceType);
const showTagFilter = tags.length > 20;
const competitionInfo = danceType?.competitionDances ?? [];
const hasReferences = computed(() => !!links.value && links.value.length > 0);
const dances = isGroup ? (dance as DanceGroup).dances : [];
const groupName = danceType?.groups![0].name;
const breadCrumbDetails: BreadCrumbItem[] = isGroup
  ? ([{ text: dance!.name, active: true }] as BreadCrumbItem[])
  : ([
      { text: groupName, href: `/dances/${groupName}` },
      { text: dance!.name, active: true },
    ] as BreadCrumbItem[]);

const breadcrumbs: BreadCrumbItem[] = dance ? [...danceTrail, ...breadCrumbDetails] : danceTrail;

const modified = computed(() => danceDescription.value?.isModified || danceLinks.value?.isModified);

const startEdit = (): void => {
  editing.value = true;
};
const cancelChanges = (): void => {
  description.value = model.description;
  links.value = model.links;
  editing.value = false;
};

const saveChanges = async () => {
  try {
    await axios.patch(`/api/dances/${model.danceId}`, {
      id: model.danceId,
      description: description.value,
      danceLinks: links.value,
    });
    danceDescription.value?.commit();
    danceLinks.value?.commit();
    model.description = description.value;
    model.links = links.value;
    editing.value = false;
  } catch (e) {
    // eslint-disable-next-line no-console
    console.log(e);
    throw e;
  }
};
</script>

<template>
  <PageFrame id="app" :breadcrumbs="breadcrumbs">
    <BRow>
      <BCol
        ><h1>{{ model.danceName }}</h1></BCol
      >
      <BCol v-if="menuContext.isAdmin" cols="auto">
        <BButton v-if="editing" variant="outline-primary" class="mr-1" @click="cancelChanges"
          >Cancel</BButton
        >
        <BButton v-if="editing" variant="primary" :disabled="!modified" @click="saveChanges"
          >Save</BButton
        >
        <BButton v-if="!editing" variant="primary" @click="startEdit">Edit</BButton>
      </BCol>
    </BRow>
    <BRow>
      <BCol md="2" order-md="2">
        <DanceContents :model="model"></DanceContents>
      </BCol>
      <BCol md="10" order-md="1">
        <DanceDescription
          ref="danceDescription"
          v-model="description"
          :dance-id="model.danceId"
          :editing="editing"
        >
        </DanceDescription>
        <TopTen
          v-if="!isGroup"
          :histories="histories"
          :filter="model.filter"
          :user-name="menuContext.userName"
        ></TopTen>
        <SpotifyPlayer :playlist="model.spotifyPlaylist"></SpotifyPlayer>
        <DanceReference :dance-id="model.danceId"></DanceReference>
        <div v-if="isGroup">
          <hr />
          <h2 id="dance-styles">
            Dances that are grouped into the {{ model.danceName }} category:
          </h2>
          <DanceList :dances="dances" :show-tempo="TempoType.Both"></DanceList>
        </div>
      </BCol>
    </BRow>
    <BRow v-if="competitionInfo.length > 0" id="competition-info">
      <BCol>
        <hr />
        <CompetitionCategoryTable
          title="Competition Tempo Information"
          :dances="competitionInfo"
          :use-full-name="true"
        ></CompetitionCategoryTable>
      </BCol>
    </BRow>
    <BRow v-if="hasReferences || editing">
      <BCol>
        <DanceLinks
          ref="danceLinks"
          v-model="links"
          :editing="editing"
          :dance-id="model.danceId"
        ></DanceLinks>
      </BCol>
    </BRow>
    <BRow>
      <BCol>
        <hr />
        <h2 id="tags">Tags</h2>
        <TagCloud
          :tags="tags"
          :user="menuContext.userName"
          :song-filter="model.filter"
          :hide-filter="!showTagFilter"
        ></TagCloud>
      </BCol>
    </BRow>
  </PageFrame>
</template>
