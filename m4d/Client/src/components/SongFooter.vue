<template>
    <b-row>
        <b-col md="8">
            <b-pagination-nav
                :link-gen="linkGen"
                :number-of-pages="pageCount"
                limit="9"
                first-number last-number
            ></b-pagination-nav>
        </b-col>
        <b-col md="2">Page 1 of {{pageCount}} ({{count}} songs found)</b-col>
        <b-col md="2"><a href="/song/advancedsearchform">New Search</a></b-col>
    </b-row>
</template>

<script lang="ts">
import { Component, Prop, Vue } from 'vue-property-decorator';
import { DanceEnvironment } from '@/model/DanceEnvironmet';
import { SongFilter } from '@/model/SongFilter';

declare const environment: DanceEnvironment;

@Component
export default class SongFooter extends Vue {
    @Prop() private readonly filter!: SongFilter;
    @Prop() private readonly count!: number;

    private linkGen(pageNum: number): string {
        const filter = this.filter.clone();
        filter.page = pageNum;
        return filter.url;
    }

    private get pageCount(): number {
        return Math.ceil(this.count / 25);
    }
}
</script>
