<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import { type BreadCrumbItem, infoTrail } from "@/models/BreadCrumbItem";
import { ProfileModel } from "@/models/ProfileModel";
import { TypedJSON } from "typedjson";
import { reactive } from "vue";
import { MenuContext } from "@/models/MenuContext";
import { getMenuContext } from "@/helpers/GetMenuContext";
import UserList from "./UserList.vue";
import UserProfile from "./UserProfile.vue";
declare const model_: string;

const model = reactive(TypedJSON.parse(model_, ProfileModel)!);
const breadcrumbs: BreadCrumbItem[] = [...infoTrail, { text: "User Profile", active: true }];
const menuContext: MenuContext = getMenuContext();
</script>

<template>
  <PageFrame id="app" :breadcrumbs="breadcrumbs">
    <h1>
      Info for <em>{{ model.displayName }}</em
      >'s Profile
    </h1>
    <div>
      <UserProfile :model="model" :menu-context="menuContext"></UserProfile>
      <UserList :model="model"></UserList>
    </div>
  </PageFrame>
</template>
