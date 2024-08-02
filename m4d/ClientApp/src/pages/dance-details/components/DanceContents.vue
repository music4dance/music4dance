<script setup lang="ts">
import { DanceModel } from "@/models/DanceModel";
import { safeDanceDatabase } from "@/helpers/DanceEnvironmentManager";
import type { DanceType } from "@/models/DanceDatabase/DanceType";
import { DanceGroup } from "@/models/DanceDatabase/DanceGroup";
import type { DanceObject } from "@/models/DanceDatabase/DanceObject";

const props = defineProps<{ model: DanceModel }>();
const danceDB = safeDanceDatabase();

const dance = danceDB.fromId(props.model.danceId)!;
const danceName = dance.name;
const danceObject = dance as DanceObject;
const isGroup = DanceGroup.isGroup(dance);
const danceType = isGroup ? undefined : (dance as DanceType);
const blog = danceObject?.blogTag;
const blogLink = blog ? `https://music4dance.blog/tag/${blog}` : undefined;
const hasPlayer = !!props.model.spotifyPlaylist;
const hasTopTen = !!props.model.histories && !!props.model.histories.length && !isGroup;
const hasReferences = !!props.model.links && !!props.model.links.length;
const hasCompetitionInfo = !!danceType && !isGroup && !!danceType.competitionDances?.length;
const hasMeter = !!danceType && danceType.meter.numerator != 1;
</script>

<template>
  <BListGroup>
    <BListGroupItem href="#description">Description</BListGroupItem>
    <BListGroupItem v-if="!isGroup && hasMeter" href="#tempo-info">Tempo Info</BListGroupItem>
    <BListGroupItem v-if="hasTopTen" href="#top-ten">Top Ten Songs</BListGroupItem>
    <BListGroupItem v-if="hasPlayer" href="#spotify-player"
      >{{ danceName }} music on Spotify</BListGroupItem
    >
    <BListGroupItem v-if="isGroup" href="#dance-styles">Dance Styles</BListGroupItem>
    <BListGroupItem v-if="hasReferences" href="#references">References</BListGroupItem>
    <BListGroupItem v-if="hasCompetitionInfo" href="#competition-info"
      >Competition Info</BListGroupItem
    >
    <BListGroupItem :href="model.filter.url" target="_blank">
      All {{ danceName }} Songs <IBiBoxArrowUpRight
    /></BListGroupItem>
    <BListGroupItem v-if="!isGroup" href="#tags">Tags</BListGroupItem>
    <BListGroupItem v-if="blogLink" :href="blogLink" target="_blank"
      >Blog <IBiBoxArrowUpRight
    /></BListGroupItem>
  </BListGroup>
</template>
