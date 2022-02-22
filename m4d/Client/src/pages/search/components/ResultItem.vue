<template>
  <div class="mb-1">
    <div><a v-html="entry.title" :href="entry.url" class="title"></a></div>
    <div class="link">
      <a :href="entry.url">{{ entry.url }}</a>
    </div>
    <div v-html="entry.description"></div>
  </div>
</template>

<script lang="ts">
import { SearchPage } from "@/model/SearchPage";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";

@Component({ components: {} })
export default class ResultItem extends Vue {
  @Prop() private entry!: SearchPage;
}
</script>

<style lang="scss" scoped>
.title {
  font-size: 1.5rem;
  font-weight: bold;
  line-height: 1.1rem;
}
.text {
  width: auto;
}
.link {
  margin-top: -0.25rem;
}
</style>
