<template>
  <page id="app">
    <song-core :model="model" :environment="environment"></song-core>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import {
  safeEnvironment,
  safeTagDatabase,
} from "@/helpers/DanceEnvironmentManager";
import AdminTools from "@/mix-ins/AdminTools";
import { DanceEnvironment } from "@/model/DanceEnvironment";
import { SongDetailsModel } from "@/model/SongDetailsModel";
import { TagDatabase } from "@/model/TagDatabase";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import SongCore from "./components/SongCore.vue";

declare const model: string;

// TODO:
//  Figure out how to be better about updating global taglist - this works, but we're doing
//   it every 6 hours which seems too inferquent - but it's an expensiveoperation, so
//   don't necessarily want to so it if not needed
//  Consider removing negating properties (this would help state changes)
//  Moderator remove dance: Uses UserProxy to removeTag for the dance
//   for each user, then reproxies to the moderator to negate the dance rating.
//   Consider just having a moderator event in the details that negates cancels out a dance.
//  Consider property history as a public facing feature
//  Make property history editable?

// TODO: I haven't been able to use the EnvironmentManager mixin because SongCore depends
//  on watching "environment" and for some reason that's not firing when using envirnomentmanager
export default AdminTools.extend({
  components: { Page, SongCore },
  props: {},
  data() {
    return new (class {
      model: SongDetailsModel = TypedJSON.parse(model, SongDetailsModel)!;
      environment: DanceEnvironment = safeEnvironment();
      tagDatabase: TagDatabase = safeTagDatabase();
    })();
  },
  computed: {},
});
</script>
