<script setup lang="ts">
import { safeTagDatabase } from "@/helpers/TagEnvironmentManager";
import { type BreadCrumbItem, homeCrumb } from "@/models/BreadCrumbItem";

const breadcrumbs: BreadCrumbItem[] = [homeCrumb, { text: "Tags", active: true }];

const tags = safeTagDatabase().tags;
</script>

<template>
  <PageFrame id="app" title="Tag Cloud" :breadcrumbs="breadcrumbs">
    <TagCloud :tags="tags" />
  </PageFrame>
</template>
@/helpers/TagEnvironmentManager
