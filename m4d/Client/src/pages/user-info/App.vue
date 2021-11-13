<template>
  <page id="app" :breadcrumbs="breadcrumbs">
    <H1
      >Info for <em>{{ model.userName }}</em></H1
    >
    <div>
      <user-profile :model="model"></user-profile>
      <user-list :model="model"></user-list>
    </div>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { BreadCrumbItem, infoTrail } from "@/model/BreadCrumbItem";
import { ProfileModel } from "@/model/ProfileModel";
import "reflect-metadata";
import { TypedJSON } from "typedjson";
import { Component, Mixins } from "vue-property-decorator";
import UserList from "./UserList.vue";
import UserProfile from "./UserProfile.vue";
declare const model: string;

// TODONEXT:
//  Figure out tag issue :-() Casing wrong in DB?  How to correct?  How often?
//  Advanced search add tag failing?
//  Custom not found page?

@Component({
  components: {
    Page,
    UserList,
    UserProfile,
  },
})
export default class App extends Mixins(AdminTools) {
  private breadcrumbs: BreadCrumbItem[] = [
    ...infoTrail,
    { text: "User Profile", active: true },
  ];

  private model!: ProfileModel;

  constructor() {
    super();

    this.model = TypedJSON.parse(model, ProfileModel)!;
  }
}
</script>
