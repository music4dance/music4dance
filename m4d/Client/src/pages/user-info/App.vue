<template>
  <page id="app" :breadcrumbs="breadcrumbs">
    <H1
      >Info for <em>{{ model.displayName }}</em
      >'s Profile</H1
    >
    <div>
      <user-profile :model="model"></user-profile>
      <user-list :model="model"></user-list>
    </div>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { BreadCrumbItem, infoTrail } from "@/model/BreadCrumbItem";
import { ProfileModel } from "@/model/ProfileModel";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import Vue from "vue";
import UserList from "./UserList.vue";
import UserProfile from "./UserProfile.vue";
declare const model: string;

export default Vue.extend({
  components: {
    Page,
    UserList,
    UserProfile,
  },
  data() {
    return new (class {
      model: ProfileModel = TypedJSON.parse(model, ProfileModel)!;
      breadcrumbs: BreadCrumbItem[] = [
        ...infoTrail,
        { text: "User Profile", active: true },
      ];
    })();
  },
});
</script>
