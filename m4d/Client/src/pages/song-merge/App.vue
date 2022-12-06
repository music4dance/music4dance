<template>
  <page id="app">
    <song-core
      :model="songDetails"
      :environment="environment"
      :startEditing="true"
      :creating="true"
    ></song-core>
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
import { SongFilter } from "@/model/SongFilter";
import { SongHistory } from "@/model/SongHistory";
import { SongMergeModel } from "@/model/SongMergeModel";
import { TagDatabase } from "@/model/TagDatabase";
import SongCore from "@/pages/song/components/SongCore.vue";
import "reflect-metadata";
import { TypedJSON } from "typedjson";

declare const model: string;

export default AdminTools.extend({
  components: { Page, SongCore },
  props: {},
  data() {
    return new (class {
      model: SongMergeModel = TypedJSON.parse(model, SongMergeModel)!;
      environment: DanceEnvironment = safeEnvironment();
      tagDatabase: TagDatabase = safeTagDatabase();
    })();
  },
  computed: {
    songDetails(): SongDetailsModel {
      if (!this.userName) {
        throw new Error(
          "Attempting to get user specific song details with an anonymous user."
        );
      }
      return new SongDetailsModel({
        created: true,
        songHistory: SongHistory.merge(
          this.model.songId,
          this.model.songs,
          this.userName
        ),
        filter: new SongFilter(),
        userName: this.userName,
      });
    },
  },
});
</script>
