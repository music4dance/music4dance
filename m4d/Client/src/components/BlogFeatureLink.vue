<template>
    <div v-if="!entry.oneTime" style="margin-left:2em">
        <div class="info">
            <img src="/images/icons/info.png" alt="info" width="24" height="24">
            {{entry.title}}
        </div>
        <div style="margin-left: 2em">
            <span v-html="entry.description"></span>
            <a :href="entry.fullPath" style="margin-left:.5em">[Read more]</a>
        </div>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { SiteMapEntry } from '@/model/SiteMapInfo';

@Component
export default class BlogFeatureLink extends Vue {
    @Prop() private readonly entry!: SiteMapEntry;
 }
</script>