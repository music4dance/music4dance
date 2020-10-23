<template>
    <div>
        <b-table striped hover no-local-sorting sort-icon-left borderless
            :items="songs" 
            :fields="fields">
            <template v-slot:head(play)>
                <div :class="likeHeader">Like/Play</div>
            </template>
            <template v-slot:cell(play)="data">
                <like-button :song="data.item" :userName="userName" scale="1.75"></like-button>
                &nbsp;
                <a href="#" @click.prevent="showPlayModal(data.item)" role="button">
                    <b-iconstack font-scale="1.75">
                        <b-icon stacked icon="circle"></b-icon>
                        <b-icon stacked icon="play-fill" shift-h="1"></b-icon>
                    </b-iconstack>
                </a>
                <vote-button v-if="filter.singleDance"
                    :song="data.item"
                    :danceRating="getDanceRating(data.item)"
                    :authenticated="!!userName"
                    style="margin-left:.25em"
                ></vote-button>
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
            <template v-slot:cell(track)="data">
                {{ trackNumber(data.item) }}
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
                <a :href="tempoRef(data.item)">{{ tempoValue(data.item) }}</a>
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

            <template v-slot:head(text)>
                <sortable-header 
                    id="Title"
                    :tip="titleHeaderTip"
                    :enableSort="!hideSort"
                    :filter="filter"
                ></sortable-header>
                -
                <sortable-header 
                    id="Artist"
                    :tip="titleHeaderTip"
                    :enableSort="!hideSort"
                    :filter="filter"
                ></sortable-header>
            </template>
            <template v-slot:cell(text)="data">
                <a :href="songRef(data.item)">{{ data.item.title }}</a> by
                <a :href="artistRef(data.item)">{{ data.item.artist }}</a>
                <span v-if="tempoValue(data.item)">
                    @ <a :href="tempoRef(data.item)">{{ tempoValue(data.item) }} BPM</a>
                </span>
            </template>
            <template v-slot:head(info)>
                <sortable-header 
                    id="Dances"
                    :tip="titleHeaderTip"
                    :enableSort="sortableDances"
                    :filter="filter"
                ></sortable-header> - Tags
            </template>
            <template v-slot:cell(info)="data">
                <dance-button v-for="tag in dances(data.item)" :key="tag.key" 
                    :danceHandler="danceHandler(tag, filter, data.item)"></dance-button>
                <tag-button v-for="tag in tags(data.item)"
                    :key="tag.key" :tagHandler="tagHandler(tag, filter, data.item)"></tag-button>
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
import VoteButton from './VoteButton.vue';
import { Component, Prop, Vue } from 'vue-property-decorator';
import { DanceRating, Song } from '@/model/Song';
import { Tag } from '@/model/Tag';
import { SongFilter } from '@/model/SongFilter';
import { DanceHandler } from '@/model/DanceHandler';
import { TagHandler } from '@/model/TagHandler';
import { ITaggableObject } from '@/model/ITaggableObject';
import { SongSort } from '@/model/SongSort';

// TODONEXT:
//  Consider going to advanced search results once anything has been selected
//   beyond a single dance
//  Get all pages that use dancestatis to pull from the same place
//    Get an async loading template working
//    Rework pages that use this
//       tempo-counter
//       tempo-list
//       dance-index
//       tag-index
//  Move album page to vue
//    Consider if we want track to show up in mobile
//    Consider if we want an option of having an album column
//  Move dance pages to vue
//  Finish up the dance modal:
//    Look at integrating dance/tag buttons and modals
//  Think about how we replace merge & other administrative functions
//  Look at what else we want to put in footer (the not as many songs as expected, for one...)
//  Figure out if there's a race condition when loading
//  https://localhost:5001/song/filtersearch?filter=Advanced--Modified---%2Bme%7Ca

interface IField {
    key: string;
    label?: string;
}

const playField = { key: 'play' };
const titleField = { key: 'title' };
const artistField = { key: 'artist' };
const trackField = { key: 'track' };
const tempoField = { key: 'tempo', label: 'Tempo (BPM)' };
const echoField = { key: 'echo'};
const dancesField = { key: 'dances' };
const tagsField = { key: 'tags' };
const orderField = { key: 'order' };
const textField = { key: 'text' };
const infoField = { key: 'info' };

@Component({
  components: {
    DanceButton,
    EchoIcon,
    LikeButton,
    PlayModal,
    SortableHeader,
    TagButton,
    VoteButton,
  },
})
export default class SongTable extends Vue {
    @Prop() private readonly songs!: Song[];
    @Prop() private readonly filter!: SongFilter;
    @Prop() private readonly userName!: string;
    @Prop() private readonly hideSort?: boolean;
    @Prop() private readonly hiddenColumns?: string[];

    private get fields() {
        const mq = (this as any).$mq;

        const fields = [
            playField,
            titleField,
            artistField,
            trackField,
            tempoField,
            echoField,
            dancesField,
            tagsField,
            orderField,
        ];

        const smallFields = [
            playField,
            textField,
            infoField,
        ];

        const hidden = this.hiddenColumns;
        if (mq === 'sm' || mq === 'md') {
            const temp = smallFields.map((f) => this.filterSmallField(f));
            return temp;
        } else {
            return !!hidden
                ? fields.filter((f) => !this.isHidden(f.key))
                : fields;
        }
    }

    private filterSmallField(field: IField): IField {
        if (field === textField && this.isHidden('artist')) {
            return titleField;
        } else if (field === infoField && this.isHidden('dances')) {
            return tagsField;
        } else {
            return field;
        }
    }

    private get likeHeader(): string[] {
        return this.filter.singleDance ? ['likeDanceHeader'] : ['likeHeader'];
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

    private trackNumber(song: Song): string {
        return (song.albums.length > 0 && song.albums[0].track)
            ? song.albums[0].track.toString()
            : '';
    }

    private get tempoHeaderTip(): string {
        return 'Tempo (Beats Per Minute): Click to sort numerically by tempo';
    }

    private tempoRef(song: Song): string {
        return `/home/counter?numerator=4&tempo=${song.tempo}`; // TODO: smart numerator?
    }

    private tempoString(song: Song): string {
        const tempo = song.tempo;
        return tempo ? `@ ${Math.round(tempo)} BPM` : '';
    }

    private tempoValue(song: Song): string {
        const tempo = song.tempo;
        return tempo  && !this.isHidden('tempo') ? `${Math.round(tempo)}` : '';
    }

    private isHidden(column: string): boolean {
        const hidden = this.hiddenColumns;
        const col = column.toLowerCase();
        return !!hidden && !!hidden.find((c) => c.toLowerCase() === col);
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

    private getDanceRating(song: Song): DanceRating {
        return song.findDanceRatingById(this.filter.danceQuery.danceList[0])!;
    }

    private showPlayModal(song: Song): void {
        this.$bvModal.show(song.songId);
    }
 }
</script>

<style scoped lang='scss'>
.likeHeader {
    min-width: 4em;
}

.likeDanceHeader {
    min-width: 6em;
}

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
