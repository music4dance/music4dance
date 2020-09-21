<template>
    <b-modal :id="tagHandler.id" :header-bg-variant="tag.variant" header-text-variant="light" hide-footer>
        <template v-slot:modal-title>
            <b-icon :icon="tag.icon"></b-icon>&nbsp;{{title}}
        </template>
        <b-list-group>
            <b-list-group-item :href="includeTag" variant="success" v-if="hasFilter">
                Filter the list to include only songs tagged as <em>{{tag.value}}</em>
            </b-list-group-item>
            <b-list-group-item :href="excludeTag" variant="danger" v-if="hasFilter">
                Filter the list to include only songs <b>not</b> tagged as <em>{{tag.value}}</em>
            </b-list-group-item>
            <b-list-group-item :href="includeOnly" variant="success">
                List all songs tagged as <em>{{tag.value}}</em>
            </b-list-group-item>
            <b-list-group-item :href="excludeOnly" variant="danger">
                List all songs <b>not</b> tagged as <em>{{tag.value}}</em>
            </b-list-group-item>
            <b-list-group-item href="https://music4dance.blog/tag-filtering" variant="secondary" target="_blank">Help</b-list-group-item>
        </b-list-group>
    </b-modal>
</template>

<script lang="ts">
import 'reflect-metadata';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { Tag } from '@/model/Tag';
import { Song } from '@/model/Song';
import { SongFilter } from '@/model/SongFilter';
import { TagHandler } from '@/model/TagHandler';

@Component
export default class TagModal extends Vue {
    @Prop() private readonly tagHandler!: TagHandler;

    private get tag(): Tag {
        return this.tagHandler.tag;
    }

    private get includeOnly(): string {
        return this.getTagLink('+', true);
    }

    private get excludeOnly(): string {
        return this.getTagLink('-', true);
    }

    private get includeTag(): string {
        return this.getTagLink('+', false);
    }

    private get excludeTag(): string {
        return this.getTagLink('-', false);
    }

    private getTagLink(modifier: string, exclusive: boolean): string {
        let link = `/song/addtags/?tags=${encodeURIComponent(modifier + this.tag.key)}`;
        const filter = this.tagHandler.filter;
        if (this.hasFilter && !exclusive) {
            link = link + `&filter=${filter!.encodedQuery}`;
        } else if (filter && filter.isDefault(this.tagHandler.user)) {
            link = link + `&filter=${filter.extractDefault(this.tagHandler.user).encodedQuery}`;
        }
        return link;
    }

    private get title(): string {
        const parent = this.tagHandler.parent;
        return parent ? parent.description : this.tag.value;
    }

    private get hasFilter(): boolean {
        const filter = this.tagHandler.filter;
        return !!filter && !filter.isDefault(this.tagHandler.user);
    }
 }
</script>