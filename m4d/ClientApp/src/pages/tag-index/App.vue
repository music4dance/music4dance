<script setup lang="ts">
import PageFrame from "@/components/PageFrame.vue";
import TagCloud from "@/components/TagCloud.vue";
import { safeTagDatabase } from "@/helpers/TagEnvironmentManager";
import { type BreadCrumbItem, homeCrumb } from "@/models/BreadCrumbItem";

const breadcrumbs: BreadCrumbItem[] = [homeCrumb, { text: "Tags", active: true }];

const tags = safeTagDatabase().tags;
</script>

<template>
  <PageFrame id="app" title="Tag Cloud" :breadcrumbs="breadcrumbs">
    <TagCloud :tags="tags"></TagCloud>
  </PageFrame>
</template>
@/helpers/TagEnvironmentManager
