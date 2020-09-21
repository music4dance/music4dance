<template>
    <div>
        <b-table striped hover :items="songs" :fields="fields">
            <template v-slot:cell(play)="data">
                <like-button :song="data.item" :userName="userName" scale="1.75"></like-button>
                &nbsp;
                <a href="#" @click.prevent="showPlayModal(data.item)" role="button">
                    <b-iconstack font-scale="1.75">
                        <b-icon stacked icon="circle"></b-icon>
                        <b-icon stacked icon="play-fill" shift-h="1"></b-icon>
                    </b-iconstack>
                </a>
                <play-modal :song="data.item"></play-modal>
            </template>
            <template v-slot:cell(title)="data">
                <a :href="songRef(data.item)">{{ data.item.title }}</a>
            </template>
            <template v-slot:cell(artist)="data">
                <a :href="artistRef(data.item)">{{ data.item.artist }}</a>
            </template>
            <template v-slot:cell(tempo)="data">
                <a :href="tempoRef(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:head(echo)="data">
                <div style="min-width:75px">
                    <img
                        src="/images/icons/beat-10.png"
                        width="25" height="25"
                        v-b-tooltip.hover.click.left="beatTip">
                    <img
                        src="/images/icons/energy-10.png"
                        width="25" height="25"
                        v-b-tooltip.hover.click.left="energyTip">
                    <img
                        src="/images/icons/mood-10.png"
                        width="25" height="25"
                        v-b-tooltip.hover.click.left="moodTip">
                </div>
            </template>
            <template v-slot:cell(echo)="data" style="width:100px">
                <echo-icon 
                    :value="data.item.danceability" 
                    type="beat" label="beat strength" maxLabel="strongest beat"
                ></echo-icon>
                <echo-icon 
                    :value="data.item.energy" 
                    type="energy" label="energy level" maxLabel="highest energy"
                ></echo-icon>
                <echo-icon 
                    :value="data.item.valence" 
                    type="mood" label="mood level" maxLabel="happiest"
                ></echo-icon>
            </template>
            <template v-slot:cell(dances)="data">
                <tag-button v-for="tag in dances(data.item)" :key="tag.key" 
                    :tagHandler="tagHandler(tag)"></tag-button>
            </template>
            <template v-slot:cell(tags)="data">
                <tag-button v-for="tag in tags(data.item)"
                    :key="tag.key" :tagHandler="tagHandler(tag, filter, data.item)"></tag-button>
            </template>
            <template v-slot:head(order)="data">
                <span v-b-tooltip.hover.click.left="orderHeaderTip"><b-icon :icon="orderIcon"></b-icon></span>
            </template>
            <template v-slot:cell(order)="data">
                <span v-b-tooltip.hover.click.topleft="orderTip(data.item)">{{ data.item.modifiedOrder }}</span>
            </template>
        </b-table>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import EchoIcon from './EchoIcon.vue';
import LikeButton from './LikeButton.vue';
import PlayModal from './PlayModal.vue';
import TagButton from './TagButton.vue';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { Song } from '@/model/Song';
import { Tag } from '@/model/Tag';
import { DanceEnvironment } from '@/model/DanceEnvironmet';
import { SongFilter } from '@/model/SongFilter';
import { TagHandler } from '@/model/TagHandler';
import { ITaggableObject } from '@/model/ITaggableObject';

// TODONEXT:
//  Add filter description to advanced search results
//  Consider going to advanced search results once amything has been selected
//   beyond a single dance
//  Continue to unravel getting default user search to be persistent
//  Show Temnpo == 0 as empty
//  Figure out how to better handle the default filter (including don't like)
//  Add in the vote count for dances
//  Get the dance modal up and running
//  Get dances working
//  Get this to be adaptive for smaller screens
//  Get sorting working
//  Get pagination working

@Component({
  components: {
    EchoIcon,
    LikeButton,
    PlayModal,
    TagButton,
  },
})
export default class SongTable extends Vue {
    @Prop() private readonly songs!: Song[];
    @Prop() private readonly filter!: SongFilter;
    @Prop() private readonly userName!: string;
    @Prop() private readonly environment!: DanceEnvironment;

    private fields = [
        {
            key: 'play',
            label: 'Like/Play',
        },
        {
            key: 'title',
        },
        {
            key: 'artist',
        },
        {
            key: 'tempo',
            label: 'Tempo (BPM)',
            formatter: (value: number) => Math.round(value),
        },
        {
            key: 'echo',
        },
        {
            key: 'dances',
        },
        {
            key: 'tags',
        },
        {
            key: 'order',
        },

    ];

    private songRef(song: Song): string {
        return `/song/details/${song.songId}`; // TODO: Get filter parameter in here
    }

    private artistRef(song: Song): string {
        return `/song/artist/?name=${song.artist}`; // TODO: Get filter parameter in here
    }

    private tempoRef(song: Song): string {
        return `/home/counter?numerator=4&tempo=${song.tempo}`; // TODO: smart numerator?
    }

    private get beatTip(): string {
        return 'Strength of the beat (fuller icons represent a stronger beat). Click to sort by strength of the beat.';
    }

    private get energyTip(): string {
        return 'Energy of the song (fuller icons represent a higher energy). Click to sort by energy.';
    }

    private get moodTip(): string {
        return 'Mood of the song (fuller icons represent a happier mood). Click to sort by mood.';
    }

    private echoLink(type: string): string {
        return `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${type}`;
    }

    private dances(song: Song): Tag[] {
        return song.tags.filter((t) => !t.value.startsWith('!') && t.category.toLowerCase() === 'dance');
    }

    private tags(song: Song): Tag[] {
        return song.tags.filter((t) => !t.value.startsWith('!') && t.category.toLowerCase() !== 'dance');
    }

    private orderTip(song: Song): string {
        return `Last Modified ${song.modified} (${song.modifiedOrderVerbose} ago)`;
    }

    private get orderHeaderTip(): string {
        return `${this.sortOrder}: Click to sort by date ${this.sortOrder}`;
    }

    private get orderIcon(): string {
        return (this.sortOrder.toLowerCase() === 'created') ?
            'file-earmark-plus' : 'pencil';
    }

    private tagHandler(tag: Tag, filter?: SongFilter, parent?: ITaggableObject): TagHandler {
        return new TagHandler(tag, this.userName, filter, parent);
    }

    private get sortOrder(): string {
        return this?.filter?.sortOrder ?? 'Modified';
    }

    private showPlayModal(song: Song): void {
        this.$bvModal.show(song.songId);
    }
 }
</script>