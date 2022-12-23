<template>
  <b-modal
    :id="tagHandler.id"
    :header-bg-variant="tag.variant"
    header-text-variant="light"
    hide-footer
  >
    <template v-slot:modal-title>
      <b-icon :icon="tag.icon"></b-icon>&nbsp;{{ title }}
    </template>
    <b-list-group>
      <b-list-group-item :href="includeTag" variant="success" v-if="hasFilter">
        <span v-if="singleDance"> List all {{ danceName }} </span>
        <span v-else> Filter the list to include only </span>
        songs tagged as <em>{{ tag.value }}</em>
      </b-list-group-item>
      <b-list-group-item :href="excludeTag" variant="danger" v-if="hasFilter">
        <span v-if="singleDance"> List all {{ danceName }} </span>
        <span v-else> Filter the list to include only </span>
        songs <b>not</b> tagged as
        <em>{{ tag.value }}</em>
      </b-list-group-item>
      <b-list-group-item :href="includeOnly" variant="success">
        List all songs tagged as <em>{{ tag.value }}</em>
      </b-list-group-item>
      <b-list-group-item :href="excludeOnly" variant="danger">
        List all songs <b>not</b> tagged as <em>{{ tag.value }}</em>
      </b-list-group-item>
      <b-list-group-item
        href="https://music4dance.blog/tag-filtering"
        variant="info"
        target="_blank"
        >Help</b-list-group-item
      >
    </b-list-group>
  </b-modal>
</template>

<script lang="ts">
import "reflect-metadata";
import TagModalBase from "./TagModalBase";

export default TagModalBase.extend({
  computed: {
    includeOnly(): string {
      return this.getTagLink("+", true);
    },
    excludeOnly(): string {
      return this.getTagLink("-", true);
    },
    includeTag(): string {
      return this.getTagLink("+", false);
    },
    excludeTag(): string {
      return this.getTagLink("-", false);
    },
    hasFilter(): boolean {
      const filter = this.tagHandler.filter;
      return (
        !!filter && !filter.isDefault(this.tagHandler.user) && !filter.isRaw
      );
    },
    singleDance(): boolean {
      return this.tagHandler.filter?.singleDance ?? false;
    },
    danceName(): string {
      return this.tagHandler.filter?.danceQuery.danceNames[0] ?? "ERROR";
    },
  },
  methods: {
    getTagLink(modifier: string, exclusive: boolean): string {
      let link = `/song/addtags/?tags=${encodeURIComponent(
        modifier + this.tag.key
      )}`;
      const filter = this.tagHandler.filter;
      if (this.hasFilter && !exclusive) {
        link = link + `&filter=${filter!.encodedQuery}`;
      } else if (filter && filter.isDefault(this.tagHandler.user)) {
        link =
          link +
          `&filter=${filter.extractDefault(this.tagHandler.user).encodedQuery}`;
      }
      return link;
    },
  },
});
</script>
