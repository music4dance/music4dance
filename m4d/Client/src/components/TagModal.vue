<template>
    <b-modal :id="tag.key" :header-bg-variant="tag.variant" header-text-variant="light" hide-footer>
        <template v-slot:modal-title>
            <b-icon :icon="tag.icon"></b-icon>&nbsp;{{tag.value}}
        </template>
        <b-list-group>
            <b-list-group-item :href="includeTag" variant="success">List all songs tagged as <em>{{tag.value}}</em></b-list-group-item>
            <b-list-group-item :href="excludeTag" variant="danger">Exclude all songs <b>not</b> tagged as <em>{{tag.value}}</em></b-list-group-item>
            <b-list-group-item href="https://music4dance.blog/tag-filtering" variant="secondary">Help</b-list-group-item>
        </b-list-group>
    </b-modal>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { BModal } from 'bootstrap-vue';
import { Tag } from '@/model/Tag';

@Component
export default class TagModal extends Vue {
    @Prop() private readonly tag!: Tag;

    // TODO: Make this work with an existing filter
    private get includeTag(): string {
        return this.getTagLink('+');
    }

    private get excludeTag(): string {
        return this.getTagLink('-');
    }

    private getTagLink(modifier: string): string {
        return `/song/addtags/?tags=${modifier}${this.tag.key}`;
    }

 }
</script>