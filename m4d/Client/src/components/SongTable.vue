<template>
    <div>
        <b-table striped hover no-local-sorting sort-icon-left
            :items="songs" 
            :fields="fields">
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
            <template v-slot:head(title)>
                <sortable-header 
                    id="Title"
                    :tip="titleHeaderTip"
                    :enableSort="!hideSort"
                    :filter="filter"
                ></sortable-header>
            </template>
            <template v-slot:cell(title)="data">
                <a :href="songRef(data.item)">{{ data.item.title }}</a>
            </template>
            <template v-slot:head(artist)>
                <sortable-header 
                    id="Artist"
                    :tip="titleHeaderTip"
                    :enableSort="!hideSort"
                    :filter="filter"
                ></sortable-header>
            </template>
            <template v-slot:cell(artist)="data">
                <a :href="artistRef(data.item)">{{ data.item.artist }}</a>
            </template>
            <template v-slot:head(tempo)>
                <sortable-header 
                    id="Tempo"
                    title="Tempo (BPM)"
                    :tip="titleHeaderTip"
                    :enableSort="!hideSort"
                    :filter="filter"
                ></sortable-header>
            </template>
            <template v-slot:cell(tempo)="data">
                <a :href="tempoRef(data.item)">{{ data.value }}</a>
            </template>
            <template v-slot:head(echo)>
                <div :class="echoClass">
                    <sortable-header 
                        id="Beat"
                        :tip="beatTip"
                        :enableSort="!hideSort"
                        :filter="filter">
                        <img src="/images/icons/beat-10.png" width="25" height="25">
                    </sortable-header>
                    <sortable-header 
                        id="Energy"
                        :tip="energyTip"
                        :enableSort="!hideSort"
                        :filter="filter">
                        <img src="/images/icons/energy-10.png" width="25" height="25">
                    </sortable-header>
                    <sortable-header 
                        id="Mood"
                        :tip="moodTip"
                        :enableSort="!hideSort"
                        :filter="filter">
                        <img src="/images/icons/mood-10.png" width="25" height="25">
                    </sortable-header>
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
            <template v-slot:head(dances)>
                <sortable-header 
                    id="Dances"
                    :tip="titleHeaderTip"
                    :enableSort="sortableDances"
                    :filter="filter"
                ></sortable-header>
            </template>
            <template v-slot:cell(dances)="data">
                <dance-button v-for="tag in dances(data.item)" :key="tag.key" 
                    :danceHandler="danceHandler(tag, filter, data.item)"></dance-button>
            </template>
            <template v-slot:cell(tags)="data">
                <tag-button v-for="tag in tags(data.item)"
                    :key="tag.key" :tagHandler="tagHandler(tag, filter, data.item)"></tag-button>
            </template>
            <template v-slot:head(order)>
                <div class="orderHeader">
                    <sortable-header 
                        :id="orderType"
                        :tip="beatTip"
                        :enableSort="!hideSort"
                        :filter="filter">
                        <b-icon :icon="orderIcon"></b-icon>
                    </sortable-header>
                </div>
            </template>
            <template v-slot:cell(order)="data">
                <span v-b-tooltip.hover.click.blur.topleft="orderTip(data.item)">{{ data.item.modifiedOrder }}</span>
            </template>
        </b-table>
    </div>
</template>

<script lang="ts">
import 'reflect-metadata';
import DanceButton from './DanceButton.vue';
import EchoIcon from './EchoIcon.vue';
import LikeButton from './LikeButton.vue';
import PlayModal from './PlayModal.vue';
import SortableHeader from './SortableHeader.vue';
import TagButton from './TagButton.vue';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { DanceRating, Song } from '@/model/Song';
import { Tag } from '@/model/Tag';
import { DanceEnvironment } from '@/model/DanceEnvironmet';
import { SongFilter } from '@/model/SongFilter';
import { DanceHandler } from '@/model/DanceHandler';
import { TagHandler } from '@/model/TagHandler';
import { ITaggableObject } from '@/model/ITaggableObject';
import { SongSort } from '@/model/SongSort';

// TODONEXT:
//  Consider going to advanced search results once amything has been selected
//   beyond a single dance
//  Show Temnpo == 0 as empty
//  Finish up the dance modal:
//    Think about the consequences of the 2 upvote rule (and removing it)
//    Look at integrating dance/tag buttons and modals
//  Get inline dance voting working
//  Get options working (no-sort, showDate, hideDances, hideArtist, hideAlbum)
//  Get simple dance header working
//  Think about how we replace merge & other administrative functions
//  Get this to be adaptive for smaller screens
//  Look at what else we want to put in footer (the not as many songs as expected, for one...)
//  https://localhost:5001/song/filtersearch?filter=Advanced--Modified---%2Bme%7Ca

@Component({
  components: {
    DanceButton,
    EchoIcon,
    LikeButton,
    PlayModal,
    SortableHeader,
    TagButton,
  },
})
export default class SongTable extends Vue {
    @Prop() private readonly songs!: Song[];
    @Prop() private readonly filter!: SongFilter;
    @Prop() private readonly userName!: string;
    @Prop() private readonly environment!: DanceEnvironment;
    @Prop() private readonly hideSort?: boolean;
    @Prop() private readonly hiddenColumns?: string[];

    private get fields() {
        const fields = [
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
        const hidden = this.hiddenColumns;
        return !!hidden
            ? fields.filter((f) => !hidden.find((c) => c.toLowerCase() === f.key))
            : fields;
    }

    private get titleHeaderTip(): string {
        return 'Song Title: Click to sort alphabetically by title';
    }

    private songRef(song: Song): string {
        return `/song/details/${song.songId}?filter=${this.filter.encodedQuery}`;
    }

    private get artistHeaderTip(): string {
        return 'Artist: Click to sort alphabetically by artist';
    }

    private artistRef(song: Song): string {
        return `/song/artist/?name=${song.artist}`;
    }

    private get tempoHeaderTip(): string {
        return 'Tempo (Beats Per Minute): Click to sort numerically by tempo';
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

    private get echoClass(): string[] {
        const order = this.filter.sort.order;
        return (order === 'Mood' || order === 'Beat' || order === 'Energy')
            ? ['sortedEchoHeader']
            : ['echoHeader'];
    }

    private echoLink(type: string): string {
        return `https://music4dance.blog/music4dance-help/playing-or-purchasing-songs/echonest/#${type}`;
    }

    private get dancesHeaderTip(): string {
        return 'Dance: Click to sort by dance rating';
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
        return `Click to sort by date ${this.orderType.toLowerCase()}`;
    }

    private get orderType(): string {
        return this.createdOrder ? 'Created' : 'Modified';
    }

    private get orderIcon(): string {
        return (this.createdOrder) ?
            'file-earmark-plus' : 'pencil';
    }

    private danceHandler(tag: Tag, filter: SongFilter, song: Song): DanceHandler {
        return new DanceHandler(song.findDanceRatingByName(tag.value)!, tag, this.userName, filter, song);
    }

    private tagHandler(tag: Tag, filter?: SongFilter, parent?: ITaggableObject): TagHandler {
        return new TagHandler(tag, this.userName, filter, parent);
    }

    private get sortOrder(): SongSort {
        return this?.filter?.sort ?? new SongSort('Modified');
    }

    private get createdOrder(): boolean {
        return this.sortOrder.order === 'Created';
    }

    private get modifiedOrder(): boolean {
        return this.sortOrder.order === 'Modified';
    }

    private get sortableDances(): boolean {
        return !this.hideSort &&  this.filter.singleDance;
    }

    private showPlayModal(song: Song): void {
        this.$bvModal.show(song.songId);
    }
 }
</script>

<style scoped lang='scss'>
.echoHeader {
    min-width:75px
}
.sortedEchoHeader {
    min-width:100px
}
.orderHeader {
    min-width:3em
}
</style>